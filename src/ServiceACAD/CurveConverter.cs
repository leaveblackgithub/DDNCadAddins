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
    ///     曲线→多边形转换器：将各种 AutoCAD Curve 类型转换为多边形顶点列表.
    ///     - Polyline：逐顶点+凸度提取（精确）
    ///     - Circle：用 2 个半圆+bulges=1.0 表示（精确）
    ///     - Ellipse：提取关键顶点+凸度（精确）
    ///     - Spline：CurveSampler 32点采样（近似，同 GENERATEHATCHBOUNDARY 精度）
    /// </summary>
    public static class CurveConverter
    {
        /// <summary>
        ///     将闭合 Curve 转换为多边形顶点列表 (WCS).
        ///     返回 null 表示转换失败或曲线不闭合.
        /// </summary>
        public static List<CorePoint2D> ConvertToPolygon(Curve curve)
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
                if (curve is Spline spline)
                    return ConvertSpline(spline);

                // 其他闭合曲线 → 均匀采样
                return ConvertGenericClosedCurve(curve);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Polyline → 逐顶点提取（含凸度信息，精确）.
        /// </summary>
        private static List<CorePoint2D> ConvertPolyline(Polyline pl)
        {
            int n = pl.NumberOfVertices;
            if (n < 3) return null;

            // 对于直线段 Polyline（所有 bulge=0），直接返回顶点
            // 对于含弧段的 Polyline，采样弧段
            var sampler = new CurveSampler();
            var points = new List<CorePoint2D>();

            for (int i = 0; i < n; i++)
            {
                var pt = pl.GetPoint2dAt(i);
                points.Add(new CorePoint2D(pt.X, pt.Y));

                double bulge = pl.GetBulgeAt(i);
                if (Math.Abs(bulge) > 1e-9)
                {
                    // 弧段：采样中间点
                    int nextIdx = (i + 1) % n;
                    var nextPt = pl.GetPoint2dAt(nextIdx);
                    var arcPoints = SampleArcSegment(pt, nextPt, bulge);
                    if (arcPoints != null)
                    {
                        // 跳过第一个点（已添加 pt），添加剩余点
                        for (int j = 1; j < arcPoints.Count; j++)
                            points.Add(arcPoints[j]);
                    }
                }
            }

            return points.Count >= 3 ? points : null;
        }

        /// <summary>
        ///     Circle → 用 2 个半圆顶点 + bulges=1.0 表示（精确）.
        ///     这里转换为 64 顶点近似以便 PolygonClipper 使用.
        /// </summary>
        private static List<CorePoint2D> ConvertCircle(Circle c)
        {
            var sampler = new CurveSampler();
            var pts = sampler.SampleArc(
                c.Center.X - c.Radius, c.Center.Y,
                c.Center.X, c.Center.Y,
                c.Radius,
                0, Math.PI * 2, false);
            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
                result.Add(new CorePoint2D(pt.X, pt.Y));
            return result.Count >= 3 ? result : null;
        }

        /// <summary>
        ///     Ellipse → 64 顶点采样（保持与 GENERATEHATCHBOUNDARY 精度一致）.
        /// </summary>
        private static List<CorePoint2D> ConvertEllipse(Ellipse e)
        {
            var sampler = new CurveSampler();
            var startPt = new Point2D(e.StartPoint.X, e.StartPoint.Y);
            var endPt = new Point2D(e.EndPoint.X, e.EndPoint.Y);
            int samples = 64;
            var pts = sampler.SampleGenericCurve(startPt, endPt, samples,
                t =>
                {
                    double param = e.StartParam + (e.EndParam - e.StartParam) * t;
                    var pt = e.GetPointAtParameter(param);
                    return new Point2D(pt.X, pt.Y);
                });
            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
                result.Add(new CorePoint2D(pt.X, pt.Y));
            return result.Count >= 3 ? result : null;
        }

        /// <summary>
        ///     Spline → 按控制点倍数采样（每控制点 8 点，保证曲线精度）.
        /// </summary>
        private static List<CorePoint2D> ConvertSpline(Spline s)
        {
            var sampler = new CurveSampler();
            var startPt = new Point2D(s.StartPoint.X, s.StartPoint.Y);
            var endPt = new Point2D(s.EndPoint.X, s.EndPoint.Y);

            // 按控制点倍数采样：每控制点 8 点，最少 32 点
            int numCtrlPts = s.NumControlPoints;
            int samples = Math.Max(32, numCtrlPts * 8);

            var pts = sampler.SampleGenericCurve(startPt, endPt, samples,
                t =>
                {
                    double param = s.StartParam + (s.EndParam - s.StartParam) * t;
                    var pt = s.GetPointAtParameter(param);
                    return new Point2D(pt.X, pt.Y);
                });
            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
                result.Add(new CorePoint2D(pt.X, pt.Y));
            return result.Count >= 3 ? result : null;
        }

        private static List<CorePoint2D> ConvertGenericClosedCurve(Curve curve)
        {
            var sampler = new CurveSampler();
            var start = curve.StartPoint;
            var end = curve.EndPoint;
            var pts = sampler.SampleGenericCurve(
                new Point2D(start.X, start.Y),
                new Point2D(end.X, end.Y),
                64,
                t =>
                {
                    double param = curve.StartParam + (curve.EndParam - curve.StartParam) * t;
                    var pt = curve.GetPointAtParameter(param);
                    return new Point2D(pt.X, pt.Y);
                });
            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
                result.Add(new CorePoint2D(pt.X, pt.Y));
            return result.Count >= 3 ? result : null;
        }

        /// <summary>
        ///     对单个弧段（2个端点+bulge）进行采样，返回多边形顶点.
        /// </summary>
        private static List<CorePoint2D> SampleArcSegment(Point2d start, Point2d end, double bulge)
        {
            double theta = Math.Atan(bulge) * 4.0;
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double chordLen = Math.Sqrt(dx * dx + dy * dy);
            if (chordLen < 1e-12) return null;

            double radius = chordLen / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));
            double midX = (start.X + end.X) / 2.0;
            double midY = (start.Y + end.Y) / 2.0;
            double perpX = -(end.Y - start.Y) / chordLen;
            double perpY = (end.X - start.X) / chordLen;
            double sagitta = radius * (1 - Math.Cos(theta / 2.0));
            if (bulge < 0) sagitta = -sagitta;
            double centerX = midX + perpX * sagitta;
            double centerY = midY + perpY * sagitta;

            double startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
            double endAngle = Math.Atan2(end.Y - centerY, end.X - centerX);

            // 采样弧段
            var sampler = new CurveSampler();
            var pts = sampler.SampleArc(start.X, start.Y, centerX, centerY,
                Math.Abs(radius), startAngle, endAngle, theta < 0);
            var result = new List<CorePoint2D>();
            foreach (var pt in pts)
                result.Add(new CorePoint2D(pt.X, pt.Y));
            return result;
        }
    }
}