using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using ServiceACAD;
using CoreOpResult = DDNCadAddins.Core.Models.OpResult;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CloneHatchCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CLONEHATCH 命令 — 提取源 Hatch 的填充参数（图案 / 比例 / 原点 / 角度），
    ///     然后让用户选取新的边界对象，用相同的参数对新边界进行填充.
    ///     交互流程：选择源 Hatch → 输出填充参数 → 选取新边界 → 用相同参数填充.
    ///     核心逻辑委托给 <see cref="HatchCloneService"/> 实现.
    /// </summary>
    public class CloneHatchCommand
    {
        [CommandMethod("CLONEHATCH")]
        public void Execute()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var stopwatch = Stopwatch.StartNew();
            string uid = null;
            var isSuccess = false;

            try
            {
                TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);

                // Step 1: 选择源 Hatch
                var peo = new PromptEntityOptions("\n选择源 Hatch（提取填充参数）: ");
                peo.SetRejectMessage("\n请选择一个 Hatch 实体。");
                peo.AddAllowedClass(typeof(Hatch), false);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消选择。");
                    return;
                }

                var sourceId = per.ObjectId;

                // Step 2: 提取源 Hatch 参数（委托到 HatchCloneService）
                var extractResult = HatchCloneService.ExtractHatchParams(sourceId);
                if (!extractResult.IsSuccess)
                {
                    ed.WriteMessage($"\n{extractResult.Message}");
                    return;
                }

                var p = extractResult.Data;
                ed.WriteMessage(
                    $"\n源 Hatch 参数：\n" +
                    $"  PATTERN  = {p.PatternName} ({p.PatternType})\n" +
                    $"  比例     = {p.PatternScale}\n" +
                    $"  原点     = ({p.Origin.X:F4}, {p.Origin.Y:F4})\n" +
                    $"  角度     = {p.PatternAngle:F6} rad ({p.PatternAngle * 180.0 / Math.PI:F2}°)\n" +
                    $"  双向填充 = {p.PatternDouble}\n" +
                    $"  间距     = {p.PatternSpace}");

                // Step 3: 选取新边界对象（可多选）
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选取新边界对象（闭合曲线）: "
                };
                var ssr = ed.GetSelection(pso);
                if (ssr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选取边界，取消。");
                    return;
                }

                var boundaryIds = ssr.Value.GetObjectIds();
                if (boundaryIds == null || boundaryIds.Length == 0)
                {
                    ed.WriteMessage("\n未选取边界，取消。");
                    return;
                }

                // Step 4: 用相同参数填充新边界 + 记录 TestRecord
                ObjectId newHatchId = ObjectId.Null;
                CropTestRecord record = null;
                CadServiceManager._.ExecuteInCommandTransaction(ts =>
                {
                    try
                    {
                        var created = HatchCloneService.CloneHatchWithNewBoundaries(
                            ts, p, boundaryIds, out newHatchId);
                        if (created)
                            ed.WriteMessage(
                                $"\n已用源参数填充新边界：PATTERN={p.PatternName}, 比例={p.PatternScale}, " +
                                $"角度={p.PatternAngle * 180.0 / Math.PI:F2}°。");

                        isSuccess = created;

                        record = new CropTestRecord
                        {
                            Command   = "CLONEHATCH",
                            Direction = "Clone",
                            UcsOrigin = ucsOrigin,
                            UcsXAxis  = ucsX,
                            UcsYAxis  = ucsY,
                            IsSuccess = created,
                            ElapsedMs = stopwatch.ElapsedMilliseconds,
                        };

                        var entityIds = new List<ObjectId>();
                        if (!sourceId.IsNull) entityIds.Add(sourceId);
                        if (!newHatchId.IsNull) entityIds.Add(newHatchId);

                        if (entityIds.Count > 0)
                        {
                            record.Entities = TestRecorder.CollectSnapshots(
                                ts, entityIds, null, null);
                            record.TotalEntityCount = record.Entities?.Count ?? 0;
                        }

                        if (created)
                            return ServiceACAD.OpResult.Success();

                        ed.WriteMessage("\n未能创建填充。");
                        return ServiceACAD.OpResult.Fail("未能创建填充");
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"CLONEHATCH 填充失败: {ex.Message}", ex);
                        ed.WriteMessage($"\n填充失败: {ex.Message}");
                        return ServiceACAD.OpResult.Fail(ex.Message);
                    }
                });

                // Step 5: 写入 TestRecord（事务外写入文件）
                if (record != null)
                {
                    try
                    {
                        uid = TestRecorder.Record(record);
                        ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                    }
                    catch (System.Exception recEx)
                    {
                        Logger._.Warn($"CloneHatch TestRecorder 记录失败: {recEx.Message}");
                        ed.WriteMessage($"\n[TestRecorder] 记录失败: {recEx.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CLONEHATCH 命令失败: {ex.Message}", ex);
                ed.WriteMessage($"\nCLONEHATCH 命令失败: {ex.Message}");
            }
        }
    }
}
