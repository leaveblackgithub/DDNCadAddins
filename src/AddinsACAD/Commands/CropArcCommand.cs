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

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropArcCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     圆弧裁剪命令 - 专门处理 Arc 类型的裁剪.
    ///     选择一条闭合曲线作为裁剪边界，然后选择要裁剪的圆弧（或自动选择所有圆弧）.
    ///     支持选择保留边界内部或外部的圆弧.
    /// </summary>
    public class CropArcCommand
    {
        [CommandMethod("CROPARC")]
        public void Execute()
        {
            this.ExecuteCropArc(selectAllArcs: false);
        }

        [CommandMethod("CROPALLARCS")]
        public void ExecuteAll()
        {
            this.ExecuteCropArc(selectAllArcs: true);
        }

        private void ExecuteCropArc(bool selectAllArcs)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                var boundaryPoints = this.SelectSingleBoundaryCurve(ed, out var boundaryId);
                if (boundaryPoints == null || boundaryPoints.Count < 3) return;

                List<ObjectId> arcIds = null;
                if (selectAllArcs)
                {
                    ed.WriteMessage("\n正在自动选择图纸中所有圆弧...");
                    List<ObjectId> autoArcIds = null;
                    CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                    {
                        autoArcIds = serviceTrans.GetChildObjectsFromModelspace<Arc>();
                    });
                    if (autoArcIds == null || autoArcIds.Count == 0)
                    {
                        ed.WriteMessage("\n图纸中没有找到任何圆弧。");
                        return;
                    }
                    // 排除边界自身
                    autoArcIds.RemoveAll(id => id == boundaryId);
                    if (autoArcIds.Count == 0) { ed.WriteMessage("\n排除边界后没有其他圆弧。"); return; }
                    ed.WriteMessage($"\n已排除边界圆弧，剩余 {autoArcIds.Count} 条。");
                    arcIds = autoArcIds;
                    ed.WriteMessage($"\n已自动选择 {arcIds.Count} 条圆弧。");
                }
                else
                {
                    arcIds = this.SelectArcsToCrop(ed);
                    if (arcIds == null || arcIds.Count == 0) return;
                    // 手动选择也排除边界自身
                    arcIds.RemoveAll(id => id == boundaryId);
                }

                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue) return;

                // ── 采集 UCS ──
                ServiceACAD.TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);
                var capturedUcsOrigin = ucsOrigin;
                var capturedUcsX = ucsX;
                var capturedUcsY = ucsY;
                var capturedBoundaryVerts = boundaryPoints;
                var capturedArcIds = arcIds;
                var capturedKeepInside = keepInside.Value;
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var cropArcService = new CropArcService(new CropGeometryService());
                        var result = capturedKeepInside
                            ? cropArcService.CropArcsInside(boundaryPoints, capturedArcIds, serviceTrans)
                            : cropArcService.CropArcsOutside(boundaryPoints, capturedArcIds, serviceTrans);
                        if (!result.IsSuccess)
                        {
                            ed.WriteMessage($"\n圆弧裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }
                        var cropResult = result.Data;
                        string commandName = selectAllArcs ? "CROPALLARCS" : "CROPARC";
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
                            TotalEntityCount = capturedArcIds.Count,
                            DeletedCount = cropResult.DeletedCount,
                            SplitCount = cropResult.SplitCount,
                            KeptCount = cropResult.KeptCount,
                            SkippedCount = cropResult.SkippedCount,
                        };
                        record.Entities = ServiceACAD.TestRecorder.CollectSnapshots(
                            serviceTrans, capturedArcIds, boundaryPoints, new CropGeometryService());
                        var uid = ServiceACAD.TestRecorder.Record(record);
                        ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                        ed.WriteMessage($"\n{commandName} 完成 ({direction}): 删除 {cropResult.DeletedCount} 个, 拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个");
                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"ExecuteCropArc 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"圆弧裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPARC 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPARC 命令失败: {ex.Message}");
            }
        }

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
                if (result.StringResult == "裁剪内部") return false;
                if (result.StringResult == "裁剪外部") return true;
                return true;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"询问裁剪方向失败: {ex.Message}", ex);
                ed.WriteMessage($"\n询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }

        private List<DDNCadAddins.Core.Models.Point2D> SelectSingleBoundaryCurve(Editor ed, out ObjectId boundaryId)
        {
            try
            {
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
                    if (curve == null || !curve.Closed)
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
                        if (deduped.Count == 0) { deduped.Add(p); continue; }
                        var last = deduped[deduped.Count - 1];
                        var dx = Math.Abs(last.X - p.X);
                        var dy = Math.Abs(last.Y - p.Y);
                        if (dx > 1e-6 || dy > 1e-6) deduped.Add(p);
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

        private List<ObjectId> SelectArcsToCrop(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要裁剪的圆弧: ",
                    AllowDuplicates = false
                };
                var promptResult = ed.GetSelection(options, new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "ARC"),
                }));
                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择圆弧或选择被取消。");
                    return null;
                }
                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in promptResult.Value)
                    ids.Add(selObj.ObjectId);
                ed.WriteMessage($"\n已选择 {ids.Count} 条圆弧。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择待裁剪圆弧失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择待裁剪圆弧失败: {ex.Message}");
                return null;
            }
        }
    }
}