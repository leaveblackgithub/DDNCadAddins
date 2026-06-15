using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
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

                var boundaryPoints = this.SelectBoundaryPolyline(ed);
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return;
                }

                var entityIds = this.SelectEntitiesToCrop(ed);
                if (entityIds == null || entityIds.Count == 0)
                {
                    return;
                }

                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var cropService = new CropService(new CropGeometryService());
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
                                ? $"\nCROPINSIDE 完成: 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个"
                                : $"\nCROPOUTSIDE 完成: 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个");
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
        /// </summary>
        /// <returns>边界顶点列表（WCS），如果取消或选择无效则返回 null.</returns>
        private List<DDNCadAddins.Core.Models.Point2D> SelectBoundaryPolyline(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择闭合曲线作为裁剪边界（圆/椭圆/闭合多段线/闭合样条线）: ",
                    AllowDuplicates = false
                };

                var filter = new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "POLYLINE"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "ELLIPSE"),
                    new TypedValue((int)DxfCode.Start, "SPLINE"),
                    new TypedValue((int)DxfCode.Operator, "OR>"),
                });

                var promptResult = ed.GetSelection(options, filter);
                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择边界曲线或选择被取消。");
                    return null;
                }

                if (promptResult.Value.Count != 1)
                {
                    ed.WriteMessage("\n请选择一条闭合曲线作为裁剪边界（圆/椭圆/闭合多段线/闭合样条线均可）。");
                    return null;
                }

                var curveId = promptResult.Value[0].ObjectId;
                var points = new List<DDNCadAddins.Core.Models.Point2D>();

                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var curve = serviceTrans.GetObject<Curve>(curveId);
                    if (curve == null)
                    {
                        return;
                    }

                    if (!curve.Closed)
                    {
                        ed.WriteMessage("\n所选的边界曲线未闭合，请选择闭合曲线。");
                        return;
                    }

                    const int sampleCount = 64;
                    var startParam = curve.StartParam;
                    var endParam = curve.EndParam;

                    for (var i = 0; i < sampleCount; i++)
                    {
                        var param = startParam + (endParam - startParam) * i / sampleCount;
                        var pt = curve.GetPointAtParameter(Math.Min(param, endParam));
                        points.Add(new DDNCadAddins.Core.Models.Point2D(pt.X, pt.Y));
                    }

                    var deduped = new List<DDNCadAddins.Core.Models.Point2D>();
                    foreach (var p in points)
                    {
                        if (deduped.Count == 0)
                        {
                            deduped.Add(p);
                            continue;
                        }

                        var last = deduped[deduped.Count - 1];
                        var dx = Math.Abs(last.X - p.X);
                        var dy = Math.Abs(last.Y - p.Y);
                        if (dx > 1e-6 || dy > 1e-6)
                        {
                            deduped.Add(p);
                        }
                    }

                    points = deduped;
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
