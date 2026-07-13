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
        ///     执行 CROPINSIDE 命令：减掉内部，保留外部.
        /// </summary>
        [CommandMethod("CROPINSIDE")]
        public void Execute()
        {
            this.ExecuteCrop(keepInside: false);
        }

        /// <summary>
        ///     执行 CROPOUTSIDE 命令：减掉外部，保留内部.
        /// </summary>
        [CommandMethod("CROPOUTSIDE")]
        public void ExecuteOutside()
        {
            this.ExecuteCrop(keepInside: true);
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

                // ★ 使用 CropBoundaryFactory 创建精确边界（圆/椭圆解析，不再采样为多义线）
                var (boundary, boundaryPoints, boundaryId) = this.SelectBoundaryCurve(ed);
                if (boundary == null || boundaryPoints == null || boundaryPoints.Count < 3)
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
                string commandName = keepInside ? "CROPOUTSIDE" : "CROPINSIDE";

                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        // ── 统一裁剪：CropService.CropInsideOutside 自动分离 Hatch/非 Hatch ──
                        var geoService = new CropGeometryService();
                        var cropService = new CropService(geoService);

                        var result = cropService.CropInsideOutside(
                            boundary, boundaryPoints.AsReadOnly(), entityIds,
                            boundaryId, keepInside, serviceTrans);

                        if (!result.IsSuccess)
                        {
                            ed.WriteMessage($"\n裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }

                        var cropResult = result.Data;
                        ed.WriteMessage(
                            $"\n{commandName} 裁剪: 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个, 新 Hatch {cropResult.NewHatchesCreated} 个");

                        // ── TestRecorder 记录 ──
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
        ///     选择闭合曲线作为裁剪边界，并创建对应的 ICropBoundary.
        ///     <para>圆/椭圆使用精确解析边界；多段线使用多边形边界；样条线使用采样代理.</para>
        /// </summary>
        /// <returns>(ICropBoundary, 多边形顶点列表, 边界ObjectId)；取消或无效返回 (null, null, ObjectId.Null).</returns>
        private (DDNCadAddins.Core.Interfaces.ICropBoundary, List<DDNCadAddins.Core.Models.Point2D>, ObjectId)
            SelectBoundaryCurve(Editor ed)
        {
            var boundaryId = ObjectId.Null;
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
                    return (null, null, ObjectId.Null);
                }

                boundaryId = res.ObjectId;
                var capturedId = boundaryId;
                DDNCadAddins.Core.Interfaces.ICropBoundary boundary = null;
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

                    // ★ 使用 CropBoundaryFactory 创建精确边界
                    boundary = ServiceACAD.CropBoundaryFactory.CreateFromCurve(curve);

                    // 同时获取近似多边形（用于 TestRecorder 快照兼容）
                    if (boundary != null)
                    {
                        var polygon = boundary.GetApproximatePolygon();
                        if (polygon != null && polygon.Count >= 3)
                        {
                            points.AddRange(polygon);
                        }
                    }
                });

                if (boundary == null || points.Count < 3)
                {
                    ed.WriteMessage("\n边界曲线无效或顶点不足，请选择更大的闭合曲线。");
                    return (null, null, ObjectId.Null);
                }

                return (boundary, points, boundaryId);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择边界曲线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择边界曲线失败: {ex.Message}");
                return (null, null, ObjectId.Null);
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
