using System;
using System.Collections.Generic;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     几何辅助工具类 — 精确数学计算，不依赖 CAD 对象.
    /// </summary>
    internal static class GeometryHelper
    {
        /// <summary>
        ///     直线与圆的交点（解析解）.
        /// </summary>
        public static List<CorePoint2D> LineCircleIntersection(
            double x1, double y1, double x2, double y2, double cx, double cy, double r)
        {
            var result = new List<CorePoint2D>();
            var dx = x2 - x1;
            var dy = y2 - y1;
            var fx = x1 - cx;
            var fy = y1 - cy;

            var a = dx * dx + dy * dy;
            if (Math.Abs(a) < 1e-12) return result;

            var b = 2.0 * (dx * fx + dy * fy);
            var c = fx * fx + fy * fy - r * r;
            var discriminant = b * b - 4.0 * a * c;
            if (discriminant < 0) return result;

            var sqrtD = Math.Sqrt(discriminant);
            var t1 = (-b - sqrtD) / (2.0 * a);
            result.Add(new CorePoint2D(x1 + t1 * dx, y1 + t1 * dy));

            if (Math.Abs(discriminant) > 1e-12)
            {
                var t2 = (-b + sqrtD) / (2.0 * a);
                result.Add(new CorePoint2D(x1 + t2 * dx, y1 + t2 * dy));
            }

            return result;
        }

        /// <summary>
        ///     点是否在线段上（含端点）.
        /// </summary>
        public static bool PointOnSegment(CorePoint2D pt, CorePoint2D a, CorePoint2D b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12) return false;
            var t = ((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / lenSq;
            return t >= -1e-10 && t <= 1.0 + 1e-10;
        }

        /// <summary>
        ///     角度归一化到 [0, 2π).
        /// </summary>
        public static double NormalizeAngle0To2Pi(double angle)
        {
            while (angle < 0) angle += 2.0 * Math.PI;
            while (angle >= 2.0 * Math.PI) angle -= 2.0 * Math.PI;
            return angle;
        }

        /// <summary>
        ///     判断角度是否在 [start, end] 范围内（考虑跨 2π 的情况）.
        /// </summary>
        public static bool AngleInRange(double angle, double start, double end)
        {
            angle = NormalizeAngle0To2Pi(angle);
            start = NormalizeAngle0To2Pi(start);
            end = NormalizeAngle0To2Pi(end);

            if (end >= start)
                return angle >= start - 1e-9 && angle <= end + 1e-9;
            else
                return angle >= start - 1e-9 || angle <= end + 1e-9;
        }

        /// <summary>
        ///     将角度归一化到 [start, end] 范围内.
        /// </summary>
        public static double NormalizeAngle(double angle, double start, double end)
        {
            angle = NormalizeAngle0To2Pi(angle);
            start = NormalizeAngle0To2Pi(start);
            end = NormalizeAngle0To2Pi(end);

            if (end >= start)
            {
                if (angle > end) angle -= 2.0 * Math.PI;
                if (angle < start) angle += 2.0 * Math.PI;
            }
            else
            {
                if (angle > end && angle < start)
                {
                    if (angle - end > start - angle) angle -= 2.0 * Math.PI;
                    else angle += 2.0 * Math.PI;
                }
            }

            return angle;
        }

        /// <summary>
        ///     判断点是否与列表中已有点重复.
        /// </summary>
        public static bool IsDuplicatePt(CorePoint2D pt, List<CorePoint2D> existing, double tol = 1e-8)
        {
            foreach (var e in existing)
            {
                var dx = pt.X - e.X;
                var dy = pt.Y - e.Y;
                if ((dx * dx + dy * dy) < tol * tol) return true;
            }
            return false;
        }
    }
}