using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropTestsCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     裁剪测试命令 — 集合所有 CROP 命令的批量测试入口.
    ///     选择一个裁剪边界，然后对图纸中所有可裁剪实体统一执行裁剪操作，
    ///     汇总处理结果并记录 TestRecords.
    ///     通过 MANUALCMDTESTS → CROPTESTS 或直接在 AutoCAD 执行 CROPTESTS 调用.
    /// </summary>
    public class CropTestsCommand
    {
        /// <summary>
        ///     执行 CROPTESTS 命令.
        /// </summary>
        [CommandMethod("CROPTESTS")]
        public void Execute()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application
                    .DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                // 1. 选择裁剪边界
                var boundaryPoints = this.SelectSingleBoundaryCurve(ed, out var boundaryId);
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return;

                // 2. 询问裁剪方向
                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue)
                    return;

                bool captureInside = keepInside.Value;
                string directionLabel = captureInside ? "内部" : "外部";

                ServiceACAD.TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);

                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   CROPTESTS 开始 — 裁剪方向: {directionLabel}");
                ed.WriteMessage($"\n═══════════════════════════════════════════");

                // 3. 收集所有可裁剪实体（排除边界自身）
                var allEntities = new List<ObjectId>();

                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    void Collect<T>() where T : Entity
                    {
                        var ids = serviceTrans.GetChildObjectsFromModelspace<T>();
                        if (ids != null)
                        {
                            ids.RemoveAll(id => id == boundaryId);
                            allEntities.AddRange(ids);
                        }
                    }

                    Collect<Line>();
                    Collect<Arc>();
                    Collect<Circle>();
                    Collect<Polyline>();
                    Collect<Polyline2d>();
                    Collect<Spline>();
                    Collect<Ellipse>();
                    Collect<DBText>();
                    Collect<MText>();
                    Collect<Dimension>();
                    Collect<Hatch>();
                    Collect<BlockReference>();
                    Collect<DBPoint>();
                    Collect<Solid>();
                    Collect<Leader>();
                    Collect<Polyline3d>();
                    Collect<Mline>();
                });

                if (allEntities.Count == 0)
                {
                    ed.WriteMessage("\n图纸中未找到任何可裁剪的实体。");
                    return;
                }

                ed.WriteMessage($"\n找到 {allEntities.Count} 个待处理实体，正在批量裁剪...\n");

                // 4. 使用统一的 CropService 执行裁剪
                var geoService = new CropGeometryService();
                var cropService = new CropService(geoService);

                var input = new CropInput
                {
                    BoundaryPoints = boundaryPoints.AsReadOnly(),
                    EntityIds = allEntities,
                    TransactionService = null, // 将在事务内赋值
                };

                CropResult cropResult = null;
                bool cropSuccess = false;
                string errorMsg = null;

                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        // ★ 裁剪前采集实体几何快照（避免裁剪擦除后无法获取几何）
                        List<CropEntitySnapshot> snapshots = null;
                        try
                        {
                            snapshots = ServiceACAD.TestRecorder.CollectSnapshots(
                                serviceTrans, allEntities, boundaryPoints, geoService);
                        }
                        catch (System.Exception snapEx)
                        {
                            Logger._.Warn($"采集快照失败: {snapEx.Message}");
                        }

                        input.TransactionService = serviceTrans;
                        var result = captureInside
                            ? cropService.CropInside(input)
                            : cropService.CropOutside(input);

                        if (!result.IsSuccess)
                        {
                            errorMsg = result.Message;
                            ed.WriteMessage($"\n裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }

                        cropResult = result.Data;
                        cropSuccess = true;

                        // 记录 TestRecord
                        try
                        {
                            var record = new CropTestRecord
                            {
                                Command = "CROPTESTS",
                                Direction = directionLabel,
                                IsSuccess = true,
                                UcsOrigin = ucsOrigin,
                                UcsXAxis = ucsX,
                                UcsYAxis = ucsY,
                                BoundaryVertices = boundaryPoints,
                                BoundaryVertexCount = boundaryPoints.Count,
                                TotalEntityCount = allEntities.Count,
                                DeletedCount = cropResult.DeletedCount,
                                SplitCount = cropResult.SplitCount,
                                KeptCount = cropResult.KeptCount,
                                SkippedCount = cropResult.SkippedCount,
                                Entities = snapshots,
                            };
                            var uid = ServiceACAD.TestRecorder.Record(record);
                            ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                        }
                        catch (System.Exception recEx)
                        {
                            Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                        }

                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        errorMsg = ex.Message;
                        Logger._.Error($"裁剪执行失败: {ex.Message}", ex);
                        ed.WriteMessage($"\n裁剪执行失败: {ex.Message}");
                        return ServiceACAD.OpResult.Fail(ex.Message);
                    }
                });

                // 5. 输出结果
                if (cropSuccess && cropResult != null)
                {
                    ed.WriteMessage($"\n{'─',50}");
                    ed.WriteMessage($"\n  CROPTESTS 汇总:");
                    ed.WriteMessage($"\n{'─',50}");
                    ed.WriteMessage($"\n  总实体: {allEntities.Count,6}");
                    ed.WriteMessage($"\n  删除:   {cropResult.DeletedCount,6}");
                    ed.WriteMessage($"\n  拆分:   {cropResult.SplitCount,6}");
                    ed.WriteMessage($"\n  保留:   {cropResult.KeptCount,6}");
                    ed.WriteMessage($"\n  跳过:   {cropResult.SkippedCount,6}");
                    ed.WriteMessage($"\n{'─',50}");
                }
                else
                {
                    ed.WriteMessage($"\n裁剪未完成: {errorMsg ?? "未知错误"}");
                }

                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   CROPTESTS 完成");
                ed.WriteMessage($"\n═══════════════════════════════════════════");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPTESTS 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPTESTS 命令失败: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助方法
        // ════════════════════════════════════════════════════════════════

        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions("\n请选择裁剪方向 [内部(N)/外部(W)]: ", "内部 外部");
                options.Keywords.Add("内部", "内部(N)", "保留边界内部的实体");
                options.Keywords.Add("外部", "外部(W)", "保留边界外部的实体");
                options.Keywords.Default = "内部";
                options.AllowNone = true;

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                {
                    ed.WriteMessage("\n取消裁剪方向选择。");
                    return null;
                }
                return result.StringResult == "内部" || string.IsNullOrEmpty(result.StringResult);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"询问裁剪方向失败: {ex.Message}", ex);
                ed.WriteMessage($"\n询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }

        private List<CorePoint2D> SelectSingleBoundaryCurve(Editor ed, out ObjectId boundaryId)
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
                var points = new List<CorePoint2D>();

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
                        points.Add(new CorePoint2D(pt.X, pt.Y));
                    }

                    var deduped = new List<CorePoint2D>();
                    foreach (var p in points)
                    {
                        if (deduped.Count == 0) { deduped.Add(p); continue; }
                        var last = deduped[deduped.Count - 1];
                        if (Math.Abs(last.X - p.X) > 1e-6 || Math.Abs(last.Y - p.Y) > 1e-6)
                            deduped.Add(p);
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
    }
}