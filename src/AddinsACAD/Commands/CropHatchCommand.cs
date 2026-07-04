using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropHatchCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPHATCH 命令 — 用户选择一个 Hatch 和一个裁剪边界（闭合曲线），
    ///     将 Hatch 的填充区域用裁剪边界进行布尔差集运算，生成裁剪后的新 Hatch 并删除原图.
    ///     严格复用 GENERATEHATCHBOUNDARY + SUBTRACTCLOSEDCURVE + CLONEHATCH 的核心方法.
    /// </summary>
    public class CropHatchCommand
    {
        /// <summary>
        ///     执行 CROPHATCH 命令.
        ///     交互流程：选择源 Hatch → 选择裁剪边界曲线 → 自动裁剪 → 输出结果.
        /// </summary>
        [CommandMethod("CROPHATCH")]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var stopwatch = Stopwatch.StartNew();
            string uid = null;
            var isSuccess = false;

            try
            {
                TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);

                // ── 输入获取 ──

                // Step 1: 选择源 Hatch
                var peoHatch = new PromptEntityOptions("\n选择要裁剪的 Hatch: ");
                peoHatch.SetRejectMessage("\n请选择一个 Hatch 实体。");
                peoHatch.AddAllowedClass(typeof(Hatch), false);

                var perHatch = ed.GetEntity(peoHatch);
                if (perHatch.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消选择。");
                    return;
                }

                var hatchId = perHatch.ObjectId;

                // Step 2: 选择裁剪边界曲线
                var peoBoundary = new PromptEntityOptions("\n选择裁剪边界（闭合曲线）: ");
                peoBoundary.SetRejectMessage("\n请选择闭合的 Polyline/Circle/Ellipse/Spline 作为裁剪边界。");
                peoBoundary.AddAllowedClass(typeof(Curve), false);

                var perBoundary = ed.GetEntity(peoBoundary);
                if (perBoundary.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消选择。");
                    return;
                }

                var boundaryCurveId = perBoundary.ObjectId;

                // Step 3: 询问是否删除原 Hatch
                var kwo = new PromptKeywordOptions(
                    "\n是否删除原 Hatch? [是(Y)/否(N)] ", "是 否");
                kwo.Keywords.Add("是");
                kwo.Keywords.Add("否");
                kwo.Keywords.Default = "是";
                kwo.AllowNone = true;

                var kwr = ed.GetKeywords(kwo);
                bool deleteOriginal = true;
                if (kwr.Status == PromptStatus.OK || kwr.Status == PromptStatus.Keyword)
                {
                    deleteOriginal = string.IsNullOrEmpty(kwr.StringResult)
                        || kwr.StringResult == "是";
                }

                // ── 主体逻辑（调用 CropHatchService）──
                ObjectId newHatchId = ObjectId.Null;
                CropHatchWithBoundaryResult cropResult = null;
                CropTestRecord record = null;

                CadServiceManager._.ExecuteInCommandTransaction(ts =>
                {
                    try
                    {
                        var cropService = new CropHatchService(null);
                        var result = cropService.CropHatchWithBoundary(
                            hatchId, boundaryCurveId, ts, ed,
                            out newHatchId, deleteOriginal);

                        if (result.IsSuccess)
                        {
                            cropResult = result.Data;
                            isSuccess = true;
                            ed.WriteMessage($"\n{cropResult.Message}");

                            // 构建 TestRecord
                            record = new CropTestRecord
                            {
                                Command = "CROPHATCH",
                                Direction = "Difference",
                                UcsOrigin = ucsOrigin,
                                UcsXAxis = ucsX,
                                UcsYAxis = ucsY,
                                IsSuccess = true,
                                ElapsedMs = stopwatch.ElapsedMilliseconds,
                                KeptCount = 1,
                                DeletedCount = deleteOriginal ? 1 : 0,
                            };

                            var entityIds = new List<ObjectId>();
                            if (!newHatchId.IsNull) entityIds.Add(newHatchId);
                            if (!deleteOriginal && !hatchId.IsNull)
                                entityIds.Add(hatchId);

                            if (entityIds.Count > 0)
                            {
                                record.Entities = TestRecorder.CollectSnapshots(
                                    ts, entityIds, null, null);
                                record.TotalEntityCount = record.Entities?.Count ?? 0;
                            }

                            return ServiceACAD.OpResult.Success();
                        }

                        ed.WriteMessage($"\n裁剪失败: {result.Message}");
                        return ServiceACAD.OpResult.Fail(result.Message);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"CROPHATCH 执行失败: {ex.Message}", ex);
                        ed.WriteMessage($"\nCROPHATCH 执行失败: {ex.Message}");
                        return ServiceACAD.OpResult.Fail(ex.Message);
                    }
                });

                // ── 输出显示 ──
                if (isSuccess)
                {
                    ed.WriteMessage($"\nCROPHATCH 完成：新 Hatch 已创建。");
                }
                else
                {
                    ed.WriteMessage($"\nCROPHATCH 未完成。");
                }

                // 写入 TestRecord（事务外写入文件）
                if (record != null)
                {
                    try
                    {
                        uid = TestRecorder.Record(record);
                        ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                    }
                    catch (System.Exception recEx)
                    {
                        Logger._.Warn($"CROPHATCH TestRecorder 记录失败: {recEx.Message}");
                        ed.WriteMessage($"\n[TestRecorder] 记录失败: {recEx.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPHATCH 命令失败: {ex.Message}", ex);
                ed.WriteMessage($"\nCROPHATCH 命令失败: {ex.Message}");
            }
        }
    }
}
