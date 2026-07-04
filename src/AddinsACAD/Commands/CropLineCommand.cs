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

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropLineCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     直线裁剪命令 - 专门处理 Line 类型的裁剪.
    ///     选择一条闭合曲线作为裁剪边界，然后选择要裁剪的直线（或自动选择所有直线）.
    ///     支持选择保留边界内部或外部的直线.
    /// </summary>
    public class CropLineCommand
    {
        /// <summary>
        ///     执行 CROPLINE 命令：选择边界（单选），再选择直线，询问裁剪方向后裁剪.
        /// </summary>
        [CommandMethod("CROPLINE")]
        public void Execute()
        {
            this.ExecuteCropLine(selectAllLines: false);
        }

        /// <summary>
        ///     执行 CROPALLLINES 命令：选择边界（单选），自动选择所有直线，询问裁剪方向后裁剪.
        /// </summary>
        [CommandMethod("CROPALLLINES")]
        public void ExecuteAll()
        {
            this.ExecuteCropLine(selectAllLines: true);
        }

        /// <summary>
        ///     核心执行逻辑.
        /// </summary>
        /// <param name="selectAllLines">是否自动选择图纸中所有直线，false 则让用户手动选择.</param>
        private void ExecuteCropLine(bool selectAllLines)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager
                    .MdiActiveDocument;
                var ed = doc.Editor;

                // 1. 选择边界（单选）
                var boundaryPoints = this.SelectSingleBoundaryCurve(ed, out var boundaryId);
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return;
                }

                // 2. 选择或获取直线
                List<ObjectId> lineIds = null;
                if (selectAllLines)
                {
                    ed.WriteMessage("\n正在自动选择图纸中所有直线...");
                    List<ObjectId> autoLineIds = null;
                    CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                    {
                        autoLineIds = serviceTrans.GetChildObjectsFromModelspace<Line>();
                    });

                    if (autoLineIds == null || autoLineIds.Count == 0)
                    {
                        ed.WriteMessage("\n图纸中没有找到任何直线。");
                        return;
                    }

                    // 排除边界自身
                    autoLineIds.RemoveAll(id => id == boundaryId);
                    if (autoLineIds.Count == 0) { ed.WriteMessage("\n排除边界后没有其他直线。"); return; }
                    ed.WriteMessage($"\n已排除边界直线，剩余 {autoLineIds.Count} 条。");
                    lineIds = autoLineIds;
                    ed.WriteMessage($"\n已自动选择 {lineIds.Count} 条直线。");
                }
                else
                {
                    lineIds = this.SelectLinesToCrop(ed);
                    if (lineIds == null || lineIds.Count == 0)
                    {
                        return;
                    }
                    // 手动选择也排除边界自身
                    lineIds.RemoveAll(id => id == boundaryId);
                }

                // 3. 询问裁剪方向：保留内部还是外部
                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue)
                {
                    return; // 用户取消
                }

                // ── 采集 UCS 和边界顶点 ──
                ServiceACAD.TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);
                var capturedUcsOrigin = ucsOrigin;
                var capturedUcsX = ucsX;
                var capturedUcsY = ucsY;
                var capturedBoundaryVerts = boundaryPoints;
                // 4. 执行裁剪
                var capturedLineIds = lineIds;
                var capturedKeepInside = keepInside.Value;
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var cropLineService = new CropLineService(new CropGeometryService());
                        var result = capturedKeepInside
                            ? cropLineService.CropLinesInside(boundaryPoints, capturedLineIds, serviceTrans)
                            : cropLineService.CropLinesOutside(boundaryPoints, capturedLineIds, serviceTrans);

                        if (!result.IsSuccess)
                        {
                            ed.WriteMessage($"\n直线裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }

                        var cropResult = result.Data;
                        string commandName = selectAllLines ? "CROPALLLINES" : "CROPLINE";
                        string direction = capturedKeepInside ? "内部" : "外部";

                        // ── 完整几何测试记录 ──
                        var record = new CropTestRecord
                        {
                            Command = commandName,
                            Direction = capturedKeepInside ? "Inside" : "Outside",
                            IsSuccess = true,
                            UcsOrigin = capturedUcsOrigin,
                            UcsXAxis = capturedUcsX,
                            UcsYAxis = capturedUcsY,
                            BoundaryVertices = capturedBoundaryVerts,
                            BoundaryVertexCount = capturedBoundaryVerts.Count,
                            TotalEntityCount = capturedLineIds.Count,
                            DeletedCount = cropResult.DeletedCount,
                            SplitCount = cropResult.SplitCount,
                            KeptCount = cropResult.KeptCount,
                            SkippedCount = cropResult.SkippedCount,
                        };
                        record.Entities = ServiceACAD.TestRecorder.CollectSnapshots(
                            serviceTrans, capturedLineIds, boundaryPoints, new CropGeometryService());
                        var uid = ServiceACAD.TestRecorder.Record(record);
                        ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                        ed.WriteMessage(
                            $"\n{commandName} 完成 ({direction}): 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个");
                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"ExecuteCropLine 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"直线裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPLINE 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPLINE 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     询问裁剪方向：裁剪内部-保留外部，还是裁剪外部-保留内部.
        /// </summary>
        /// <returns>true=裁剪外部（保留内部），false=裁剪内部（保留外部），null=取消.</returns>
        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions(
                    "\n请选择裁剪方向 [裁剪内部-保留外部(I)/裁剪外部-保留内部(O)]: ", "裁剪内部 裁剪外部");
                options.Keywords.Add("裁剪内部", "裁剪内部-保留外部(I)", "裁剪掉边界内部的实体，保留外部");
                options.Keywords.Add("裁剪外部", "裁剪外部-保留内部(O)", "裁剪掉边界外部的实体，保留内部");
                options.Keywords.Default = "裁剪外部";
                options.AllowNone = true;

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                {
                    ed.WriteMessage("\n取消裁剪方向选择。");
                    return null;
                }

                // 裁剪内部 = 保留外部 = keepInside = false
                // 裁剪外部 = 保留内部 = keepInside = true
                if (result.StringResult == "裁剪内部")
                    return false;
                if (result.StringResult == "裁剪外部")
                    return true;

                // 默认 = 裁剪外部（保留内部）
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
        private List<DDNCadAddins.Core.Models.Point2D> SelectSingleBoundaryCurve(Editor ed, out ObjectId boundaryId)
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
                    boundaryId = ObjectId.Null;
                    return null;
                }

                boundaryId = promptResult.ObjectId;
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
                boundaryId = ObjectId.Null;
                return null;
            }
        }

        /// <summary>
        ///     选择要裁剪的直线.
        /// </summary>
        private List<ObjectId> SelectLinesToCrop(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要裁剪的直线: ",
                    AllowDuplicates = false
                };

                var promptResult = ed.GetSelection(options, new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE"),
                }));

                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择直线或选择被取消。");
                    return null;
                }

                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in promptResult.Value)
                {
                    ids.Add(selObj.ObjectId);
                }

                ed.WriteMessage($"\n已选择 {ids.Count} 条直线。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择待裁剪直线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择待裁剪直线失败: {ex.Message}");
                return null;
            }
        }
    }
}