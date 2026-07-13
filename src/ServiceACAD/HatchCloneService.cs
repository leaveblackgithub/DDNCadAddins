using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ServiceACAD
{
    /// <summary>
    ///     Hatch 克隆服务 — 提取 Hatch 填充参数并用新边界创建 Hatch.
    ///     从 <c>CloneHatchCommand</c> 提取的核心方法，无 UI 交互.
    /// </summary>
    public static class HatchCloneService
    {
        /// <summary>
        ///     Hatch 填充参数结构体.
        /// </summary>
        public struct HatchParams
        {
            /// <summary>图案类型.</summary>
            public HatchPatternType PatternType;

            /// <summary>图案名称.</summary>
            public string PatternName;

            /// <summary>图案比例.</summary>
            public double PatternScale;

            /// <summary>图案角度（弧度）.</summary>
            public double PatternAngle;

            /// <summary>是否双向填充.</summary>
            public bool PatternDouble;

            /// <summary>图案间距.</summary>
            public double PatternSpace;

            /// <summary>填充原点.</summary>
            public Point2d Origin;

            /// <summary>填充样式.</summary>
            public HatchStyle Style;

            /// <summary>法线方向.</summary>
            public Vector3d Normal;

            /// <summary>标高.</summary>
            public double Elevation;
        }

        /// <summary>
        ///     提取源 Hatch 的填充参数.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="hatchId">源 Hatch 的 ObjectId.</param>
        /// <returns>包含 HatchParams 的操作结果.</returns>
        public static OpResult<HatchParams> ExtractHatchParams(ObjectId hatchId)
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
                return OpResult<HatchParams>.Fail("源 Hatch 无效或已被删除。");
            return OpResult<HatchParams>.Success(result);
        }

        /// <summary>
        ///     重新生成 Hatch（不深克隆），应用源 Hatch 的填充参数 + 新边界.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        ///     <para>
        ///         使用源 Hatch 的 HatchStyle（Outer/Ignore/Normal）直接进行 EvaluateHatch，
        ///         AutoCAD 会根据 HatchStyle 自动处理环的填充规则：
        ///         Normal — 交替填充（fill→skip→fill→skip）；
        ///         Outer — 填充最外层后遇到内环停止；
        ///         Ignore — 填充所有环，忽略内环边界。
        ///     </para>
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="p">源 Hatch 提取的填充参数.</param>
        /// <param name="boundaryIds">新边界对象的 ObjectId 数组（已按 depth 升序排列）.</param>
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
                //    先用 HatchStyle.Normal 评估，让 AutoCAD 尊重手动设置的
                //    Outermost/Default 环类型。SortByContainmentHierarchy 已按源
                //    HatchStyle 过滤环数量（Ignore→1环, Outer→depth≤1, Normal→全部），
                //    Normal 的交替填充规则恰好等价于各 HatchStyle 的预期效果。
                //    评估后再设回源 HatchStyle，保留属性值供后续检查。
                var hatch = new Hatch();
                hatch.SetHatchPattern(p.PatternType, p.PatternName);
                hatch.PatternScale  = p.PatternScale;
                hatch.PatternAngle  = p.PatternAngle;
                hatch.PatternDouble = p.PatternDouble;
                hatch.PatternSpace  = p.PatternSpace;
                hatch.Origin        = p.Origin;
                hatch.HatchStyle    = HatchStyle.Normal; // ★ 先用 Normal 评估
                hatch.Normal        = p.Normal;
                hatch.Elevation     = p.Elevation;

                // 2. 加入数据库
                if (ts.AppendEntityToCurrentSpace(hatch).IsNull)
                {
                    Logger._.Warn("HatchCloneService: 无法将填充加入数据库。");
                    hatch.Dispose();
                    return false;
                }

                newHatchId = hatch.ObjectId;

                // 3. 追加边界环（顶点方式，非关联）
                //    使用顶点方式追加环，不关联外部边界曲线，这样裁剪完成后
                //    临时生成的边界曲线可以被安全删除。
                //    第1个环 = Outermost（外环），其余 = Default（内环/孔洞）.
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
                        var pts = new Point2dCollection();
                        var bulges = new DoubleCollection();
                        if (!ExtractCurveGeometry(curve, pts, bulges)) continue;
                        hatch.AppendLoop(loopType, pts, bulges);
                        appended++;
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"边界 {id} 追加失败: {ex.Message}");
                    }
                }

                // 非关联方式：不设置 Associative，边界曲线可安全删除
                hatch.Associative = false;

                if (appended == 0)
                {
                    Logger._.Warn("HatchCloneService: 所选对象均不是有效的闭合边界。");
                    hatch.Erase();
                    newHatchId = ObjectId.Null;
                    return false;
                }

                // 4. 评估填充（Normal 样式下 AutoCAD 尊重我们设置的 Outermost/Default 环类型）
                hatch.EvaluateHatch(true);

                // ★ 5. 评估后设回源 HatchStyle，保留属性值（不重新 Evaluate，填充已固化）
                if (p.Style != HatchStyle.Normal)
                {
                    hatch.UpgradeOpen();
                    hatch.HatchStyle = p.Style;
                }

                return true;
            }
            catch (Exception ex)
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
            try
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
            catch (Exception ex)
            {
                Logger._.Error($"ExtractCurveGeometry 失败: {ex.Message}", ex);
                return false;
            }
        }
    }
}
