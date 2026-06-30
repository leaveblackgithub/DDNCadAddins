using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     拟合曲线生成器 — 将无法用简单几何参数精确表达的曲线段按控制点倍数分段采样为多边形顶点.
    ///     <para>包括：椭圆弧（EllipticalArc）/ 样条曲线（Spline/NURBS）.</para>
    ///     纯数学运算，无 AutoCAD 依赖.
    /// </summary>
    public class FittedCurveGenerator
    {
        /// <summary>默认采样精度：每弧度采样点数（用于弧/椭圆弧）.</summary>
        private const double DefaultSamplesPerRadian = 8.0;

        /// <summary>默认最大采样点数.</summary>
        private const int DefaultMaxSamples = 64;

        /// <summary>默认最小采样点数.</summary>
        private const int DefaultMinSamples = 4;

        /// <summary>默认通用曲线采样段数.</summary>
        private const int DefaultGenericCurveSamples = 50;

        /// <summary>SPLINE 每控制点采样倍数.</summary>
        private const int DefaultSamplesPerControlPoint = 8;

        /// <summary>SPLINE 最小采样点数.</summary>
        private const int DefaultMinSplineSamples = 32;

        /// <summary>
        ///     将椭圆弧段采样为直线段顶点（使用逼近椭圆参数方程）.
        /// </summary>
        /// <param name="center">椭圆中心.</param>
        /// <param name="majorRadius">长轴半径.</param>
        /// <param name="minorRatio">短轴比（短轴/长轴）.</param>
        /// <param name="startAngleRad">起始弧度.</param>
        /// <param name="endAngleRad">终止弧度.</param>
        /// <param name="isClockwise">是否顺时针.</param>
        /// <param name="samplesPerRadian">每弧度采样点数.</param>
        /// <param name="maxSamples">最大采样点数.</param>
        /// <param name="minSamples">最小采样点数.</param>
        /// <returns>椭圆弧上的点序列（含起止点）.</returns>
        public IReadOnlyList<Point2D> GenerateEllipticalArc(
            Point2D center,
            double majorRadius,
            double minorRatio,
            double startAngleRad,
            double endAngleRad,
            bool isClockwise,
            double samplesPerRadian = DefaultSamplesPerRadian,
            int maxSamples = DefaultMaxSamples,
            int minSamples = DefaultMinSamples)
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
                var cosA = Math.Cos(angle);
                var sinA = Math.Sin(angle);
                var x = center.X + (majorRadius * cosA);
                var y = center.Y + (majorRadius * sinA * minorRatio);
                points.Add(new Point2D(x, y));
            }

            return points;
        }

        /// <summary>
        ///     生成 SPLINE 曲线的采样点（按控制点倍数确定采样密度）.
        /// </summary>
        /// <param name="startPoint">曲线起点.</param>
        /// <param name="endPoint">曲线终点.</param>
        /// <param name="evaluator">参数求值回调：t ∈ [0,1] → 曲线上的点.</param>
        /// <param name="numControlPoints">控制点数量（用于确定采样密度）.</param>
        /// <param name="samplesPerControlPoint">每控制点的采样倍数（默认8）.</param>
        /// <param name="minSamples">最小采样点数（默认32）.</param>
        /// <returns>曲线上的采样点序列（含起止点）.</returns>
        public IReadOnlyList<Point2D> GenerateSpline(
            Point2D startPoint,
            Point2D endPoint,
            Func<double, Point2D> evaluator,
            int numControlPoints,
            int samplesPerControlPoint = DefaultSamplesPerControlPoint,
            int minSamples = DefaultMinSplineSamples)
        {
            var samples = Math.Max(minSamples, numControlPoints * samplesPerControlPoint);
            return this.SampleGenericCurveUniform(startPoint, endPoint, samples, evaluator);
        }

        /// <summary>
        ///     生成通用曲线的采样点（固定分段数的均匀采样）.
        ///     用于 3DPolyline / MLine / Leader 等无精确几何参数的曲线类型.
        /// </summary>
        /// <param name="startPoint">曲线起点.</param>
        /// <param name="endPoint">曲线终点.</param>
        /// <param name="evaluator">参数求值回调：t ∈ [0,1] → 曲线上的点.</param>
        /// <param name="segments">分段数（默认50）.</param>
        /// <returns>曲线上的采样点序列（含起止点）.</returns>
        public IReadOnlyList<Point2D> GenerateGenericCurve(
            Point2D startPoint,
            Point2D endPoint,
            Func<double, Point2D> evaluator,
            int segments = DefaultGenericCurveSamples)
        {
            return this.SampleGenericCurveUniform(startPoint, endPoint, segments, evaluator);
        }

        /// <summary>
        ///     在参数空间 [0,1] 内均匀采样，返回点序列.
        /// </summary>
        private List<Point2D> SampleGenericCurveUniform(
            Point2D startPoint,
            Point2D endPoint,
            int samples,
            Func<double, Point2D> evaluator)
        {
            var points = new List<Point2D>();

            if (samples <= 0)
            {
                samples = 16;
            }

            try
            {
                var dx = endPoint.X - startPoint.X;
                var dy = endPoint.Y - startPoint.Y;
                var length = Math.Sqrt((dx * dx) + (dy * dy));

                // 退化曲线（起点≈终点，例如闭合样条线）：
                // 不能只返回单点，必须评估 evaluator 全范围采样以获得曲线形状。
                if (length < 1e-12)
                {
                    samples = Math.Max(samples, 8);
                    var degenerateStep = 1.0 / samples;
                    for (var i = 0; i <= samples; i++)
                    {
                        var t = degenerateStep * i;
                        var pt = evaluator(t);
                        points.Add(new Point2D(pt.X, pt.Y));
                    }

                    return points;
                }

                var step = 1.0 / samples;
                for (var i = 0; i <= samples; i++)
                {
                    var t = step * i;
                    var pt = evaluator(t);
                    points.Add(new Point2D(pt.X, pt.Y));
                }
            }
            catch
            {
                // 采样失败时至少添加起点和终点
                points.Add(new Point2D(startPoint.X, startPoint.Y));
                points.Add(new Point2D(endPoint.X, endPoint.Y));
            }

            return points;
        }
    }
}