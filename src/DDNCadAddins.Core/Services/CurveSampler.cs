using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     曲线采样器 — 将各种曲线段参数转换为多边形顶点列表（纯数学运算，无 AutoCAD 依赖）.
    /// </summary>
    public class CurveSampler : ICurveSampler
    {
        /// <summary>弧段采样精度：每弧度采样点数</summary>
        private const double SamplesPerRadian = 8.0;

        /// <summary>最大采样点数（防止弧段过密）</summary>
        private const int MaxSamples = 64;

        /// <summary>最小采样点数（确保弧段可见）</summary>
        private const int MinSamples = 4;

        /// <summary>
        ///     将圆弧段采样为直线段顶点.
        /// </summary>
        public IReadOnlyList<Point2D> SampleArc(
            double startX, double startY,
            double centerX, double centerY,
            double radius,
            double startAngleRad, double endAngleRad,
            bool isClockwise)
        {
            var points = new List<Point2D>();

            // 计算角度跨度
            var angleSpan = endAngleRad - startAngleRad;
            if (isClockwise)
                angleSpan = startAngleRad - endAngleRad;

            // 确保正数
            if (angleSpan < 0)
                angleSpan += 2.0 * Math.PI;

            // 采样点数
            var samples = (int)Math.Ceiling(angleSpan * SamplesPerRadian);
            samples = Math.Max(MinSamples, Math.Min(MaxSamples, samples));

            var step = angleSpan / samples;
            var direction = isClockwise ? -1.0 : 1.0;

            for (var i = 0; i <= samples; i++)
            {
                var angle = startAngleRad + (direction * step * i);
                var x = centerX + (radius * Math.Cos(angle));
                var y = centerY + (radius * Math.Sin(angle));
                points.Add(new Point2D(x, y));
            }

            return points;
        }

        /// <summary>
        ///     将椭圆弧段采样为直线段顶点（使用逼近椭圆参数方程）.
        /// </summary>
        public IReadOnlyList<Point2D> SampleEllipticalArc(
            double centerX, double centerY,
            double majorRadius, double minorRatio,
            double startAngleRad, double endAngleRad,
            bool isClockwise)
        {
            var points = new List<Point2D>();

            var angleSpan = endAngleRad - startAngleRad;
            if (isClockwise)
                angleSpan = startAngleRad - endAngleRad;
            if (angleSpan < 0)
                angleSpan += 2.0 * Math.PI;

            var samples = (int)Math.Ceiling(angleSpan * SamplesPerRadian);
            samples = Math.Max(MinSamples, Math.Min(MaxSamples, samples));

            var step = angleSpan / samples;
            var direction = isClockwise ? -1.0 : 1.0;

            for (var i = 0; i <= samples; i++)
            {
                var angle = startAngleRad + (direction * step * i);
                var cosA = Math.Cos(angle);
                var sinA = Math.Sin(angle);
                var x = centerX + (majorRadius * cosA);
                // 椭圆在 Y 方向按 minorRatio 缩放
                var y = centerY + (majorRadius * sinA * minorRatio);
                points.Add(new Point2D(x, y));
            }

            return points;
        }

        /// <summary>
        ///     将通用曲线采样为多边形（通过 evaluator 回调计算参数空间中的点）.
        /// </summary>
        public IReadOnlyList<Point2D> SampleGenericCurve(
            Point2D startPoint, Point2D endPoint,
            int samples,
            Func<double, Point2D> evaluator)
        {
            var points = new List<Point2D>();

            if (samples <= 0)
                samples = 16;

            try
            {
                var dx = endPoint.X - startPoint.X;
                var dy = endPoint.Y - startPoint.Y;
                var length = Math.Sqrt((dx * dx) + (dy * dy));

                // 退化曲线（startPoint ≈ endPoint，例如闭合样条线的 NurbCurve2d）：
                // 不能直接返回单点，必须评估 evaluator 全范围采样以获得曲线形状。
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

        /// <summary>
        ///     去除相邻重复点（距离小于 1e-20 视为重复）.
        /// </summary>
        public IReadOnlyList<Point2D> RemoveAdjacentDuplicates(IReadOnlyList<Point2D> polygon)
        {
            if (polygon.Count < 2)
                return polygon;

            var result = new List<Point2D>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var dx = current.X - next.X;
                var dy = current.Y - next.Y;
                if ((dx * dx + dy * dy) >= 1e-20)
                    result.Add(current);
            }

            return result;
        }

        /// <summary>
        ///     闭合多边形：如果首尾点不重合，添加首点作为终点.
        /// </summary>
        public IReadOnlyList<Point2D> ClosePolygon(IReadOnlyList<Point2D> polygon)
        {
            if (polygon.Count < 3)
                return polygon;

            var result = new List<Point2D>(polygon);
            var first = polygon[0];
            var last = polygon[polygon.Count - 1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            if ((dx * dx + dy * dy) > 1e-12)
                result.Add(first);

            return result;
        }
    }
}
