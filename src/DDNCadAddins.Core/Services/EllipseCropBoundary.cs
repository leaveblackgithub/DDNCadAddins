using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     椭圆裁剪边界 — 基于解析几何的精确实现，零精度损失.
    ///     <para>
    ///         支持任意旋转角度的椭圆.
    ///         - 点含判断：将点变换到椭圆局部坐标系，用隐式方程 x²/a² + y²/b² ≤ 1
    ///         - 线段求交：直线-椭圆二次方程解析解（在椭圆局部坐标系中求解）
    ///         - 包围盒分类：椭圆-矩形精确相交判定
    ///     </para>
    ///     纯数学运算，无 AutoCAD 依赖.
    /// </summary>
    public class EllipseCropBoundary : ICropBoundary
    {
        private const double Tolerance = 1e-9;

        private readonly Point2D _center;
        private readonly double _majorRadius;
        private readonly double _minorRadius;
        private readonly double _rotation;       // 长轴旋转角度（弧度）
        private readonly double _cosRot;
        private readonly double _sinRot;
        private readonly double _majorSq;         // a²
        private readonly double _minorSq;         // b²
        private readonly Point2D _bboxMin;
        private readonly Point2D _bboxMax;

        /// <summary>
        ///     构造椭圆裁剪边界（支持旋转）.
        /// </summary>
        /// <param name="center">椭圆中心（WCS）.</param>
        /// <param name="majorRadius">长轴半径.</param>
        /// <param name="minorRadius">短轴半径.</param>
        /// <param name="rotation">长轴旋转角度（弧度，0=沿X轴）.</param>
        public EllipseCropBoundary(Point2D center, double majorRadius, double minorRadius, double rotation = 0.0)
        {
            this._center = center;
            this._majorRadius = majorRadius;
            this._minorRadius = minorRadius;
            this._rotation = rotation;
            this._cosRot = Math.Cos(rotation);
            this._sinRot = Math.Sin(rotation);
            this._majorSq = majorRadius * majorRadius;
            this._minorSq = minorRadius * minorRadius;

            // 计算椭圆的轴对齐包围盒（考虑旋转）
            // 椭圆参数方程在旋转后，X/Y 极值可通过导数求出
            if (Math.Abs(rotation) < Tolerance)
            {
                // 无旋转，简单情况
                this._bboxMin = new Point2D(center.X - majorRadius, center.Y - minorRadius);
                this._bboxMax = new Point2D(center.X + majorRadius, center.Y + minorRadius);
            }
            else
            {
                // 旋转椭圆的包围盒半宽
                var halfW = Math.Sqrt(
                    this._majorSq * this._cosRot * this._cosRot +
                    this._minorSq * this._sinRot * this._sinRot);
                var halfH = Math.Sqrt(
                    this._majorSq * this._sinRot * this._sinRot +
                    this._minorSq * this._cosRot * this._cosRot);
                this._bboxMin = new Point2D(center.X - halfW, center.Y - halfH);
                this._bboxMax = new Point2D(center.X + halfW, center.Y + halfH);
            }
        }

        /// <summary>椭圆中心.</summary>
        public Point2D Center => this._center;

        /// <summary>长轴半径.</summary>
        public double MajorRadius => this._majorRadius;

        /// <summary>短轴半径.</summary>
        public double MinorRadius => this._minorRadius;

        /// <summary>长轴旋转角度（弧度）.</summary>
        public double Rotation => this._rotation;

        /// <inheritdoc />
        public Point2D BoundingBoxMin => this._bboxMin;

        /// <inheritdoc />
        public Point2D BoundingBoxMax => this._bboxMax;

        /// <inheritdoc />
        public bool IsPointInside(Point2D point)
        {
            // 将点变换到椭圆局部坐标系（逆旋转）
            var dx = point.X - this._center.X;
            var dy = point.Y - this._center.Y;
            var localX = dx * this._cosRot + dy * this._sinRot;
            var localY = -dx * this._sinRot + dy * this._cosRot;

            // 隐式方程：x²/a² + y²/b² ≤ 1
            return (localX * localX) / this._majorSq + (localY * localY) / this._minorSq <= 1.0 + Tolerance;
        }

        /// <inheritdoc />
        public List<Point2D> FindLineIntersections(Point2D segStart, Point2D segEnd)
        {
            var result = new List<Point2D>();

            // 将线段端点变换到椭圆局部坐标系
            var d1 = this.WorldToLocal(segStart);
            var d2 = this.WorldToLocal(segEnd);

            var dx = d2.X - d1.X;
            var dy = d2.Y - d1.Y;

            // 在局部坐标系中，椭圆方程为 x²/a² + y²/b² = 1
            // 直线参数方程：x = x1 + t*dx, y = y1 + t*dy
            // 代入：(x1 + t*dx)²/a² + (y1 + t*dy)²/b² = 1
            // 展开：A*t² + B*t + C = 0
            var A = (dx * dx) / this._majorSq + (dy * dy) / this._minorSq;
            var B = 2.0 * ((d1.X * dx) / this._majorSq + (d1.Y * dy) / this._minorSq);
            var C = (d1.X * d1.X) / this._majorSq + (d1.Y * d1.Y) / this._minorSq - 1.0;

            if (Math.Abs(A) < Tolerance)
                return result;

            var discriminant = B * B - 4.0 * A * C;
            if (discriminant < 0)
                return result;

            var sqrtD = Math.Sqrt(discriminant);
            var t1 = (-B - sqrtD) / (2.0 * A);

            // t ∈ [0, 1] 表示交点在线段上
            if (t1 >= -Tolerance && t1 <= 1.0 + Tolerance)
            {
                var t1Clamped = Math.Max(0.0, Math.Min(1.0, t1));
                var localPt = new Point2D(d1.X + t1Clamped * dx, d1.Y + t1Clamped * dy);
                result.Add(this.LocalToWorld(localPt));
            }

            if (Math.Abs(discriminant) > Tolerance)
            {
                var t2 = (-B + sqrtD) / (2.0 * A);
                if (t2 >= -Tolerance && t2 <= 1.0 + Tolerance)
                {
                    var t2Clamped = Math.Max(0.0, Math.Min(1.0, t2));
                    var localPt = new Point2D(d1.X + t2Clamped * dx, d1.Y + t2Clamped * dy);
                    result.Add(this.LocalToWorld(localPt));
                }
            }

            // 按距离起点排序
            result.Sort((p1, p2) =>
            {
                var d1s = (p1.X - segStart.X) * (p1.X - segStart.X) + (p1.Y - segStart.Y) * (p1.Y - segStart.Y);
                var d2s = (p2.X - segStart.X) * (p2.X - segStart.X) + (p2.Y - segStart.Y) * (p2.Y - segStart.Y);
                return d1s.CompareTo(d2s);
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
            var corners = new[]
            {
                minPoint,
                new Point2D(maxPoint.X, minPoint.Y),
                maxPoint,
                new Point2D(minPoint.X, maxPoint.Y),
            };

            // 1. 四角是否都在椭圆内 → Inside
            int insideCount = 0;
            foreach (var corner in corners)
            {
                if (this.IsPointInside(corner))
                    insideCount++;
            }

            if (insideCount == 4)
                return ContainmentResult.Inside;

            if (insideCount > 0)
                return ContainmentResult.Intersects;

            // 2. 没有角在椭圆内 → 检查椭圆中心是否在包围盒内
            if (this._center.X >= minPoint.X - Tolerance && this._center.X <= maxPoint.X + Tolerance &&
                this._center.Y >= minPoint.Y - Tolerance && this._center.Y <= maxPoint.Y + Tolerance)
            {
                return ContainmentResult.Intersects;
            }

            // 3. 检查包围盒边是否与椭圆相交
            if (this.BoundingBoxIntersectsEllipse(minPoint, maxPoint))
                return ContainmentResult.Intersects;

            return ContainmentResult.Outside;
        }

        /// <inheritdoc />
        public IReadOnlyList<Point2D> GetApproximatePolygon()
        {
            const int samples = 128;
            var pts = new List<Point2D>(samples);
            var step = 2.0 * Math.PI / samples;
            for (int i = 0; i < samples; i++)
            {
                var angle = step * i;
                var localX = this._majorRadius * Math.Cos(angle);
                var localY = this._minorRadius * Math.Sin(angle);
                var worldPt = this.LocalToWorld(new Point2D(localX, localY));
                pts.Add(worldPt);
            }
            return pts;
        }

        /// <summary>
        ///     将 WCS 点变换到椭圆局部坐标系（逆旋转）.
        /// </summary>
        private Point2D WorldToLocal(Point2D world)
        {
            var dx = world.X - this._center.X;
            var dy = world.Y - this._center.Y;
            return new Point2D(
                dx * this._cosRot + dy * this._sinRot,
                -dx * this._sinRot + dy * this._cosRot);
        }

        /// <summary>
        ///     将椭圆局部坐标系的点变换回 WCS（正向旋转）.
        /// </summary>
        private Point2D LocalToWorld(Point2D local)
        {
            return new Point2D(
                this._center.X + local.X * this._cosRot - local.Y * this._sinRot,
                this._center.Y + local.X * this._sinRot + local.Y * this._cosRot);
        }

        /// <summary>
        ///     检查轴对齐包围盒是否与椭圆相交.
        /// </summary>
        private bool BoundingBoxIntersectsEllipse(Point2D minPoint, Point2D maxPoint)
        {
            // 找到包围盒上离椭圆中心最近的点，变换到局部坐标系
            var nearestX = Math.Max(minPoint.X, Math.Min(this._center.X, maxPoint.X));
            var nearestY = Math.Max(minPoint.Y, Math.Min(this._center.Y, maxPoint.Y));
            return this.IsPointInside(new Point2D(nearestX, nearestY));
        }
    }
}
