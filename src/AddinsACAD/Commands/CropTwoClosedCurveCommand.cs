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

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropTwoClosedCurveCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPTWOCLOSEDCURVE — 调试命令：选择两个闭合曲线（外环+内环）和一个裁剪边界，
    ///     同时裁剪两个环并绘制结果。用于诊断奇偶环局部重叠时的共边对齐问题。
    ///     交互流程：选择裁剪边界 B → 选择外环 A₁ → 选择内环 A₂ → 询问方向 → 裁剪.
    /// </summary>
    public class CropTwoClosedCurveCommand
    {
        [CommandMethod("CROPTWOCLOSEDCURVE")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var stopwatch = Stopwatch.StartNew();

                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                // ── 步骤 1: 选择裁剪边界曲线 B（Clip，单选）───────────────────
                var idB = SelectClosedCurve(ed, "B（裁剪边界）", 2); // 黄色
                if (idB.IsNull) return;
                var curveB = CropClosedCurveService.CreateCurveSelection(idB);
                if (curveB == null) { ed.WriteMessage("\n边界曲线 B 转换失败。"); return; }

                // ── 步骤 2: 选择外环 A₁（Subject 1，单选）───────────────────
                var idA1 = SelectClosedCurve(ed, "A₁（外环/Subject 1）", 1); // 红色
                if (idA1.IsNull) return;
                var curveA1 = CropClosedCurveService.CreateCurveSelection(idA1);
                if (curveA1 == null) { ed.WriteMessage("\n外环 A₁ 转换失败。"); return; }

                // ── 步骤 3: 选择内环 A₂（Subject 2，单选）───────────────────
                var idA2 = SelectClosedCurve(ed, "A₂（内环/Subject 2）", 4); // 青色
                if (idA2.IsNull) return;
                var curveA2 = CropClosedCurveService.CreateCurveSelection(idA2);
                if (curveA2 == null) { ed.WriteMessage("\n内环 A₂ 转换失败。"); return; }

                // ── 步骤 4: 询问裁剪方向 ─────────────────────────────────────
                bool? keepInside = CropUtils.AskCropDirection(ed);
                if (!keepInside.HasValue) return;

                string directionLabel = keepInside.Value ? "保留内部(交集)" : "保留外部(差集)";

                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   CROPTWOCLOSEDCURVE — {directionLabel}");
                ed.WriteMessage($"\n═══════════════════════════════════════════");

                // ── 步骤 5: 执行裁剪 ─────────────────────────────────────────
                //    A₁=外环，A₂=内环（孔洞），使用 CropRingWithHole 正确处理
                //    Clip 同时与内外环相交的凹字形场景（内环区域始终不属于结果）.
                var cropResult = CropClosedCurveService.CropRingWithHole(
                    curveA1, curveA2, curveB, keepInside.Value);

                // ── 步骤 6: 输出结果 ─────────────────────────────────────────
                if (cropResult.IsSuccess)
                {
                    ed.WriteMessage($"\n{'─',50}");
                    ed.WriteMessage($"\n  CROPTWOCLOSEDCURVE 汇总:");
                    ed.WriteMessage($"\n{'─',50}");
                    ed.WriteMessage($"\n  外环 A₁: type={curveA1.Type}, vertices={curveA1.Polygon?.Count ?? 0}");
                    ed.WriteMessage($"\n  内环 A₂: type={curveA2.Type}, vertices={curveA2.Polygon?.Count ?? 0}");
                    ed.WriteMessage($"\n  裁剪边界 B: type={curveB.Type}");
                    ed.WriteMessage($"\n  方向: {directionLabel}");
                    ed.WriteMessage($"\n  结果环数: {cropResult.PolyCount}");
                    ed.WriteMessage($"\n  总顶点: {cropResult.TotalVertices}");
                    ed.WriteMessage($"\n  信息: {cropResult.Message}");
                    if (cropResult.CreatedEntityIds != null)
                    {
                        ed.WriteMessage($"\n  创建实体 ID 数: {cropResult.CreatedEntityIds.Count}");
                        if (cropResult.CreatedEntityAreas != null)
                        {
                            for (int i = 0; i < cropResult.CreatedEntityAreas.Length; i++)
                                ed.WriteMessage($"\n    Loop[{i}]: Area={cropResult.CreatedEntityAreas[i]:F4}");
                        }
                    }
                    ed.WriteMessage($"\n{'─',50}");
                }
                else
                {
                    ed.WriteMessage($"\n  裁剪失败: {cropResult.Message}");
                }

                // ── 步骤 7: TestRecorder ─────────────────────────────────────
                try
                {
                    var record = new CropTestRecord
                    {
                        Command = "CROPTWOCLOSEDCURVE",
                        Direction = keepInside.Value ? "Inside" : "Outside",
                        IsSuccess = cropResult.IsSuccess,
                        UcsOrigin = ucsO,
                        UcsXAxis = ucsX,
                        UcsYAxis = ucsY,
                        TotalEntityCount = 3,
                        DeletedCount = 0,
                        KeptCount = cropResult.PolyCount,
                        SkippedCount = 0,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                    };
                    var uid = TestRecorder.Record(record);
                    ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                }
                catch (System.Exception recEx)
                {
                    Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                }

                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   CROPTWOCLOSEDCURVE 完成");
                ed.WriteMessage($"\n═══════════════════════════════════════════");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPTWOCLOSEDCURVE 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPTWOCLOSEDCURVE 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     选择一条闭合曲线，并将其实体颜色设置为指定颜色索引（用于调试可视化）.
        /// </summary>
        /// <param name="ed">编辑器.</param>
        /// <param name="label">提示标签（显示在交互消息中）.</param>
        /// <param name="colorIndex">AutoCAD 颜色索引（1=红, 2=黄, 3=绿, 4=青）.</param>
        /// <returns>选择的曲线 ObjectId；取消或无效返回 ObjectId.Null.</returns>
        private static ObjectId SelectClosedCurve(Editor ed, string label, int colorIndex)
        {
            try
            {
                var peo = new PromptEntityOptions($"\n选择 {label}（单选闭合曲线）: ");
                peo.SetRejectMessage($"\n请选择闭合曲线作为 {label}。");
                peo.AddAllowedClass(typeof(Curve), false);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    ed.WriteMessage($"\n取消选择 {label}。");
                    return ObjectId.Null;
                }

                var id = per.ObjectId;
                bool closed = false;
                CadServiceManager._.ExecuteInTransactions(null, ts =>
                {
                    var curve = ts.GetObject<Curve>(id, OpenMode.ForRead);
                    if (curve != null && curve.Closed)
                    {
                        closed = true;
                        curve.UpgradeOpen();
                        curve.ColorIndex = colorIndex;
                    }
                });

                if (!closed)
                {
                    ed.WriteMessage($"\n{label} 未闭合，请重新选择。");
                    return ObjectId.Null;
                }

                return id;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"SelectClosedCurve({label}) 失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }
    }
}
