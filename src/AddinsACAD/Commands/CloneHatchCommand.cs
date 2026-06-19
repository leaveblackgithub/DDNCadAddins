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
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CloneHatchCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CLONEHATCH 命令 — 提取源 Hatch 的填充参数（图案 / 比例 / 原点 / 角度），
    ///     然后让用户选取新的边界对象，用相同的参数对新边界进行填充.
    ///     交互流程：选择源 Hatch → 输出填充参数 → 选取新边界 → 用相同参数填充.
    ///     同时通过 TestRecorder 记录源/目标 Hatch 的完整信息到 TestRecords/ 目录.
    /// </summary>
    public class CloneHatchCommand
    {
        private struct HatchParams
        {
            public HatchPatternType PatternType;
            public string PatternName;
            public double PatternScale;
            public double PatternAngle;
            public bool PatternDouble;
            public double PatternSpace;
            public Point2d Origin;
            public HatchStyle Style;
            public Vector3d Normal;
            public double Elevation;
        }

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

                // Step 2: 在只读事务中提取并输出参数
                var p = new HatchParams();
                var gotParams = false;
                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var src = ts.GetObject<Hatch>(sourceId, OpenMode.ForRead);
                    if (src == null || src.IsErased)
                    {
                        ed.WriteMessage("\n源 Hatch 无效或已被删除。");
                        return;
                    }

                    p.PatternType   = src.PatternType;
                    p.PatternName   = src.PatternName;
                    p.PatternScale  = src.PatternScale;
                    p.PatternAngle  = src.PatternAngle;
                    p.PatternDouble = src.PatternDouble;
                    p.PatternSpace  = src.PatternSpace;
                    p.Origin        = src.Origin;
                    p.Style         = src.HatchStyle;
                    p.Normal        = src.Normal;
                    p.Elevation     = src.Elevation;
                    gotParams       = true;

                    ed.WriteMessage(
                        $"\n源 Hatch 参数：\n" +
                        $"  PATTERN  = {src.PatternName} ({src.PatternType})\n" +
                        $"  比例     = {src.PatternScale}\n" +
                        $"  原点     = ({src.Origin.X:F4}, {src.Origin.Y:F4})\n" +
                        $"  角度     = {src.PatternAngle:F6} rad ({src.PatternAngle * 180.0 / Math.PI:F2}°)\n" +
                        $"  双向填充 = {src.PatternDouble}\n" +
                        $"  间距     = {src.PatternSpace}");
                });

                if (!gotParams)
                    return;

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
                        // 4a: 深克隆源 Hatch（保留内部图案线条偏移数据），替换边界为新边界
                        var created = CloneHatchWithNewBoundaries(ts, sourceId, p, boundaryIds, ed, out newHatchId);
                        if (created)
                            ed.WriteMessage(
                                $"\n已用源参数填充新边界：PATTERN={p.PatternName}, 比例={p.PatternScale}, " +
                                $"角度={p.PatternAngle * 180.0 / Math.PI:F2}°。");

                        isSuccess = created;

                        // 4b: 采集源 Hatch + 新创建 Hatch 的几何快照（同一事务内）
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

        /// <summary>
        ///     深克隆源 Hatch（保留内部图案线条偏移数据），替换边界为新选对象.
        ///     这是解决 SetHatchPattern 重新加载工厂默认图案导致线条偏移不同的根本方案.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="sourceId">源 Hatch 的 ObjectId.</param>
        /// <param name="p">源 Hatch 提取的填充参数.</param>
        /// <param name="boundaryIds">新边界对象的 ObjectId 数组.</param>
        /// <param name="ed">编辑器（用于输出提示）.</param>
        /// <param name="newHatchId">[out] 新创建的 Hatch 的 ObjectId.</param>
        /// <returns>是否成功创建填充.</returns>
        private static bool CloneHatchWithNewBoundaries(
            ITransactionService ts, ObjectId sourceId, HatchParams p,
            ObjectId[] boundaryIds, Editor ed, out ObjectId newHatchId)
        {
            newHatchId = ObjectId.Null;

            try
            {
                // 1. 用 Hatch.Clone() 深克隆源 Hatch（保留所有内部图案线条偏移数据）
                var source = ts.GetObject<Hatch>(sourceId, OpenMode.ForRead);
                if (source == null || source.IsErased) return false;

                var hatch = (Hatch)source.Clone();

                // 2. 加入数据库
                if (ts.AppendEntityToCurrentSpace(hatch).IsNull)
                {
                    ed.WriteMessage("\n无法将填充加入数据库。");
                    hatch.Dispose();
                    return false;
                }

                newHatchId = hatch.ObjectId;

                // 3. 移除克隆 Hatch 的所有旧边界环（从最后一个开始往前删，避免索引偏移）
                int numLoops = hatch.NumberOfLoops;
                for (int i = numLoops - 1; i >= 0; i--)
                {
                    hatch.RemoveLoopAt(i);
                }

                // 4. 用边界实体的几何顶点追加新环（而非 ObjectId 引用，避免形状错误）
                hatch.Associative = true;
                hatch.HatchStyle  = p.Style;

                var appended = 0;
                foreach (var id in boundaryIds)
                {
                    if (id.IsNull) continue;
                    var ent = ts.GetObject<Entity>(id, OpenMode.ForRead);
                    if (!(ent is Curve curve)) continue;

                    try
                    {
                        var pts = new Point2dCollection();
                        var bulges = new DoubleCollection();
                        if (!ExtractCurveGeometry(curve, pts, bulges)) continue;

                        hatch.AppendLoop(HatchLoopTypes.Outermost, pts, bulges);
                        appended++;
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"边界 {id} 追加失败: {ex.Message}");
                    }
                }

                if (appended == 0)
                {
                    ed.WriteMessage("\n所选对象均不是有效的闭合边界。");
                    hatch.Erase();
                    newHatchId = ObjectId.Null;
                    return false;
                }

                hatch.EvaluateHatch(true);
                return true;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CloneHatchWithNewBoundaries 失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        ///     从 Curve 提取完整的几何顶点和凸度，构建 Point2dCollection 和 DoubleCollection.
        ///     Polyline — 逐顶点+凸度；Circle — 2个半圆顶点+凸度；Arc — 2个端点+凸度.
        /// </summary>
        private static bool ExtractCurveGeometry(Curve curve, Point2dCollection pts, DoubleCollection bulges)
        {
            if (curve is Polyline pl)
            {
                int n = pl.NumberOfVertices;
                for (int i = 0; i < n; i++)
                {
                    pts.Add(pl.GetPoint2dAt(i));
                    bulges.Add(pl.GetBulgeAt(i));
                }
                return n >= 3;
            }
            if (curve is Circle c)
            {
                // 用两个半圆表示圆
                var cx = c.Center.X; var cy = c.Center.Y; var r = c.Radius;
                pts.Add(new Point2d(cx - r, cy));
                bulges.Add(1.0);
                pts.Add(new Point2d(cx + r, cy));
                bulges.Add(1.0);
                return true;
            }
            if (curve is Arc a)
            {
                // 圆弧直接用2个端点+凸度
                pts.Add(new Point2d(a.StartPoint.X, a.StartPoint.Y));
                var theta = a.EndAngle - a.StartAngle;
                var bulge = Math.Tan(theta / 4.0);
                bulges.Add(bulge);
                pts.Add(new Point2d(a.EndPoint.X, a.EndPoint.Y));
                bulges.Add(0.0);
                return true;
            }
            return false;
        }
    }
}
