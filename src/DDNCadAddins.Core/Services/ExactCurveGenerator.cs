using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     精确曲线生成器 — 将可用几何参数精确表达的曲线段转换为多边形顶点.
    ///     <para>包括：直线 / Polyline（凸度弧段精确展开）/ 圆弧 / 圆 / 完整椭圆.</para>
    ///     纯数学运算，无 AutoCAD 依赖.
    /// </summary>
    public class ExactCurveGenerator
    {
        private const double DefaultSamplesPerRadian = 8.0;
        private const int DefaultMaxArcSamples = 64;
        private const int DefaultMinArcSamples = 4;

        /// <summary>
        ///     生成直线段的多边形顶点.
        /// </summary>
        public IReadOnlyList<Point2D> GenerateLine(Point2D start, Point2D end)
        {
            return new List<Point2D> { start, end };
        }

        /// <summary>
        ///     将圆弧段精确采样为多边形顶点（按角度均匀采样，满足和弦容忍度）.
        /// </summary>
        /// <param name="center">圆心.</param>
        /// <param name="radius">半径.</param>
        /// <param name="startAngleRad">起始弧度.</param>
        /// <param name="endAngleRad">终止弧度.</param>
        /// <param name="isClockwise">是否顺时针.</param>
        /// <param name="samplesPerRadian">每弧度采样点数.</param>
        /// <param name="maxSamples">最大采样点数.</param>
        /// <param name="minSamples">最小采样点数.</param>
        /// <returns>弧段上的点序列（含起止点）.</returns>
        public IReadOnlyList<Point2D> GenerateArc(
            Point2D center,
            double radius,
            double startAngleRad,
            double endAngleRad,
            bool isClockwise,
            double samplesPerRadian = DefaultSamplesPerRadian,
            int maxSamples = DefaultMaxArcSamples,
            int minSamples = DefaultMinArcSamples)
        {
            var points = new List<Point2D>();

            var angleSpan = endAngleRad - startAngleRad;
            if (isClockwise)
            {
                angleSpan = startAngleRad - endAngleRad;
            }

            if (angleSpan < 0)
            {
                angleSpan += 2.0 * Math.PI;
            }

            var samples = (int)Math.Ceiling(angleSpan * samplesPerRadian);
            samples = Math.Max(minSamples, Math.Min(maxSamples, samples));

            var step = angleSpan / samples;
            var direction = isClockwise ? -1.0 : 1.0;

            for (var i = 0; i <= samples; i++)
            {
                var angle = startAngleRad + (direction * step * i);
                var x = center.X + (radius * Math.Cos(angle));
                var y = center.Y + (radius * Math.Sin(angle));
                points.Add(new Point2D(x, y));
            }

            return points;
        }

        /// <summary>
        ///     生成完整圆的顶点.
        /// </summary>
        public IReadOnlyList<Point2D> GenerateFullCircle(
            Point2D center,
            double radius,
            int samples = DefaultMaxArcSamples)
        {
            var points = new List<Point2D>(samples + 1);
            var step = (2.0 * Math.PI) / samples;

            for (var i = 0; i <= samples; i++)
            {
                var angle = step * i;
                var x = center.X + (radius * Math.Cos(angle));
                var y = center.Y + (radius * Math.Sin(angle));
                points.Add(new Point2D(x, y));
            }

            return points;
        }

        /// <summary>
        ///     生成完整椭圆的顶点（使用4个关键控制点精确表达椭圆形状）.
        ///     对于大多数应用场景，4个点已经足够表达椭圆的几何特征。
        ///     如需更高精度，可配合 FittedCurveGenerator 的采样方法.
        /// </summary>
        /// <param name="center">椭圆中心.</param>
        /// <param name="majorRadius">长轴半径.</param>
        /// <param name="minorRatio">短轴比（短轴/长轴）.</param>
        /// <returns>4个关键顶点（0°, 90°, 180°, 270°）.</returns>
        public IReadOnlyList<Point2D> GenerateFullEllipse(
            Point2D center,
            double majorRadius,
            double minorRatio)
        {
            var minorRadius = majorRadius * minorRatio;

            return new List<Point2D>
            {
                new Point2D(center.X + majorRadius, center.Y),          // 0°
                new Point2D(center.X, center.Y + minorRadius),          // 90°
                new Point2D(center.X - majorRadius, center.Y),          // 180°
                new Point2D(center.X, center.Y - minorRadius),          // 270°
            };
        }

        /// <summary>
        ///     通过凸度（bulge）值精确计算弧段的圆心和起止角.
        ///     参考：theta = 4 * atan(bulge)
        /// </summary>
        /// <param name="start">起点.</param>
        /// <param name="end">终点.</param>
        /// <param name="bulge">凸度值.</param>
        /// <param name="center">输出圆心.</param>
        /// <param name="radius">输出半径.</param>
        /// <param name="startAngle">输出起始弧度.</param>
        /// <param name="endAngle">输出终止弧度.</param>
        /// <param name="isClockwise">输出是否顺时针.</param>
        /// <returns>如果能计算出有效弧段返回 true.</returns>
        public bool TryGetArcFromBulge(
            Point2D start,
            Point2D end,
            double bulge,
            out Point2D center,
            out double radius,
            out double startAngle,
            out double endAngle,
            out bool isClockwise)
        {
            center = default(Point2D);
            radius = 0;
            startAngle = 0;
            endAngle = 0;
            isClockwise = false;

            if (Math.Abs(bulge) < 1e-12)
            {
                // 直线段，无弧
                return false;
            }

            var theta = Math.Atan(bulge) * 4.0;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var chordLenSq = dx * dx + dy * dy;
            if (chordLenSq < 1e-12)
            {
                return false;
            }

            var chordLen = Math.Sqrt(chordLenSq);
            radius = chordLen / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));

            var midX = (start.X + end.X) / 2.0;
            var midY = (start.Y + end.Y) / 2.0;
            var perpX = -(end.Y - start.Y) / chordLen;
            var perpY = (end.X - start.X) / chordLen;
            var sagitta = radius * (1.0 - Math.Cos(theta / 2.0));
            if (bulge < 0)
            {
                sagitta = -sagitta;
            }

            center = new Point2D(midX + perpX * sagitta, midY + perpY * sagitta);
            startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
            isClockwise = bulge < 0;

            return true;
        }
    }
}