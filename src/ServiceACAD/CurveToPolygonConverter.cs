using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     曲线→多边形转换器 — 将各种 AutoCAD Curve 类型转换为多边形顶点或 CAD 实体.
    ///     <para>
    ///         内部自动选择精确/拟合策略：
    ///         - 精确（ExactCurveGenerator）：直线 / Polyline（凸度弧段精确展开）/ 圆弧 / 圆 / 完整椭圆
    ///         - 拟合（FittedCurveGenerator）：椭圆弧/Spline/3DPolyline/MLine/Leader 等
    ///     </para>
    /// </summary>
    public class CurveToPolygonConverter
    {
        private readonly ExactCurveGenerator _exactGen;
        private readonly FittedCurveGenerator _fittedGen;

        /// <summary>
        ///     默认构造函数.
        /// </summary>
        public CurveToPolygonConverter()
        {
            this._exactGen = new ExactCurveGenerator();
            this._fittedGen = new FittedCurveGenerator();
        }

        /// <summary>
        ///     构造函数（依赖注入，便于测试）.
        /// </summary>
        public CurveToPolygonConverter(ExactCurveGenerator exactGen, FittedCurveGenerator fittedGen)
        {
            this._exactGen = exactGen ?? new ExactCurveGenerator();
            this._fittedGen = fittedGen ?? new FittedCurveGenerator();
        }

        // ──────────────────────────────────────────────────────────────
        //  多边形顶点生成（纯数据，用于 Core 层计算）
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     将闭合 Curve 转换为多边形顶点列表 (WCS).
        ///     自动选择精确/拟合策略.
        /// </summary>
        /// <param name="curve">闭合曲线.</param>
        /// <returns>多边形顶点列表；转换失败返回 null.</returns>
        public List<CorePoint2D> ConvertCurveToPolygon(Curve curve)
        {
            if (curve == null || !curve.Closed)
            {
                return null;
            }

            try
            {
                if (curve is Polyline pl)
                {
                    return this.ConvertPolyline(pl);
                }

                if (curve is Circle circle)
                {
                    return this.ConvertCircle(circle);
                }

                if (curve is Ellipse ellipse)
                {
                    return this.ConvertEllipse(ellipse);
                }

                if (curve is Spline spline)
                {
                    return this.ConvertSpline(spline);
                }

                // 其他闭合曲线 → 均匀采样
                return this.ConvertGenericClosedCurve(curve);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     将 HatChLoop 转换为多边形顶点列表.
        /// </summary>
        public List<CorePoint2D> ConvertLoopToPolygon(HatchLoop loop)
        {
            var points = new List<CorePoint2D>();

            if (loop.Curves != null)
            {
                foreach (Curve2d curve in loop.Curves)
                {
                    this.AddCurve2dPoints(curve, points);
                }
            }

            // 闭合多边形
            var closed = ClosePolygon(points);

            // 去重
            return RemoveAdjacentDuplicates(closed);
        }

        // ──────────────────────────────────────────────────────────────
        //  CAD 实体生成（用于命令输出）
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     从 HatChLoop 创建最优的 CAD 实体（Polyline / Circle / Ellipse / Polyline2d）.
        ///     自动检测环的类型并选择最精确的表示方式.
        /// </summary>
        /// <param name="loop">Hatch 边界环.</param>
        /// <param name="plane">Hatch 所在平面.</param>
        /// <param name="colorIndex">颜色索引.</param>
        /// <param name="layer">图层.</param>
        /// <param name="ts">事务服务（用于 AddNewlyCreatedDBObject）.</param>
        /// <returns>创建的 ObjectId；失败返回 ObjectId.Null.</returns>
        public ObjectId CreateEntityFromLoop(
            HatchLoop loop,
            Plane plane,
            int colorIndex,
            string layer,
            ITransactionService ts)
        {
            // ── Polyline 环 ──────────────────────────────────────────
            if (loop.IsPolyline)
            {
                return this.CreatePolylineFromBulgeVerts(loop.Polyline, plane, colorIndex, layer, ts);
            }

            // ── 曲线环 ──────────────────────────────────────────────
            if (loop.Curves == null || loop.Curves.Count == 0)
            {
                return ObjectId.Null;
            }

            // 单曲线快速路径
            if (loop.Curves.Count == 1)
            {
                var curve = loop.Curves[0];

                // 完整圆
                if (curve is CircularArc2d arc)
                {
                    double span = Math.Abs(arc.EndAngle - arc.StartAngle);
                    if (span >= Math.PI * 2 - 1e-6)
                    {
                        return this.CreateCircleEntity(arc, plane, colorIndex, layer, ts);
                    }
                }

                // 完整椭圆
                if (curve is EllipticalArc2d ell)
                {
                    double span = Math.Abs(ell.EndAngle - ell.StartAngle);
                    if (span >= Math.PI * 2 - 1e-6)
                    {
                        return this.CreateEllipseEntity(ell, plane, colorIndex, layer, ts);
                    }
                }

                // NURBS → CurveFit Polyline2d
                if (curve is NurbCurve2d nurb)
                {
                    return this.CreateCurveFitPolylineFromNurb(nurb, plane, colorIndex, layer, ts);
                }
            }

            // 多曲线组合环 → 采样为直线段多段线
            return this.CreatePolylineFromSampledLoop(loop, plane, colorIndex, layer, ts);
        }

        /// <summary>
        ///     从 Curve2d 生成采样点离散形式的多段线 Polyline.
        /// </summary>
        public Polyline CreateSampledPolyline(Curve2d curve, Plane plane, string layer)
        {
            var pts = new List<CorePoint2D>();
            this.AddCurve2dPoints(curve, pts);
            pts = ClosePolygon(pts);
            pts = RemoveAdjacentDuplicates(pts);

            var poly = new Polyline();
            poly.Layer = layer;
            for (int i = 0; i < pts.Count; i++)
            {
                var pt3d = plane.EvaluatePoint(new Point2d(pts[i].X, pts[i].Y));
                poly.AddVertexAt(i, new Point2d(pt3d.X, pt3d.Y), 0.0, 0.0, 0.0);
            }

            poly.Closed = true;
            return poly;
        }

        // ──────────────────────────────────────────────────────────────
        //  私有辅助：多边形生成
        // ──────────────────────────────────────────────────────────────

        private List<CorePoint2D> ConvertPolyline(Polyline pl)
        {
            int n = pl.NumberOfVertices;
            if (n < 3)
            {
                return null;
            }

            var points = new List<CorePoint2D>();

            for (int i = 0; i < n; i++)
            {
                var pt = pl.GetPoint2dAt(i);
                points.Add(new CorePoint2D(pt.X, pt.Y));

                double bulge = pl.GetBulgeAt(i);
                if (Math.Abs(bulge) > 1e-9)
                {
                    // 弧段：精确展开为多边形点
                    var endPt = pl.GetPoint2dAt((i + 1) % n);
                    var arcPoints = this.GenerateArcFromBulge(
                        new CorePoint2D(pt.X, pt.Y),
                        new CorePoint2D(endPt.X, endPt.Y),
                        bulge);
                    if (arcPoints != null && arcPoints.Count > 1)
                    {
                        // 跳过第一个点（已添加），添加剩余点
                        for (int j = 1; j < arcPoints.Count; j++)
                        {
                            points.Add(arcPoints[j]);
                        }
                    }
                }
            }

            return points.Count >= 3 ? points : null;
        }

        private List<CorePoint2D> ConvertCircle(Circle c)
        {
            var pts = this._exactGen.GenerateFullCircle(
                new CorePoint2D(c.Center.X, c.Center.Y),
                c.Radius);
            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
            {
                result.Add(new CorePoint2D(pt.X, pt.Y));
            }

            return result.Count >= 3 ? result : null;
        }

        private List<CorePoint2D> ConvertEllipse(Ellipse e)
        {
            // 完整椭圆 → 密集采样（128点）确保裁剪精度
            // ★ 必须使用 MajorAxis 方向向量，忽略旋转会导致边界多边形错误
            // ★ 不生成重复首尾点（i < N 而非 i <= N），否则零长度退化边
            //   会导致 IsPointInPolygon 的 IsPointOnSegment 对任意点误判"在边上"
            var center = new CorePoint2D(e.Center.X, e.Center.Y);
            var majorAxis = e.MajorAxis;                     // Vector3d: 方向和长轴半长
            var majorDir = majorAxis.GetNormal();            // 长轴单位方向
            var minorDir = new Vector3d(-majorDir.Y, majorDir.X, 0); // XY 平面内垂直长轴
            var majorLen = majorAxis.Length;
            var minorLen = e.MinorRadius;

            var result = new List<CorePoint2D>();
            const int ellipseSamples = 128;
            for (int i = 0; i < ellipseSamples; i++)  // ★ i < N，不含末点（与首点重合）
            {
                var angle = 2.0 * Math.PI * i / ellipseSamples;
                var cosA = Math.Cos(angle);
                var sinA = Math.Sin(angle);
                var x = center.X + majorDir.X * majorLen * cosA
                                    + minorDir.X * minorLen * sinA;
                var y = center.Y + majorDir.Y * majorLen * cosA
                                    + minorDir.Y * minorLen * sinA;
                result.Add(new CorePoint2D(x, y));
            }

            return result.Count >= 3 ? result : null;
        }

        private List<CorePoint2D> ConvertSpline(Spline s)
        {
            // 闭合 Spline 边界 → 密集采样 200 点确保裁剪精度
            var startPt = new Point2D(s.StartPoint.X, s.StartPoint.Y);
            var endPt = new Point2D(s.EndPoint.X, s.EndPoint.Y);

            var pts = this._fittedGen.GenerateGenericCurve(
                startPt, endPt,
                t =>
                {
                    double param = s.StartParam + (s.EndParam - s.StartParam) * t;
                    var pt = s.GetPointAtParameter(param);
                    return new Point2D(pt.X, pt.Y);
                },
                200);

            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
            {
                result.Add(new CorePoint2D(pt.X, pt.Y));
            }

            return result.Count >= 3 ? result : null;
        }

        private List<CorePoint2D> ConvertGenericClosedCurve(Curve curve)
        {
            var start = curve.StartPoint;
            var end = curve.EndPoint;
            var pts = this._fittedGen.GenerateGenericCurve(
                new Point2D(start.X, start.Y),
                new Point2D(end.X, end.Y),
                t =>
                {
                    double param = curve.StartParam + (curve.EndParam - curve.StartParam) * t;
                    var pt = curve.GetPointAtParameter(param);
                    return new Point2D(pt.X, pt.Y);
                });

            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
            {
                result.Add(new CorePoint2D(pt.X, pt.Y));
            }

            return result.Count >= 3 ? result : null;
        }

        /// <summary>
        ///     从凸度展开弧段，返回多边形顶点.
        /// </summary>
        private IReadOnlyList<CorePoint2D> GenerateArcFromBulge(
            CorePoint2D start,
            CorePoint2D end,
            double bulge)
        {
            if (this._exactGen.TryGetArcFromBulge(start, end, bulge,
                out var center, out var radius, out var startAngle, out var endAngle, out var isClockwise))
            {
                var pts = this._exactGen.GenerateArc(center, radius, startAngle, endAngle, isClockwise);
                var result = new List<CorePoint2D>();
                foreach (var pt in pts)
                {
                    result.Add(new CorePoint2D(pt.X, pt.Y));
                }

                return result;
            }

            // 直线段，只返回端点
            return new List<CorePoint2D> { start, end };
        }

        // ──────────────────────────────────────────────────────────────
        //  私有辅助：CAD 实体创建
        // ──────────────────────────────────────────────────────────────

        private ObjectId CreatePolylineFromBulgeVerts(
            BulgeVertexCollection bulgeVerts,
            Plane plane,
            int colorIndex,
            string layer,
            ITransactionService ts)
        {
            if (bulgeVerts == null || bulgeVerts.Count == 0)
            {
                return ObjectId.Null;
            }

            var poly = new Polyline();
            foreach (BulgeVertex bv in bulgeVerts)
            {
                var pt3d = plane.EvaluatePoint(bv.Vertex);
                poly.AddVertexAt(
                    poly.NumberOfVertices,
                    new Point2d(pt3d.X, pt3d.Y),
                    bv.Bulge,
                    0.0,
                    0.0);
            }

            poly.Closed = true;
            poly.ColorIndex = colorIndex;
            poly.Layer = layer;
            return ts.AppendEntityToCurrentSpace(poly);
        }

        private ObjectId CreateCircleEntity(
            CircularArc2d arc,
            Plane plane,
            int colorIndex,
            string layer,
            ITransactionService ts)
        {
            var center = plane.EvaluatePoint(arc.Center);
            var circle = new Circle(center, plane.Normal, arc.Radius)
            {
                ColorIndex = colorIndex,
                Layer = layer,
            };
            return ts.AppendEntityToCurrentSpace(circle);
        }

        private ObjectId CreateEllipseEntity(
            EllipticalArc2d ell,
            Plane plane,
            int colorIndex,
            string layer,
            ITransactionService ts)
        {
            var center = plane.EvaluatePoint(ell.Center);
            var majorDir = plane.EvaluatePoint(ell.Center + ell.MajorAxis * ell.MajorRadius) - center;
            double ratio = ell.MinorRadius / ell.MajorRadius;
            var ellipse = new Ellipse(center, plane.Normal, majorDir, ratio, 0.0, Math.PI * 2)
            {
                ColorIndex = colorIndex,
                Layer = layer,
            };
            return ts.AppendEntityToCurrentSpace(ellipse);
        }

        private ObjectId CreateCurveFitPolylineFromNurb(
            NurbCurve2d nurb,
            Plane plane,
            int colorIndex,
            string layer,
            ITransactionService ts)
        {
            try
            {
                var startPt = new Point2D(nurb.StartPoint.X, nurb.StartPoint.Y);
                var endPt = new Point2D(nurb.EndPoint.X, nurb.EndPoint.Y);
                int numCtrlPts = nurb.Order; // NurbCurve2d 没有直接的 NumControlPoints，用 Order 近似

                var sampled = this._fittedGen.GenerateSpline(
                    startPt, endPt,
                    t =>
                    {
                        double param = MapNurbParameter(nurb, t);
                        var pt = nurb.EvaluatePoint(param);
                        return new Point2D(pt.X, pt.Y);
                    },
                    numCtrlPts);

                if (sampled == null || sampled.Count < 3)
                {
                    return ObjectId.Null;
                }

                var poly2d = new Polyline2d
                {
                    PolyType = Poly2dType.SimplePoly,
                    Closed = true,
                    ColorIndex = colorIndex,
                    Layer = layer,
                };

                if (ts.AppendEntityToCurrentSpace(poly2d).IsNull)
                {
                    return ObjectId.Null;
                }

                foreach (var cp in sampled)
                {
                    var pt3d = plane.EvaluatePoint(new Point2d(cp.X, cp.Y));
                    using (var vertex = new Vertex2d(new Point3d(pt3d.X, pt3d.Y, 0.0), 0.0, 0.0, 0.0, 0.0))
                    {
                        poly2d.AppendVertex(vertex);
                        ts.AddNewlyCreatedDBObject(vertex, true);
                    }
                }

                poly2d.CurveFit();
                return poly2d.ObjectId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"CreateCurveFitPolylineFromNurb 失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }

        private ObjectId CreatePolylineFromSampledLoop(
            HatchLoop loop,
            Plane plane,
            int colorIndex,
            string layer,
            ITransactionService ts)
        {
            var loopPts = this.ConvertLoopToPolygon(loop);
            if (loopPts == null || loopPts.Count < 3)
            {
                return ObjectId.Null;
            }

            var poly = new Polyline();
            foreach (var cp in loopPts)
            {
                var pt3d = plane.EvaluatePoint(new Point2d(cp.X, cp.Y));
                poly.AddVertexAt(
                    poly.NumberOfVertices,
                    new Point2d(pt3d.X, pt3d.Y),
                    0.0,
                    0.0,
                    0.0);
            }

            poly.Closed = true;
            poly.ColorIndex = colorIndex;
            poly.Layer = layer;
            return ts.AppendEntityToCurrentSpace(poly);
        }

        // ──────────────────────────────────────────────────────────────
        //  私有辅助：Curve2d → 点
        // ──────────────────────────────────────────────────────────────

        private void AddCurve2dPoints(Curve2d curve, List<CorePoint2D> points)
        {
            if (curve is LineSegment2d line)
            {
                var start = line.StartPoint;
                points.Add(new CorePoint2D(start.X, start.Y));
            }
            else if (curve is CircularArc2d arc)
            {
                // 弧段 → 精确展开（ExactCurveGenerator）
                var center = arc.Center;
                var arcPoints = this._exactGen.GenerateArc(
                    new CorePoint2D(center.X, center.Y),
                    arc.Radius,
                    arc.StartAngle,
                    arc.EndAngle,
                    arc.IsClockWise);

                foreach (var pt in arcPoints)
                {
                    points.Add(new CorePoint2D(pt.X, pt.Y));
                }
            }
            else if (curve is EllipticalArc2d ellipse)
            {
                // 椭圆弧 → 拟合采样（FittedCurveGenerator）
                var center = ellipse.Center;
                var arcPoints = this._fittedGen.GenerateEllipticalArc(
                    new CorePoint2D(center.X, center.Y),
                    ellipse.MajorRadius,
                    ellipse.MinorRadius / ellipse.MajorRadius,
                    ellipse.StartAngle,
                    ellipse.EndAngle,
                    ellipse.IsClockWise);

                foreach (var pt in arcPoints)
                {
                    points.Add(new CorePoint2D(pt.X, pt.Y));
                }
            }
            else if (curve is NurbCurve2d nurb)
            {
                // NURBS → 拟合采样（FittedCurveGenerator）
                var startPt = new CorePoint2D(nurb.StartPoint.X, nurb.StartPoint.Y);
                var endPt = new CorePoint2D(nurb.EndPoint.X, nurb.EndPoint.Y);
                int numCtrlPts = Math.Max(4, nurb.Order);

                var nurbPoints = this._fittedGen.GenerateSpline(
                    new Point2D(startPt.X, startPt.Y),
                    new Point2D(endPt.X, endPt.Y),
                    t =>
                    {
                        var param = MapNurbParameter(nurb, t);
                        var pt = nurb.EvaluatePoint(param);
                        return new Point2D(pt.X, pt.Y);
                    },
                    numCtrlPts);

                foreach (var pt in nurbPoints)
                {
                    points.Add(new CorePoint2D(pt.X, pt.Y));
                }
            }
            else if (curve is PolylineCurve2d)
            {
                // PolylineCurve2d → 拟合采样
                var startPt = new CorePoint2D(curve.StartPoint.X, curve.StartPoint.Y);
                var endPt = new CorePoint2D(curve.EndPoint.X, curve.EndPoint.Y);

                var polyPts = this._fittedGen.GenerateGenericCurve(
                    new Point2D(startPt.X, startPt.Y),
                    new Point2D(endPt.X, endPt.Y),
                    t =>
                    {
                        var pt = curve.EvaluatePoint(t);
                        return new Point2D(pt.X, pt.Y);
                    },
                    16);

                foreach (var pt in polyPts)
                {
                    points.Add(new CorePoint2D(pt.X, pt.Y));
                }
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  静态辅助方法（无 CAD 依赖或通用）
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     NURBS 曲线参数范围映射：将 t∈[0,1] 映射到 Knots[degree] ~ Knots[n-degree-1].
        /// </summary>
        private static double MapNurbParameter(NurbCurve2d nurb, double t)
        {
            try
            {
                int degree = nurb.Order - 1;
                if (degree < 0)
                {
                    return t;
                }

                var knots = nurb.Knots;
                if (knots == null || knots.Count < degree * 2 + 2)
                {
                    return t;
                }

                double tStart = knots[degree];
                double tEnd = knots[knots.Count - degree - 1];
                double range = tEnd - tStart;
                if (range <= 0)
                {
                    return tStart;
                }

                return tStart + range * t;
            }
            catch
            {
                return t;
            }
        }

        /// <summary>
        ///     闭合多边形：如果首尾点不重合，添加首点作为终点.
        /// </summary>
        private static List<CorePoint2D> ClosePolygon(List<CorePoint2D> points)
        {
            if (points.Count < 3)
            {
                return points;
            }

            var result = new List<CorePoint2D>(points);
            var first = points[0];
            var last = points[points.Count - 1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            if ((dx * dx + dy * dy) > 1e-12)
            {
                result.Add(first);
            }

            return result;
        }

        /// <summary>
        ///     去除相邻重复点（距离小于 1e-20 视为重复）.
        /// </summary>
        private static List<CorePoint2D> RemoveAdjacentDuplicates(List<CorePoint2D> points)
        {
            if (points.Count < 2)
            {
                return points;
            }

            var result = new List<CorePoint2D>();
            for (var i = 0; i < points.Count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % points.Count];
                var dx = current.X - next.X;
                var dy = current.Y - next.Y;
                if ((dx * dx + dy * dy) >= 1e-20)
                {
                    result.Add(current);
                }
            }

            return result;
        }
    }
}