using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     曲线精确段适配器 — 将 AutoCAD Curve 转换为 Core 层的 ExactSegment 列表和 ICropBoundary.
    ///     <para>
    ///         - Polyline：逐段提取直线/圆弧（凸度），精确参数化.
    ///         - Circle：完整圆，4 条 90° 圆弧.
    ///         - Ellipse：完整椭圆，4 条 90° 椭圆弧.
    ///         - Spline/其他：采样为多边形直线段（拟合）.
    ///     </para>
    ///     依赖 AutoCAD API，位于 ServiceACAD 层.
    /// </summary>
    public static class CurveToExactSegmentConverter
    {
        /// <summary>
        ///     将闭合 Curve 转换为 ExactSegment 列表（精确参数化）.
        /// </summary>
        /// <param name="curve">闭合曲线.</param>
        /// <returns>精确段列表；转换失败返回 null.</returns>
        public static List<ExactSegment> ConvertToExactSegments(Curve curve)
        {
            if (curve == null || !curve.Closed)
                return null;

            try
            {
                if (curve is Polyline pl)
                    return ConvertPolyline(pl);

                if (curve is Circle circle)
                    return ConvertCircle(circle);

                if (curve is Ellipse ellipse)
                    return ConvertEllipse(ellipse);

                // Spline 等其他类型 → 采样为直线段
                return ConvertBySampling(curve);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     将闭合 Curve 转换为 ICropBoundary（用于精确求交和包含测试）.
        ///     委托给 <see cref="CropBoundaryFactory.CreateFromCurve"/> 实现.
        /// </summary>
        /// <param name="curve">闭合曲线.</param>
        /// <returns>裁剪边界；转换失败返回 null.</returns>
        public static ICropBoundary ConvertToCropBoundary(Curve curve)
        {
            return CropBoundaryFactory.CreateFromCurve(curve);
        }

        // ──────────────────────────────────────────────────────────────
        //  Polyline：逐段精确提取
        // ──────────────────────────────────────────────────────────────

        private static List<ExactSegment> ConvertPolyline(Polyline pl)
        {
            int n = pl.NumberOfVertices;
            if (n < 3) return null;

            var segments = new List<ExactSegment>(n);
            int segCount = pl.Closed ? n : n - 1;

            for (int i = 0; i < segCount; i++)
            {
                var startPt = pl.GetPoint2dAt(i);
                var endPt = pl.GetPoint2dAt((i + 1) % n);
                double bulge = pl.GetBulgeAt(i);

                var seg = new ExactSegment
                {
                    Source = SegmentSource.Subject,
                    Start = new Point2D(startPt.X, startPt.Y),
                    End = new Point2D(endPt.X, endPt.Y)
                };

                if (Math.Abs(bulge) > 1e-9)
                {
                    // 圆弧段
                    var arc = pl.GetArcSegment2dAt(i);
                    seg.SegmentType = ExactSegmentType.Arc;
                    seg.ArcCenter = new Point2D(arc.Center.X, arc.Center.Y);
                    seg.ArcRadius = arc.Radius;
                    seg.ArcStartAngle = arc.StartAngle;
                    seg.ArcEndAngle = arc.EndAngle;
                    seg.ArcIsClockwise = bulge < 0;
                }
                else
                {
                    seg.SegmentType = ExactSegmentType.Line;
                }

                segments.Add(seg);
            }

            return segments;
        }

        // ──────────────────────────────────────────────────────────────
        //  Circle：4 条 90° 圆弧
        // ──────────────────────────────────────────────────────────────

        private static List<ExactSegment> ConvertCircle(Circle c)
        {
            var center = new Point2D(c.Center.X, c.Center.Y);
            double r = c.Radius;
            var segments = new List<ExactSegment>(4);

            for (int i = 0; i < 4; i++)
            {
                double startAngle = i * Math.PI / 2.0;
                double endAngle = (i + 1) * Math.PI / 2.0;

                var seg = new ExactSegment
                {
                    Source = SegmentSource.Subject,
                    SegmentType = ExactSegmentType.Arc,
                    Start = new Point2D(
                        center.X + r * Math.Cos(startAngle),
                        center.Y + r * Math.Sin(startAngle)),
                    End = new Point2D(
                        center.X + r * Math.Cos(endAngle),
                        center.Y + r * Math.Sin(endAngle)),
                    ArcCenter = center,
                    ArcRadius = r,
                    ArcStartAngle = startAngle,
                    ArcEndAngle = endAngle,
                    ArcIsClockwise = false
                };
                segments.Add(seg);
            }

            return segments;
        }

        // ──────────────────────────────────────────────────────────────
        //  Ellipse：4 条 90° 椭圆弧
        // ──────────────────────────────────────────────────────────────

        private static List<ExactSegment> ConvertEllipse(Ellipse e)
        {
            var center = new Point2D(e.Center.X, e.Center.Y);
            var majorAxis = e.MajorAxis;
            double majorR = majorAxis.Length;
            double minorR = e.MinorRadius;
            double rotation = Math.Atan2(majorAxis.Y, majorAxis.X);

            var segments = new List<ExactSegment>(4);
            double cosRot = Math.Cos(rotation);
            double sinRot = Math.Sin(rotation);

            for (int i = 0; i < 4; i++)
            {
                double startAngle = i * Math.PI / 2.0;
                double endAngle = (i + 1) * Math.PI / 2.0;

                double sxLocal = majorR * Math.Cos(startAngle);
                double syLocal = minorR * Math.Sin(startAngle);
                double exLocal = majorR * Math.Cos(endAngle);
                double eyLocal = minorR * Math.Sin(endAngle);

                var seg = new ExactSegment
                {
                    Source = SegmentSource.Subject,
                    SegmentType = ExactSegmentType.Ellipse,
                    Start = new Point2D(
                        center.X + sxLocal * cosRot - syLocal * sinRot,
                        center.Y + sxLocal * sinRot + syLocal * cosRot),
                    End = new Point2D(
                        center.X + exLocal * cosRot - eyLocal * sinRot,
                        center.Y + exLocal * sinRot + eyLocal * cosRot),
                    EllipseCenter = center,
                    EllipseMajorRadius = majorR,
                    EllipseMinorRadius = minorR,
                    EllipseRotation = rotation,
                    EllipseStartAngle = startAngle,
                    EllipseEndAngle = endAngle,
                    EllipseIsClockwise = false
                };
                segments.Add(seg);
            }

            return segments;
        }

        // ──────────────────────────────────────────────────────────────
        //  Spline 等其他类型：采样为直线段
        // ──────────────────────────────────────────────────────────────

        private static List<ExactSegment> ConvertBySampling(Curve curve)
        {
            var polygon = new CurveToPolygonConverter().ConvertCurveToPolygon(curve);
            if (polygon == null || polygon.Count < 3)
                return null;

            var segments = new List<ExactSegment>(polygon.Count);
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                var start = polygon[i];
                var end = polygon[(i + 1) % n];

                segments.Add(new ExactSegment
                {
                    Source = SegmentSource.Subject,
                    SegmentType = ExactSegmentType.Line,
                    Start = new Point2D(start.X, start.Y),
                    End = new Point2D(end.X, end.Y)
                });
            }

            return segments;
        }

        /// <summary>
        ///     将 ExactSegment 列表绘制为 AutoCAD 实体并添加到当前空间.
        ///     直线段 → Polyline 直线顶点；圆弧段 → Polyline 带凸度顶点；
        ///     椭圆弧段 → 采样为多段直线.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="loop">精确段组成的闭合环.</param>
        /// <param name="colorIndex">颜色索引.</param>
        /// <returns>创建的顶点总数.</returns>
        public static int DrawExactSegments(
            ITransactionService ts, IReadOnlyList<ExactSegment> loop, int colorIndex)
        {
            if (loop == null || loop.Count == 0)
                return 0;

            try
            {
                var pline = new Polyline();
                pline.SetDatabaseDefaults();
                pline.ColorIndex = colorIndex;

                int vertexIdx = 0;
                int totalVertices = 0;

                for (int segIdx = 0; segIdx < loop.Count; segIdx++)
                {
                    var seg = loop[segIdx];
                    double bulge = 0.0;

                    if (seg.SegmentType == ExactSegmentType.Arc)
                    {
                        // 从圆弧参数计算凸度
                        bulge = CalcBulgeFromArc(
                            seg.ArcStartAngle, seg.ArcEndAngle, seg.ArcIsClockwise);
                        pline.AddVertexAt(vertexIdx,
                            new Point2d(seg.Start.X, seg.Start.Y),
                            bulge, 0.0, 0.0);
                        vertexIdx++;
                        totalVertices++;
                    }
                    else if (seg.SegmentType == ExactSegmentType.Ellipse)
                    {
                        // 椭圆弧采样为多段直线（含起点，不含终点）
                        var pts = seg.ToPolylinePoints();
                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            pline.AddVertexAt(vertexIdx,
                                new Point2d(pts[i].X, pts[i].Y),
                                0.0, 0.0, 0.0);
                            vertexIdx++;
                            totalVertices++;
                        }
                    }
                    else
                    {
                        // 直线段：添加起点
                        pline.AddVertexAt(vertexIdx,
                            new Point2d(seg.Start.X, seg.Start.Y),
                            bulge, 0.0, 0.0);
                        vertexIdx++;
                        totalVertices++;
                    }
                }

                // 添加最后一段的终点（闭合环的最后顶点）
                var lastSeg = loop[loop.Count - 1];
                pline.AddVertexAt(vertexIdx,
                    new Point2d(lastSeg.End.X, lastSeg.End.Y),
                    0.0, 0.0, 0.0);
                totalVertices++;

                pline.Closed = true;

                // 确保 Intersection 图层存在
                try
                {
                    ts.Style.GetOrCreateLayer("Intersection");
                    pline.Layer = "Intersection";
                }
                catch
                {
                    // 图层创建失败时继续使用当前图层
                }

                ts.AppendEntityToCurrentSpace(pline);
                return totalVertices;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        ///     从圆弧起止角和方向计算凸度（bulge）.
        ///     bulge = tan(θ/4)，θ 为圆心角，正=CCW，负=CW.
        /// </summary>
        private static double CalcBulgeFromArc(
            double startAngle, double endAngle, bool isClockwise)
        {
            double span = isClockwise
                ? startAngle - endAngle
                : endAngle - startAngle;
            if (span < 0) span += 2.0 * Math.PI;
            if (span >= 2.0 * Math.PI - 1e-9) span = 2.0 * Math.PI;

            double bulge = Math.Tan(span / 4.0);
            return isClockwise ? -bulge : bulge;
        }
    }
}
