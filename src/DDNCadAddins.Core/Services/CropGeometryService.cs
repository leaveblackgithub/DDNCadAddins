using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     裁剪几何计算服务 - 纯逻辑，无 CAD 依赖.
    ///     提供射线法判断点与多边形关系、线段与多边形求交等核心几何算法.
    /// </summary>
    public class CropGeometryService : ICropGeometryService
    {
        private const double Tolerance = 1e-9;

        /// <inheritdoc />
        public bool IsPointInPolygon(Point2D point, IReadOnlyList<Point2D> polygonVertices)
        {
            if (polygonVertices == null || polygonVertices.Count < 3)
            {
                return false;
            }

            var inside = false;
            var n = polygonVertices.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var vi = polygonVertices[i];
                var vj = polygonVertices[j];

                // 点在边上
                if (IsPointOnSegment(point, vj, vi))
                {
                    return true;
                }

                // 射线法：从 point 向右发射水平射线，统计与多边形边的交点
                if ((vi.Y > point.Y) != (vj.Y > point.Y))
                {
                    var intersectX = vj.X + (point.Y - vj.Y) * (vi.X - vj.X) / (vi.Y - vj.Y);
                    if (point.X < intersectX)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        /// <inheritdoc />
        public ContainmentResult ClassifyBoundingBox(
            Point2D minPoint,
            Point2D maxPoint,
            IReadOnlyList<Point2D> polygonVertices)
        {
            if (polygonVertices == null || polygonVertices.Count < 3)
            {
                return ContainmentResult.Outside;
            }

            // 1. 包围盒8个点（4个角 + 4条边中点）中是否有在内部的
            var corners = new List<Point2D>
            {
                minPoint,
                new Point2D(maxPoint.X, minPoint.Y),
                maxPoint,
                new Point2D(minPoint.X, maxPoint.Y),
            };

            var insideCount = 0;
            foreach (var corner in corners)
            {
                if (IsPointInPolygon(corner, polygonVertices))
                {
                    insideCount++;
                }
            }

            if (insideCount == 4)
            {
                // 所有角点都在多边形内（含边界），但如果有角点恰好在边界上，
                // 则包围盒不是严格内部，而是相交
                foreach (var corner in corners)
                {
                    if (IsPointOnPolygonBoundary(corner, polygonVertices))
                    {
                        return ContainmentResult.Intersects;
                    }
                }

                return ContainmentResult.Inside;
            }

            if (insideCount == 0)
            {
                // 2. 没有角点在内部，检查多边形顶点是否在包围盒内（完全包围情况）
                foreach (var vert in polygonVertices)
                {
                    if (IsPointInBoundingBox(vert, minPoint, maxPoint))
                    {
                        return ContainmentResult.Intersects;
                    }
                }

                // 3. 检查包围盒边是否与多边形边相交
                foreach (var edge in this.GetBoundingBoxEdges(minPoint, maxPoint))
                {
                    var intersections = this.FindLineSegmentIntersections(edge.Item1, edge.Item2, polygonVertices);
                    if (intersections.Count > 0)
                    {
                        return ContainmentResult.Intersects;
                    }
                }

                return ContainmentResult.Outside;
            }

            // insideCount 在 (0, 4) 范围内，肯定有交点
            return ContainmentResult.Intersects;
        }

        /// <inheritdoc />
        public List<Point2D> FindLineSegmentIntersections(
            Point2D segStart,
            Point2D segEnd,
            IReadOnlyList<Point2D> polygonVertices)
        {
            var result = new List<Point2D>();
            if (polygonVertices == null || polygonVertices.Count < 3)
            {
                return result;
            }

            var n = polygonVertices.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = polygonVertices[i];
                var pj = polygonVertices[j];

                if (this.TryGetSegmentIntersection(segStart, segEnd, pj, pi, out var intersection))
                {
                    result.Add(intersection);
                }
                else
                {
                    var overlapPts = GetCollinearOverlap(segStart, segEnd, pj, pi);
                    if (overlapPts != null) { result.AddRange(overlapPts); }
                }
            }

            // 去重：角点可能被相邻边重复返回
            var deduped = new List<Point2D>(result.Count);
            foreach (var pt in result)
            {
                var isDuplicate = false;
                foreach (var existing in deduped)
                {
                    if (DistanceSquared(pt, existing) < Tolerance * Tolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    deduped.Add(pt);
                }
            }

            return this.SortPointsAlongLine(segStart, deduped);
        }

        /// <inheritdoc />
        public List<Point2D> SortPointsAlongLine(Point2D startPoint, List<Point2D> points)
        {
            if (points == null || points.Count <= 1)
            {
                return points ?? new List<Point2D>();
            }

            // 使用参数 t 排序：t = (p - startPoint) 在直线方向上的投影长度
            points.Sort((a, b) =>
            {
                var ta = DistanceSquared(startPoint, a);
                var tb = DistanceSquared(startPoint, b);
                return ta.CompareTo(tb);
            });

            return points;
        }

        /// <inheritdoc />
        public bool TryGetSegmentIntersection(
            Point2D p1, Point2D p2,
            Point2D p3, Point2D p4,
            out Point2D intersection)
        {
            intersection = default(Point2D);

            var d1x = p2.X - p1.X;
            var d1y = p2.Y - p1.Y;
            var d2x = p4.X - p3.X;
            var d2y = p4.Y - p3.Y;

            var cross = d1x * d2y - d1y * d2x;

            // 平行或共线
            if (Math.Abs(cross) < Tolerance)
            {
                return false;
            }

            var dx = p3.X - p1.X;
            var dy = p3.Y - p1.Y;

            var t = (dx * d2y - dy * d2x) / cross;
            var u = (dx * d1y - dy * d1x) / cross;

            // 交点在两条线段上（含端点）
            if (t >= -Tolerance && t <= 1.0 + Tolerance &&
                u >= -Tolerance && u <= 1.0 + Tolerance)
            {
                intersection = new Point2D(p1.X + t * d1x, p1.Y + t * d1y);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     判断点是否在线段上（含端点）.
        /// </summary>
        private static bool IsPointOnSegment(Point2D point, Point2D segStart, Point2D segEnd)
        {
            // 叉积=0 表示共线
            var cross = (point.Y - segStart.Y) * (segEnd.X - segStart.X) -
                        (point.X - segStart.X) * (segEnd.Y - segStart.Y);

            if (Math.Abs(cross) > Tolerance)
            {
                return false;
            }

            // 点在线段的包围盒内
            var dotProduct = (point.X - segStart.X) * (segEnd.X - segStart.X) +
                             (point.Y - segStart.Y) * (segEnd.Y - segStart.Y);
            if (dotProduct < 0)
            {
                return false;
            }

            var squaredLength = DistanceSquared(segStart, segEnd);
            if (dotProduct > squaredLength)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///     判断点是否在轴对齐包围盒内（含边界）.
        /// </summary>
        private static bool IsPointInBoundingBox(Point2D point, Point2D minPoint, Point2D maxPoint)
        {
            return point.X >= minPoint.X - Tolerance &&
                   point.X <= maxPoint.X + Tolerance &&
                   point.Y >= minPoint.Y - Tolerance &&
                   point.Y <= maxPoint.Y + Tolerance;
        }

        /// <summary>
        ///     判断点是否在多边形的任何边上（含端点）.
        /// </summary>
        private static bool IsPointOnPolygonBoundary(Point2D point, IReadOnlyList<Point2D> polygonVertices)
        {
            var n = polygonVertices.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (IsPointOnSegment(point, polygonVertices[j], polygonVertices[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     获取包围盒的四条边.
        /// </summary>
        private List<Tuple<Point2D, Point2D>> GetBoundingBoxEdges(Point2D minPoint, Point2D maxPoint)
        {
            var p1 = minPoint;
            var p2 = new Point2D(maxPoint.X, minPoint.Y);
            var p3 = maxPoint;
            var p4 = new Point2D(minPoint.X, maxPoint.Y);

            return new List<Tuple<Point2D, Point2D>>
            {
                Tuple.Create(p1, p2),
                Tuple.Create(p2, p3),
                Tuple.Create(p3, p4),
                Tuple.Create(p4, p1),
            };
        }

        /// <summary>
        ///     计算两点距离的平方.
        /// </summary>
        private static double DistanceSquared(Point2D a, Point2D b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
        }

        /// <summary>
        ///     检测两段是否共线重叠，返回重叠端点作为交点.
        ///     多段线与边界边平行时，非重叠部分的中点能正确判定内外.
        /// </summary>
        private static List<Point2D> GetCollinearOverlap(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
        {
            var d1x = p2.X - p1.X;
            var d1y = p2.Y - p1.Y;
            var lenSq = d1x * d1x + d1y * d1y;
            if (lenSq < Tolerance) return null;

            // p3, p4 是否在 p1→p2 直线上
            if (Math.Abs(d1x * (p3.Y - p1.Y) - d1y * (p3.X - p1.X)) > Tolerance) return null;
            if (Math.Abs(d1x * (p4.Y - p1.Y) - d1y * (p4.X - p1.X)) > Tolerance) return null;

            // 投影到 p1→p2 方向
            var t3 = ((p3.X - p1.X) * d1x + (p3.Y - p1.Y) * d1y) / lenSq;
            var t4 = ((p4.X - p1.X) * d1x + (p4.Y - p1.Y) * d1y) / lenSq;
            if (t3 > t4) { var tmp = t3; t3 = t4; t4 = tmp; }

            // 与 [0,1] 求交
            var tMin = Math.Max(0, t3);
            var tMax = Math.Min(1, t4);
            if (tMax - tMin < Tolerance) return null;

            // 始终返回重叠区间的两个端点作为交点，
            // 如果端点恰好与 segStart/segEnd 重合也无妨——调用方 FindLineSegmentIntersections 会做去重
            var result = new List<Point2D>
            {
                new Point2D(p1.X + tMin * d1x, p1.Y + tMin * d1y),
                new Point2D(p1.X + tMax * d1x, p1.Y + tMax * d1y),
            };
            return result;
        }
    }
}