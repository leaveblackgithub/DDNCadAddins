using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Services;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropPolylineCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     多段线裁剪命令 - 专门处理 Polyline 类型的裁剪.
    ///     选择一条闭合曲线作为裁剪边界，然后选择要裁剪的多段线（或自动选择所有多段线）.
    ///     支持选择保留边界内部或外部的多段线.
    /// </summary>
    public class CropPolylineCommand
    {
        /// <summary>
        ///     执行 CROPPOLYLINE 命令：选择边界（单选），再选择多段线，询问裁剪方向后裁剪.
        /// </summary>
        [CommandMethod("CROPPOLYLINE")]
        public void Execute()
        {
            this.ExecuteCropPolyline(selectAllPolylines: false);
        }

        /// <summary>
        ///     执行 CROPALLPOLYLINES 命令：选择边界（单选），自动选择所有多段线，询问裁剪方向后裁剪.
        /// </summary>
        [CommandMethod("CROPALLPOLYLINES")]
        public void ExecuteAll()
        {
            this.ExecuteCropPolyline(selectAllPolylines: true);
        }

        /// <summary>
        ///     核心执行逻辑.
        /// </summary>
        /// <param name="selectAllPolylines">是否自动选择图纸中所有多段线，false 则让用户手动选择.</param>
        private void ExecuteCropPolyline(bool selectAllPolylines)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager
                    .MdiActiveDocument;
                var ed = doc.Editor;

                // 1. 选择边界（单选）
                var boundaryPoints = this.SelectSingleBoundaryCurve(ed);
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return;
                }

                // 2. 选择或获取多段线
                List<ObjectId> polylineIds = null;
                if (selectAllPolylines)
                {
                    ed.WriteMessage("\n正在自动选择图纸中所有多段线...");
                    List<ObjectId> autoPolylineIds = null;
                    CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                    {
                        autoPolylineIds = serviceTrans.GetChildObjectsFromModelspace<Polyline>();
                    });

                    if (autoPolylineIds == null || autoPolylineIds.Count == 0)
                    {
                        ed.WriteMessage("\n图纸中没有找到任何多段线。");
                        return;
                    }

                    polylineIds = autoPolylineIds;
                    ed.WriteMessage($"\n已自动选择 {polylineIds.Count} 条多段线。");
                }
                else
                {
                    polylineIds = this.SelectPolylinesToCrop(ed);
                    if (polylineIds == null || polylineIds.Count == 0)
                    {
                        return;
                    }
                }

                // 3. 询问裁剪方向：保留内部还是外部
                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue)
                {
                    return; // 用户取消
                }

                // 4. 执行裁剪
                var capturedPolylineIds = polylineIds;
                var capturedKeepInside = keepInside.Value;
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var cropPolylineService = new CropPolylineService(new CropGeometryService());
                        var result = capturedKeepInside
                            ? cropPolylineService.CropPolylinesInside(boundaryPoints, capturedPolylineIds, serviceTrans)
                            : cropPolylineService.CropPolylinesOutside(boundaryPoints, capturedPolylineIds, serviceTrans);

                        if (!result.IsSuccess)
                        {
                            ed.WriteMessage($"\n多段线裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }

                        var cropResult = result.Data;
                        string commandName = selectAllPolylines ? "CROPALLPOLYLINES" : "CROPPOLYLINE";
                        string direction = keepInside.Value ? "内部" : "外部";
                        ed.WriteMessage(
                            $"\n{commandName} 完成 ({direction}): 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个");
                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"ExecuteCropPolyline 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"多段线裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPPOLYLINE 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPPOLYLINE 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     询问裁剪方向：保留边界内部还是外部.
        /// </summary>
        /// <returns>true 保留内部，false 保留外部，null 表示取消.</returns>
        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions("\n请选择裁剪方向 [内部(N)/外部(W)]: ", "内部 外部");
                options.Keywords.Add("内部", "内部(N)", "保留边界内部的多段线部分");
                options.Keywords.Add("外部", "外部(W)", "保留边界外部的多段线部分");
                options.Keywords.Default = "内部";
                options.AllowNone = true;

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                {
                    ed.WriteMessage("\n取消裁剪方向选择。");
                    return null;
                }

                if (result.StringResult == "内部")
                {
                    return true;
                }
                else if (result.StringResult == "外部")
                {
                    return false;
                }

                // 默认使用"内部"
                return true;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"询问裁剪方向失败: {ex.Message}", ex);
                ed.WriteMessage($"\n询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     选择一条闭合曲线作为裁剪边界（单选）.
        /// </summary>
        /// <returns>边界顶点列表（WCS），如果取消或选择无效则返回 null.</returns>
        private List<DDNCadAddins.Core.Models.Point2D> SelectSingleBoundaryCurve(Editor ed)
        {
            try
            {
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

                var options = new PromptEntityOptions("\n选择裁剪边界曲线（单选）: ");
                options.SetRejectMessage("\n请选择圆、椭圆、闭合多段线或闭合样条线作为裁剪边界。");
                options.AddAllowedClass(typeof(Curve), false);

                var promptResult = ed.GetEntity(options);
                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择边界曲线或选择被取消。");
                    return null;
                }

                var curveId = promptResult.ObjectId;
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
        ///     选择要裁剪的多段线.
        /// </summary>
        private List<ObjectId> SelectPolylinesToCrop(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要裁剪的多段线: ",
                    AllowDuplicates = false
                };

                var promptResult = ed.GetSelection(options, new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "POLYLINE"),
                    new TypedValue((int)DxfCode.Operator, "OR>"),
                }));

                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择多段线或选择被取消。");
                    return null;
                }

                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in promptResult.Value)
                {
                    ids.Add(selObj.ObjectId);
                }

                ed.WriteMessage($"\n已选择 {ids.Count} 条多段线。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择待裁剪多段线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择待裁剪多段线失败: {ex.Message}");
                return null;
            }
        }
    }
}