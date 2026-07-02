using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     圆形裁剪边界 — 基于解析几何的精确实现，零精度损失.
    ///     <para>
    ///         - 点含判断：点到圆心距离 ≤ 半径
    ///         - 线段求交：直线-圆二次方程解析解
    ///         - 包围盒分类：精确圆-矩形相交判定
    ///     </para>
    ///     纯数学运算，无 AutoCAD 依赖.
    /// </summary>
    public class CircleCropBoundary : ICropBoundary
    {
        private const double Tolerance = 1e-9;

        private readonly Point2D _center;
        private readonly double _radius;
        private readonly double _radiusSq;
        private readonly Point2D _bboxMin;
        private readonly Point2D _bboxMax;

        /// <summary>
        ///     构造圆形裁剪边界.
        /// </summary>
        /// <param name="center">圆心（WCS）.</param>
        /// <param name="radius">半径.</param>
        public CircleCropBoundary(Point2D center, double radius)
        {
            this._center = center;
            this._radius = radius;
            this._radiusSq = radius * radius;
            this._bboxMin = new Point2D(center.X - radius, center.Y - radius);
            this._bboxMax = new Point2D(center.X + radius, center.Y + radius);
        }

        /// <summary>圆心.</summary>
        public Point2D Center => this._center;

        /// <summary>半径.</summary>
        public double Radius => this._radius;

        /// <inheritdoc />
        public Point2D BoundingBoxMin => this._bboxMin;

        /// <inheritdoc />
        public Point2D BoundingBoxMax => this._bboxMax;

        /// <inheritdoc />
        public bool IsPointInside(Point2D point)
        {
            var dx = point.X - this._center.X;
            var dy = point.Y - this._center.Y;
            return (dx * dx + dy * dy) <= this._radiusSq + Tolerance;
        }

        /// <inheritdoc />
        public List<Point2D> FindLineIntersections(Point2D segStart, Point2D segEnd)
        {
            var result = new List<Point2D>();

            var dx = segEnd.X - segStart.X;
            var dy = segEnd.Y - segStart.Y;
            var fx = segStart.X - this._center.X;
            var fy = segStart.Y - this._center.Y;

            var a = dx * dx + dy * dy;
            if (Math.Abs(a) < Tolerance)
                return result;

            var b = 2.0 * (dx * fx + dy * fy);
            var c = fx * fx + fy * fy - this._radiusSq;
            var discriminant = b * b - 4.0 * a * c;
            if (discriminant < 0)
                return result;

            var sqrtD = Math.Sqrt(discriminant);
            var t1 = (-b - sqrtD) / (2.0 * a);

            // t ∈ [0, 1] 表示交点在线段上
            if (t1 >= -Tolerance && t1 <= 1.0 + Tolerance)
            {
                var t1Clamped = Math.Max(0.0, Math.Min(1.0, t1));
                result.Add(new Point2D(segStart.X + t1Clamped * dx, segStart.Y + t1Clamped * dy));
            }

            if (Math.Abs(discriminant) > Tolerance)
            {
                var t2 = (-b + sqrtD) / (2.0 * a);
                if (t2 >= -Tolerance && t2 <= 1.0 + Tolerance)
                {
                    var t2Clamped = Math.Max(0.0, Math.Min(1.0, t2));
                    result.Add(new Point2D(segStart.X + t2Clamped * dx, segStart.Y + t2Clamped * dy));
                }
            }

            // 按距离起点排序
            result.Sort((p1, p2) =>
            {
                var d1 = (p1.X - segStart.X) * (p1.X - segStart.X) + (p1.Y - segStart.Y) * (p1.Y - segStart.Y);
                var d2 = (p2.X - segStart.X) * (p2.X - segStart.X) + (p2.Y - segStart.Y) * (p2.Y - segStart.Y);
                return d1.CompareTo(d2);
            });

            // 去重
            var deduped = new List<Point2D>(result.Count);
            foreach (var pt in result)
            {
                var isDup = false;
                foreach (var existing in deduped)
                {
                    var ddx = pt.X - existing.X;
                    var ddy = pt.Y - existing.Y;
                    if (ddx * ddx + ddy * ddy < Tolerance * Tolerance)
                    {
                        isDup = true;
                        break;
                    }
                }
                if (!isDup)
                    deduped.Add(pt);
            }

            return deduped;
        }

        /// <inheritdoc />
        public ContainmentResult ClassifyBoundingBox(Point2D minPoint, Point2D maxPoint)
        {
            // 圆心
            var cx = this._center.X;
            var cy = this._center.Y;
            var r = this._radius;

            // 包围盒四角
            var corners = new[]
            {
                minPoint,
                new Point2D(maxPoint.X, minPoint.Y),
                maxPoint,
                new Point2D(minPoint.X, maxPoint.Y),
            };

            // 1. 四角是否都在圆内 → Inside
            int insideCount = 0;
            foreach (var corner in corners)
            {
                if (this.IsPointInside(corner))
                    insideCount++;
            }

            if (insideCount == 4)
            {
                // 四角都在圆内，但需检查是否有角恰好在圆周上
                foreach (var corner in corners)
                {
                    var dx = corner.X - cx;
                    var dy = corner.Y - cy;
                    if (Math.Abs(dx * dx + dy * dy - this._radiusSq) < Tolerance * r)
                        return ContainmentResult.Intersects;
                }
                return ContainmentResult.Inside;
            }

            if (insideCount == 0)
            {
                // 2. 没有角在圆内 → 检查圆是否在包围盒内（完全包围）
                if (cx >= minPoint.X - Tolerance && cx <= maxPoint.X + Tolerance &&
                    cy >= minPoint.Y - Tolerance && cy <= maxPoint.Y + Tolerance)
                {
                    // 圆心在包围盒内 → 相交
                    return ContainmentResult.Intersects;
                }

                // 3. 检查包围盒边是否与圆相交
                if (this.BoundingBoxIntersectsCircle(minPoint, maxPoint))
                    return ContainmentResult.Intersects;

                return ContainmentResult.Outside;
            }

            // insideCount 在 (0, 4) → 相交
            return ContainmentResult.Intersects;
        }

        /// <inheritdoc />
        public IReadOnlyList<Point2D> GetApproximatePolygon()
        {
            const int samples = 64;
            var pts = new List<Point2D>(samples);
            var step = 2.0 * Math.PI / samples;
            for (int i = 0; i < samples; i++)
            {
                var angle = step * i;
                pts.Add(new Point2D(
                    this._center.X + this._radius * Math.Cos(angle),
                    this._center.Y + this._radius * Math.Sin(angle)));
            }
            return pts;
        }

        /// <summary>
        ///     检查轴对齐包围盒是否与圆相交.
        /// </summary>
        private bool BoundingBoxIntersectsCircle(Point2D minPoint, Point2D maxPoint)
        {
            // 找到包围盒上离圆心最近的点
            var nearestX = Math.Max(minPoint.X, Math.Min(this._center.X, maxPoint.X));
            var nearestY = Math.Max(minPoint.Y, Math.Min(this._center.Y, maxPoint.Y));
            var dx = nearestX - this._center.X;
            var dy = nearestY - this._center.Y;
            return (dx * dx + dy * dy) <= this._radiusSq + Tolerance;
        }
    }
}
