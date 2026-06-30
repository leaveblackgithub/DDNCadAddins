using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropInsideCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     裁剪命令-保留边界内部：选择闭合多段线作为裁剪边界，再选择要裁剪的实体.
    /// </summary>
    public class CropInsideCommand
    {
        /// <summary>
        ///     执行 CROPINSIDE 命令：保留边界内部的实体.
        /// </summary>
        [CommandMethod("CROPINSIDE")]
        public void Execute()
        {
            this.ExecuteCrop(keepInside: true);
        }

        /// <summary>
        ///     执行 CROPOUTSIDE 命令：保留边界外部的实体.
        /// </summary>
        [CommandMethod("CROPOUTSIDE")]
        public void ExecuteOutside()
        {
            this.ExecuteCrop(keepInside: false);
        }

        /// <summary>
        ///     核心执行逻辑.
        /// </summary>
        private void ExecuteCrop(bool keepInside)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager
                    .MdiActiveDocument;
                var ed = doc.Editor;

                var boundaryPoints = this.SelectBoundaryPolyline(ed, out var boundaryId);
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return;
                }

                var entityIds = this.SelectEntitiesToCrop(ed);
                if (entityIds == null || entityIds.Count == 0)
                {
                    return;
                }
                // 排除边界自身
                entityIds.RemoveAll(id => id == boundaryId);

                // ── 采集 UCS 和边界顶点 ──
                ServiceACAD.TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);
                var capturedUcsOrigin = ucsOrigin;
                var capturedUcsX = ucsX;
                var capturedUcsY = ucsY;
                var capturedBoundaryVerts = boundaryPoints;
                string commandName = keepInside ? "CROPINSIDE" : "CROPOUTSIDE";

                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        // ★ 在裁剪前采集实体几何快照（避免原始实体被擦除后查不到）
                        var geoService = new CropGeometryService();
                        var snapshots = ServiceACAD.TestRecorder.CollectSnapshots(
                            serviceTrans, entityIds, boundaryPoints, geoService);

                        var cropService = new CropService(geoService);
                        var input = new CropInput
                        {
                            BoundaryPoints = boundaryPoints.AsReadOnly(),
                            EntityIds = entityIds,
                            TransactionService = serviceTrans,
                        };

                        var result = keepInside
                            ? cropService.CropInside(input)
                            : cropService.CropOutside(input);

                        if (!result.IsSuccess)
                        {
                            ed.WriteMessage($"\n裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }

                        var cropResult = result.Data;
                        ed.WriteMessage(
                            keepInside
                                ? $"\n{commandName} 完成: 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个"
                                : $"\n{commandName} 完成: 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个");

                        // ── 完整几何测试记录（防御性包围） ──
                        try
                        {
                            var record = new CropTestRecord
                            {
                                Command = commandName,
                                Direction = keepInside ? "Inside" : "Outside",
                                IsSuccess = true,
                                UcsOrigin = capturedUcsOrigin,
                                UcsXAxis = capturedUcsX,
                                UcsYAxis = capturedUcsY,
                                BoundaryVertices = capturedBoundaryVerts,
                                BoundaryVertexCount = capturedBoundaryVerts.Count,
                                TotalEntityCount = entityIds.Count,
                                DeletedCount = cropResult.DeletedCount,
                                SplitCount = cropResult.SplitCount,
                                KeptCount = cropResult.KeptCount,
                                SkippedCount = cropResult.SkippedCount,
                            };
                            record.Entities = snapshots;
                            var uid = ServiceACAD.TestRecorder.Record(record);
                            ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                        }
                        catch (System.Exception recEx)
                        {
                            Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                            ed.WriteMessage($"\n[TestRecorder] 记录失败: {recEx.Message}");
                        }
                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"ExecuteCrop 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROP 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROP 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     选择闭合曲线作为裁剪边界（圆、椭圆、闭合多段线、闭合样条线等）.
        ///     委托给 <see cref="CurveToPolygonConverter.ConvertCurveToPolygon"/> 自动选择精确/拟合策略.
        /// </summary>
        /// <returns>边界顶点列表（WCS），如果取消或选择无效则返回 null.</returns>
        private List<DDNCadAddins.Core.Models.Point2D> SelectBoundaryPolyline(Editor ed, out ObjectId boundaryId)
        {
            boundaryId = ObjectId.Null;
            try
            {
                // 单选边界（GetEntity 天然单选，选中即确认，不需额外提示）
                var opt = new PromptEntityOptions("\n选择闭合曲线作为裁剪边界");
                opt.SetRejectMessage("请选择圆、椭圆、闭合多段线或闭合样条线。");
                opt.AddAllowedClass(typeof(Curve), exactMatch: false);

                var res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n*取消*");
                    return null;
                }

                boundaryId = res.ObjectId;
                var capturedId = boundaryId;
                var points = new List<DDNCadAddins.Core.Models.Point2D>();

                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var curve = serviceTrans.GetObject<Curve>(capturedId);
                    if (curve == null)
                    {
                        return;
                    }

                    if (!curve.Closed)
                    {
                        ed.WriteMessage("\n所选的边界曲线未闭合，请选择闭合曲线。");
                        return;
                    }

                    // 使用 CurveToPolygonConverter 自动选择精确/拟合策略
                    var generator = new ServiceACAD.CurveToPolygonConverter();
                    var polygon = generator.ConvertCurveToPolygon(curve);
                    if (polygon != null && polygon.Count >= 3)
                    {
                        points.AddRange(polygon);
                    }
                });

                if (points.Count < 3)
                {
                    ed.WriteMessage("\n边界曲线顶点不足，请选择更大的闭合曲线。");
                    return null;
                }

                return points;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择边界曲线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择边界曲线失败: {ex.Message}");
                boundaryId = ObjectId.Null;
                return null;
            }
        }

        /// <summary>
        ///     选择要裁剪的实体.
        /// </summary>
        private List<ObjectId> SelectEntitiesToCrop(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要裁剪的实体: ",
                    AllowDuplicates = false
                };

                var promptResult = ed.GetSelection(options, new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LINE"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "POLYLINE"),
                    new TypedValue((int)DxfCode.Start, "ARC"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "ELLIPSE"),
                    new TypedValue((int)DxfCode.Start, "SPLINE"),
                    new TypedValue((int)DxfCode.Start, "DBPOINT"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Start, "MTEXT"),
                    new TypedValue((int)DxfCode.Start, "DIMENSION"),
                    new TypedValue((int)DxfCode.Start, "HATCH"),
                    new TypedValue((int)DxfCode.Start, "INSERT"),
                    new TypedValue((int)DxfCode.Operator, "OR>"),
                }));

                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择实体或选择被取消。");
                    return null;
                }

                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in promptResult.Value)
                {
                    ids.Add(selObj.ObjectId);
                }

                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择待裁剪实体失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择待裁剪实体失败: {ex.Message}");
                return null;
            }
        }
    }
}