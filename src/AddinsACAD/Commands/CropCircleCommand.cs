using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Services;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropCircleCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     圆裁剪命令 - CROP 圆，无法拆分，按圆心位置保留或删除.
    /// </summary>
    public class CropCircleCommand
    {
        [CommandMethod("CROPCIRCLE")]
        public void Execute()
        {
            this.ExecuteCropCircle(selectAll: false);
        }

        [CommandMethod("CROPALLCIRCLES")]
        public void ExecuteAll()
        {
            this.ExecuteCropCircle(selectAll: true);
        }

        private void ExecuteCropCircle(bool selectAll)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                var boundaryPoints = this.SelectSingleBoundaryCurve(ed);
                if (boundaryPoints == null || boundaryPoints.Count < 3) return;

                List<ObjectId> ids;
                if (selectAll)
                {
                    ed.WriteMessage("\n正在自动选择图纸中所有圆...");
                    List<ObjectId> autoIds = null;
                    CadServiceManager._.ExecuteInTransactions(null, s => { autoIds = s.GetChildObjectsFromModelspace<Circle>(); });
                    if (autoIds == null || autoIds.Count == 0)
                    {
                        ed.WriteMessage("\n图纸中没有找到任何圆。");
                        return;
                    }
                    ids = autoIds;
                    ed.WriteMessage($"\n已自动选择 {ids.Count} 个圆。");
                }
                else
                {
                    ids = this.SelectCirclesToCrop(ed);
                    if (ids == null || ids.Count == 0) return;
                }

                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue) return;

                var capturedIds = ids;
                var capturedKeepInside = keepInside.Value;
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var srv = new CropCircleService(new CropGeometryService());
                        var op = capturedKeepInside ? srv.CropCirclesInside(boundaryPoints, capturedIds, serviceTrans)
                                                    : srv.CropCirclesOutside(boundaryPoints, capturedIds, serviceTrans);
                        if (!op.IsSuccess)
                        {
                            ed.WriteMessage($"\n圆裁剪失败: {op.Message}");
                            return ServiceACAD.OpResult.Fail(op.Message);
                        }
                        var r = op.Data;
                        ed.WriteMessage($"\n{(selectAll ? "CROPALLCIRCLES" : "CROPCIRCLE")} 完成 ({ (keepInside.Value ? "内部" : "外部") }): 删除 {r.DeletedCount} 个, 保留 {r.KeptCount} 个, 跳过 {r.SkippedCount} 个");
                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"ExecuteCropCircle 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"圆裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPCIRCLE 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPCIRCLE 命令失败: {ex.Message}");
            }
        }

        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions("\n请选择裁剪方向 [内部(N)/外部(W)]: ", "内部 外部");
                options.Keywords.Add("内部", "内部(N)", "保留边界内部的圆");
                options.Keywords.Add("外部", "外部(W)", "保留边界外部的圆");
                options.Keywords.Default = "内部";
                options.AllowNone = true;
                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                { ed.WriteMessage("\n取消裁剪方向选择。"); return null; }
                if (result.StringResult == "内部") return true;
                if (result.StringResult == "外部") return false;
                return true;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"询问裁剪方向失败: {ex.Message}", ex);
                ed.WriteMessage($"\n询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }

        private List<DDNCadAddins.Core.Models.Point2D> SelectSingleBoundaryCurve(Editor ed)
        {
            try
            {
                var options = new PromptEntityOptions("\n选择裁剪边界曲线（单选）: ");
                options.SetRejectMessage("\n请选择圆、椭圆、闭合多段线或闭合样条线作为裁剪边界。");
                options.AddAllowedClass(typeof(Curve), false);
                var promptResult = ed.GetEntity(options);
                if (promptResult.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择边界。"); return null; }
                var curveId = promptResult.ObjectId;
                var points = new List<DDNCadAddins.Core.Models.Point2D>();
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var curve = serviceTrans.GetObject<Curve>(curveId);
                    if (curve == null || !curve.Closed) { ed.WriteMessage("\n边界曲线未闭合。"); return; }
                    const int sampleCount = 64;
                    for (var i = 0; i < sampleCount; i++)
                    {
                        var param = curve.StartParam + (curve.EndParam - curve.StartParam) * i / sampleCount;
                        var pt = curve.GetPointAtParameter(Math.Min(param, curve.EndParam));
                        points.Add(new DDNCadAddins.Core.Models.Point2D(pt.X, pt.Y));
                    }
                });
                if (points.Count < 3) { ed.WriteMessage("\n边界顶点不足。"); return null; }
                return points;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择边界失败: {ex.Message}", ex);
                return null;
            }
        }

        private List<ObjectId> SelectCirclesToCrop(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions { MessageForAdding = "\n选择要裁剪的圆: " };
                var promptResult = ed.GetSelection(options, new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                }));
                if (promptResult.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择圆。"); return null; }
                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in promptResult.Value) ids.Add(selObj.ObjectId);
                ed.WriteMessage($"\n已选择 {ids.Count} 个圆。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择圆失败: {ex.Message}", ex);
                return null;
            }
        }
    }
}