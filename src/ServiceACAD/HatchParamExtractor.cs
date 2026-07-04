using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;

namespace ServiceACAD
{
    /// <summary>
    ///     Hatch 填充参数提取器 — 从 CLONEHATCH 命令提取的可复用工具.
    ///     提供 Hatch 参数提取和用参数创建新 Hatch 的功能.
    /// </summary>
    public class HatchParamExtractor
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
        ///     从 Hatch 实体提取填充参数.
        /// </summary>
        /// <param name="hatch">Hatch 实体（已打开）.</param>
        /// <returns>包含 HatchParams 的操作结果.</returns>
        public OpResult<HatchParams> Extract(Hatch hatch)
        {
            try
            {
                if (hatch == null || hatch.IsErased)
                {
                    return OpResult<HatchParams>.Fail("Hatch 实体无效或已被删除。");
                }

                var p = new HatchParams
                {
                    PatternType   = hatch.PatternType,
                    PatternName   = hatch.PatternName,
                    PatternScale  = hatch.PatternScale,
                    PatternAngle  = hatch.PatternAngle,
                    PatternDouble = hatch.PatternDouble,
                    PatternSpace  = hatch.PatternSpace,
                    Origin        = hatch.Origin,
                    Style         = hatch.HatchStyle,
                    Normal        = hatch.Normal,
                    Elevation     = hatch.Elevation,
                };

                return OpResult<HatchParams>.Success(p, "Hatch 参数提取成功。");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"提取 Hatch 参数失败: {ex.Message}", ex);
                return OpResult<HatchParams>.Fail($"提取 Hatch 参数失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     用源 Hatch 的填充参数 + 新边界创建 Hatch.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="p">源 Hatch 提取的填充参数.</param>
        /// <param name="boundaryIds">新边界对象的 ObjectId 数组.</param>
        /// <param name="ed">编辑器（用于输出提示，可为 null）.</param>
        /// <returns>包含新 Hatch 的 ObjectId 的操作结果.</returns>
        public OpResult<ObjectId> CreateHatchWithParams(
            ITransactionService ts, HatchParams p,
            ObjectId[] boundaryIds, Editor ed)
        {
            try
            {
                if (boundaryIds == null || boundaryIds.Length == 0)
                {
                    return OpResult<ObjectId>.Fail("边界对象列表为空。");
                }

                // 1. 创建新 Hatch，应用源 Hatch 的填充参数
                var hatch = new Hatch();
                hatch.SetHatchPattern(p.PatternType, p.PatternName);
                hatch.PatternScale  = p.PatternScale;
                hatch.PatternAngle  = p.PatternAngle;
                hatch.PatternDouble = p.PatternDouble;
                hatch.PatternSpace  = p.PatternSpace;
                hatch.Origin        = p.Origin;
                hatch.HatchStyle    = p.Style;
                hatch.Normal        = p.Normal;
                hatch.Elevation     = p.Elevation;

                // 2. 加入数据库
                if (ts.AppendEntityToCurrentSpace(hatch).IsNull)
                {
                    hatch.Dispose();
                    return OpResult<ObjectId>.Fail("无法将填充加入数据库。");
                }

                var newHatchId = hatch.ObjectId;

                // 3. 追加边界环
                var appended = 0;
                foreach (var id in boundaryIds)
                {
                    if (id.IsNull) continue;
                    var ent = ts.GetObject<Entity>(id, OpenMode.ForRead);
                    if (!(ent is Curve curve)) continue;

                    try
                    {
                        // 关联方式：用 ObjectIdCollection 追加环
                        var idCol = new ObjectIdCollection { id };
                        hatch.AppendLoop(HatchLoopTypes.Outermost, idCol);
                        appended++;
                    }
                    catch (System.Exception)
                    {
                        // 回退：用顶点方式追加
                        try
                        {
                            var pts = new Point2dCollection();
                            var bulges = new DoubleCollection();
                            if (!ExtractCurveGeometry(curve, pts, bulges)) continue;
                            hatch.AppendLoop(HatchLoopTypes.Outermost, pts, bulges);
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
                {
                    hatch.Associative = true;
                }

                if (appended == 0)
                {
                    ed?.WriteMessage("\n所选对象均不是有效的闭合边界。");
                    hatch.Erase();
                    return OpResult<ObjectId>.Fail("没有有效的闭合边界。");
                }

                // 4. 评估填充
                hatch.EvaluateHatch(true);
                return OpResult<ObjectId>.Success(newHatchId, "Hatch 创建成功。");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CreateHatchWithParams 失败: {ex.Message}", ex);
                return OpResult<ObjectId>.Fail($"创建 Hatch 失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     从 Curve 提取完整的几何顶点和凸度，构建 Point2dCollection 和 DoubleCollection.
        ///     Polyline — 逐顶点+凸度；Circle — 2个半圆顶点+凸度；Arc — 2个端点+凸度.
        /// </summary>
        /// <param name="curve">曲线对象.</param>
        /// <param name="pts">[out] 顶点集合.</param>
        /// <param name="bulges">[out] 凸度集合.</param>
        /// <returns>是否成功提取.</returns>
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
            catch (System.Exception ex)
            {
                Logger._.Error($"ExtractCurveGeometry 失败: {ex.Message}", ex);
                return false;
            }
        }
    }
}
