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
    ///     同时通过 TestRecorder 记录源/目标 Hatch 的完整信息到 TestRecords/ 目录.
    /// </summary>
    public class CloneHatchCommand
    {
        public struct HatchParams
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

        /// <summary>
        ///     提取源 Hatch 的填充参数.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="hatchId">源 Hatch 的 ObjectId.</param>
        /// <returns>包含 HatchParams 的操作结果.</returns>
        public static ServiceACAD.OpResult<HatchParams> ExtractHatchParams(ObjectId hatchId)
        {
            var result = new HatchParams();
            var gotParams = false;
            CadServiceManager._.ExecuteInTransactions("", ts =>
            {
                var src = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                if (src == null || src.IsErased) return;

                result.PatternType   = src.PatternType;
                result.PatternName   = src.PatternName;
                result.PatternScale  = src.PatternScale;
                result.PatternAngle  = src.PatternAngle;
                result.PatternDouble = src.PatternDouble;
                result.PatternSpace  = src.PatternSpace;
                result.Origin        = src.Origin;
                result.Style         = src.HatchStyle;
                result.Normal        = src.Normal;
                result.Elevation     = src.Elevation;
                gotParams            = true;
            });

            if (!gotParams)
                return ServiceACAD.OpResult<HatchParams>.Fail("源 Hatch 无效或已被删除。");
            return ServiceACAD.OpResult<HatchParams>.Success(result);
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

                // Step 2: 提取源 Hatch 参数（调用核心方法）
                var extractResult = ExtractHatchParams(sourceId);
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
                        var created = CloneHatchWithNewBoundaries(ts, p, boundaryIds, out newHatchId);
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

        /// <summary>
        ///     重新生成 Hatch（不深克隆），应用源 Hatch 的填充参数 + 新边界。
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="p">源 Hatch 提取的填充参数.</param>
        /// <param name="boundaryIds">新边界对象的 ObjectId 数组.</param>
        /// <param name="newHatchId">[out] 新创建的 Hatch 的 ObjectId.</param>
        /// <returns>是否成功创建填充.</returns>
        public static bool CloneHatchWithNewBoundaries(
            ITransactionService ts, HatchParams p,
            ObjectId[] boundaryIds, out ObjectId newHatchId)
        {
            newHatchId = ObjectId.Null;

            try
            {
                // 1. 创建新 Hatch，应用源 Hatch 的填充参数（图案名称/比例/角度/原点等）
                //    注意：HatchStyle 统一使用 Normal，而非源 Hatch 的原始 Style。
                //    原因：Outer/Ignore 样式会导致 AutoCAD 在 EvaluateHatch 时
                //    重新自行判断环的内外关系，覆盖我们手动设置的 HatchLoopTypes。
                //    使用 Normal 后，AutoCAD 尊重我们设置的 Outermost/Default 环类型，
                //    按 depth 交替填充，效果与原始 Outer/Ignore 等价（因为
                //    ProcessHatches 已按 HatchStyle 过滤了相应 depth 的环）。
                var hatch = new Hatch();
                hatch.SetHatchPattern(p.PatternType, p.PatternName);
                hatch.PatternScale  = p.PatternScale;
                hatch.PatternAngle  = p.PatternAngle;
                hatch.PatternDouble = p.PatternDouble;
                hatch.PatternSpace  = p.PatternSpace;
                hatch.Origin        = p.Origin;
                hatch.HatchStyle    = HatchStyle.Normal;
                hatch.Normal        = p.Normal;
                hatch.Elevation     = p.Elevation;

                // 2. 加入数据库
                if (ts.AppendEntityToCurrentSpace(hatch).IsNull)
                {
                    Logger._.Warn("CloneHatch: 无法将填充加入数据库。");
                    hatch.Dispose();
                    return false;
                }

                newHatchId = hatch.ObjectId;

                // 3. 追加边界环
                //    优先尝试关联方式（传 ObjectIdCollection），失败则回退到顶点方式。
                //    注意：Associative = true 必须在 AppendLoop(ObjectIdCollection) 之后设置，
                //    否则 AutoCAD 引擎无法建立边界关联，EvaluateHatch 时填充不完整。
                //    第1个环 = Outermost（外环），第2个环 = Default（内环/孔洞）.
                //    调用方（ProcessHatches）已按面积降序排序并按 HatchStyle 截取相应数量.
                var appended = 0;
                for (int i = 0; i < boundaryIds.Length; i++)
                {
                    var id = boundaryIds[i];
                    if (id.IsNull) continue;
                    var ent = ts.GetObject<Entity>(id, OpenMode.ForRead);
                    if (!(ent is Curve curve)) continue;

                    var loopType = (i == 0) ? HatchLoopTypes.Outermost : HatchLoopTypes.Default;
                    try
                    {
                        // 关联方式：用 ObjectIdCollection 追加环，AutoCAD 自动读取几何
                        var idCol = new ObjectIdCollection { id };
                        hatch.AppendLoop(loopType, idCol);
                        appended++;
                    }
                    catch (System.Exception)
                    {
                        // 回退：用顶点方式追加（非关联，但至少保证几何正确）
                        try
                        {
                            var pts = new Point2dCollection();
                            var bulges = new DoubleCollection();
                            if (!ExtractCurveGeometry(curve, pts, bulges)) continue;
                            hatch.AppendLoop(loopType, pts, bulges);
                            appended++;
                        }
                        catch (System.Exception ex2)
                        {
                            Logger._.Warn($"边界 {id} 追加失败: {ex2.Message}");
                        }
                    }
                }

                // 关联性必须在所有 AppendLoop(ObjectIdCollection) 完成后设置
                if (appended > 0)
                    hatch.Associative = true;

                if (appended == 0)
                {
                    Logger._.Warn("CloneHatch: 所选对象均不是有效的闭合边界。");
                    hatch.Erase();
                    newHatchId = ObjectId.Null;
                    return false;
                }

                // 4. 评估填充
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
