using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     精确曲线差集服务 — 计算 A \ B（曲线 A 减去与曲线 B 的交集）.
    ///     <para>
    ///         核心算法（类似 CROP 线段分析）：
    ///         1. 将曲线 A 和曲线 B 分别拆分为原子边（直线/圆弧/椭圆弧）.
    ///         2. 对 A 的每条边，用 B 的 <see cref="ICropBoundary"/> 精确求交，按交点拆分为子曲线.
    ///         3. 对 B 的每条边，用 A 的 <see cref="ICropBoundary"/> 精确求交，按交点拆分为子曲线.
    ///         4. 保留 A 中不在 B 内部的子曲线（可在 B 边界上）.
    ///         5. 保留 B 中在 A 内部的子曲线（反向，标记为 Clip，形成差集边界）.
    ///         6. 将保留的子曲线头尾相连成闭合环（支持正向和反向匹配）.
    ///     </para>
    ///     纯数学运算，无 AutoCAD 依赖.
    /// </summary>
    public class CurveSubtractService
    {
        private const double Tol = 1e-9;
        private const double MatchTol = 1e-6;

        /// <summary>
        ///     计算精确差集 A \ B.
        /// </summary>
        /// <param name="subjectEdges">曲线 A 的原子边列表.</param>
        /// <param name="subjectBoundary">曲线 A 的精确裁剪边界（用于 B 的求交和包含测试）.</param>
        /// <param name="clipEdges">曲线 B 的原子边列表.</param>
        /// <param name="clipBoundary">曲线 B 的精确裁剪边界（用于 A 的求交和包含测试）.</param>
        /// <returns>差集结果（0 个或多个闭合环）.</returns>
        public OpResult<ExactSubtractResult> Subtract(
            IReadOnlyList<ExactSegment> subjectEdges,
            ICropBoundary subjectBoundary,
            IReadOnlyList<ExactSegment> clipEdges,
            ICropBoundary clipBoundary)
        {
            try
            {
                if (subjectEdges == null || subjectEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Subject 边列表为空");

                if (subjectBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("Subject 边界为空");

                if (clipEdges == null || clipEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边列表为空");

                if (clipBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边界为空");

                // ── 1. 拆分 A 的边，保留不在 B 内部的子段 ──────────────
                var keptFromA = new List<ExactSegment>();

                foreach (var edge in subjectEdges)
                {
                    var subSegments = SplitEdgeByBoundary(edge, clipBoundary);
                    foreach (var sub in subSegments)
                    {
                        if (!IsSegmentInsideBoundary(sub, clipBoundary))
                            keptFromA.Add(sub);
                    }
                }

                // ── 2. 拆分 B 的边，保留在 A 内部的子段（反向，标记 Clip） ──
                var keptFromB = new List<ExactSegment>();

                foreach (var edge in clipEdges)
                {
                    var subSegments = SplitEdgeByBoundary(edge, subjectBoundary);
                    foreach (var sub in subSegments)
                    {
                        if (IsSegmentInsideBoundary(sub, subjectBoundary))
                        {
                            var reversed = ReverseSegment(sub);
                            reversed.Source = SegmentSource.Clip;
                            keptFromB.Add(reversed);
                        }
                    }
                }

                // ── 3. 合并并连接成闭合环 ──────────────────────────────
                var allKept = new List<ExactSegment>(keptFromA.Count + keptFromB.Count);
                allKept.AddRange(keptFromA);
                allKept.AddRange(keptFromB);

                if (allKept.Count == 0)
                    return OpResult<ExactSubtractResult>.Success(new ExactSubtractResult());

                var loops = ChainSegmentsIntoLoops(allKept);

                var finalResult = new ExactSubtractResult();
                foreach (var loop in loops)
                {
                    if (loop.Count >= 1)
                        finalResult.Loops.Add(loop);
                }

                return OpResult<ExactSubtractResult>.Success(finalResult);
            }
            catch (Exception ex)
            {
                return OpResult<ExactSubtractResult>.Fail(
                    $"精确差集计算失败: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  逐边求交与切分
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     将一条原子边按与 clip 边界的交点切分为子线段.
        ///     返回所有子线段（含交点端点），保留/丢弃由调用方决定.
        /// </summary>
        private List<ExactSegment> SplitEdgeByBoundary(
            ExactSegment edge, ICropBoundary boundary)
        {
            // 获取边的采样点（用于求交的直线段近似）
            var edgePoints = edge.ToPolylinePoints();
            if (edgePoints.Count < 2)
                return new List<ExactSegment> { edge };

            // 收集所有交点参数（沿边方向的归一化参数 t ∈ [0,1]）
            var cutParams = new List<double>();

            for (int i = 0; i < edgePoints.Count - 1; i++)
            {
                var p1 = edgePoints[i];
                var p2 = edgePoints[i + 1];
                var intersections = boundary.FindLineIntersections(p1, p2);

                foreach (var ix in intersections)
                {
                    double t = ParamAlongEdge(edge, edgePoints, i, ix, p1, p2);
                    if (t > Tol && t < 1.0 - Tol)
                        cutParams.Add(t);
                }
            }

            // 去重并排序
            cutParams.Sort();
            var uniqueParams = new List<double>();
            foreach (var t in cutParams)
            {
                if (uniqueParams.Count == 0 || t - uniqueParams[uniqueParams.Count - 1] > Tol)
                    uniqueParams.Add(t);
            }

            // 构建切分节点列表：0, t1, t2, ..., 1
            var nodes = new List<double> { 0.0 };
            nodes.AddRange(uniqueParams);
            nodes.Add(1.0);

            // 逐子段生成 ExactSegment
            var result = new List<ExactSegment>();
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                double tStart = nodes[i];
                double tEnd = nodes[i + 1];
                if (tEnd - tStart < Tol) continue;

                var subSegment = CreateSubSegment(edge, tStart, tEnd);
                if (subSegment != null)
                    result.Add(subSegment);
            }

            return result;
        }

        /// <summary>
        ///     计算交点沿边方向的归一化参数 t ∈ [0,1].
        /// </summary>
        private static double ParamAlongEdge(
            ExactSegment edge, List<Point2D> edgePoints,
            int segIndex, Point2D intersection,
            Point2D segStart, Point2D segEnd)
        {
            // 在当前采样段内的局部参数
            double dx = segEnd.X - segStart.X;
            double dy = segEnd.Y - segStart.Y;
            double segLenSq = dx * dx + dy * dy;
            if (segLenSq < Tol) return 0;

            double localT = ((intersection.X - segStart.X) * dx +
                             (intersection.Y - segStart.Y) * dy) / segLenSq;
            localT = Math.Max(0.0, Math.Min(1.0, localT));

            // 映射到全局参数 [0, 1]
            int totalSegs = edgePoints.Count - 1;
            double globalT = (segIndex + localT) / totalSegs;
            return Math.Max(0.0, Math.Min(1.0, globalT));
        }

        /// <summary>
        ///     根据参数范围 [tStart, tEnd] 从原始边创建子线段.
        ///     保持原始边的曲线类型和参数。
        /// </summary>
        private static ExactSegment CreateSubSegment(
            ExactSegment edge, double tStart, double tEnd)
        {
            switch (edge.SegmentType)
            {
                case ExactSegmentType.Line:
                    return CreateSubLine(edge, tStart, tEnd);

                case ExactSegmentType.Arc:
                    return CreateSubArc(edge, tStart, tEnd);

                case ExactSegmentType.Ellipse:
                    return CreateSubEllipse(edge, tStart, tEnd);

                default:
                    return null;
            }
        }

        /// <summary>创建直线子段.</summary>
        private static ExactSegment CreateSubLine(
            ExactSegment edge, double tStart, double tEnd)
        {
            double sx = edge.Start.X + (edge.End.X - edge.Start.X) * tStart;
            double sy = edge.Start.Y + (edge.End.Y - edge.Start.Y) * tStart;
            double ex = edge.Start.X + (edge.End.X - edge.Start.X) * tEnd;
            double ey = edge.Start.Y + (edge.End.Y - edge.Start.Y) * tEnd;

            return new ExactSegment
            {
                Source = edge.Source,
                SegmentType = ExactSegmentType.Line,
                Start = new Point2D(sx, sy),
                End = new Point2D(ex, ey)
            };
        }

        /// <summary>创建圆弧子段.</summary>
        private static ExactSegment CreateSubArc(
            ExactSegment edge, double tStart, double tEnd)
        {
            // 参数化角度插值
            double fullSpan = edge.ArcIsClockwise
                ? edge.ArcStartAngle - edge.ArcEndAngle
                : edge.ArcEndAngle - edge.ArcStartAngle;
            if (fullSpan < 0) fullSpan += 2.0 * Math.PI;

            double dir = edge.ArcIsClockwise ? -1.0 : 1.0;
            double subStartAngle = edge.ArcStartAngle + dir * fullSpan * tStart;
            double subEndAngle = edge.ArcStartAngle + dir * fullSpan * tEnd;

            double sx = edge.ArcCenter.X + edge.ArcRadius * Math.Cos(subStartAngle);
            double sy = edge.ArcCenter.Y + edge.ArcRadius * Math.Sin(subStartAngle);
            double ex = edge.ArcCenter.X + edge.ArcRadius * Math.Cos(subEndAngle);
            double ey = edge.ArcCenter.Y + edge.ArcRadius * Math.Sin(subEndAngle);

            return new ExactSegment
            {
                Source = edge.Source,
                SegmentType = ExactSegmentType.Arc,
                Start = new Point2D(sx, sy),
                End = new Point2D(ex, ey),
                ArcCenter = edge.ArcCenter,
                ArcRadius = edge.ArcRadius,
                ArcStartAngle = subStartAngle,
                ArcEndAngle = subEndAngle,
                ArcIsClockwise = edge.ArcIsClockwise
            };
        }

        /// <summary>创建椭圆弧子段.</summary>
        private static ExactSegment CreateSubEllipse(
            ExactSegment edge, double tStart, double tEnd)
        {
            double fullSpan = edge.EllipseIsClockwise
                ? edge.EllipseStartAngle - edge.EllipseEndAngle
                : edge.EllipseEndAngle - edge.EllipseStartAngle;
            if (fullSpan < 0) fullSpan += 2.0 * Math.PI;

            double dir = edge.EllipseIsClockwise ? -1.0 : 1.0;
            double subStartAngle = edge.EllipseStartAngle + dir * fullSpan * tStart;
            double subEndAngle = edge.EllipseStartAngle + dir * fullSpan * tEnd;

            double cosRot = Math.Cos(edge.EllipseRotation);
            double sinRot = Math.Sin(edge.EllipseRotation);

            double sxLocal = edge.EllipseMajorRadius * Math.Cos(subStartAngle);
            double syLocal = edge.EllipseMinorRadius * Math.Sin(subStartAngle);
            double exLocal = edge.EllipseMajorRadius * Math.Cos(subEndAngle);
            double eyLocal = edge.EllipseMinorRadius * Math.Sin(subEndAngle);

            return new ExactSegment
            {
                Source = edge.Source,
                SegmentType = ExactSegmentType.Ellipse,
                Start = new Point2D(
                    edge.EllipseCenter.X + sxLocal * cosRot - syLocal * sinRot,
                    edge.EllipseCenter.Y + sxLocal * sinRot + syLocal * cosRot),
                End = new Point2D(
                    edge.EllipseCenter.X + exLocal * cosRot - eyLocal * sinRot,
                    edge.EllipseCenter.Y + exLocal * sinRot + eyLocal * cosRot),
                EllipseCenter = edge.EllipseCenter,
                EllipseMajorRadius = edge.EllipseMajorRadius,
                EllipseMinorRadius = edge.EllipseMinorRadius,
                EllipseRotation = edge.EllipseRotation,
                EllipseStartAngle = subStartAngle,
                EllipseEndAngle = subEndAngle,
                EllipseIsClockwise = edge.EllipseIsClockwise
            };
        }

        // ──────────────────────────────────────────────────────────────
        //  中点包含测试
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     判断子线段是否在边界内部（用中点测试，含边界）.
        /// </summary>
        private static bool IsSegmentInsideBoundary(
            ExactSegment segment, ICropBoundary boundary)
        {
            var midPt = GetSegmentMidpoint(segment);
            return boundary.IsPointInside(midPt);
        }

        /// <summary>
        ///     获取子线段的中点（参数化曲线的中参数点）.
        /// </summary>
        private static Point2D GetSegmentMidpoint(ExactSegment segment)
        {
            switch (segment.SegmentType)
            {
                case ExactSegmentType.Line:
                    return new Point2D(
                        (segment.Start.X + segment.End.X) / 2.0,
                        (segment.Start.Y + segment.End.Y) / 2.0);

                case ExactSegmentType.Arc:
                    {
                        double midAngle = (segment.ArcStartAngle + segment.ArcEndAngle) / 2.0;
                        return new Point2D(
                            segment.ArcCenter.X + segment.ArcRadius * Math.Cos(midAngle),
                            segment.ArcCenter.Y + segment.ArcRadius * Math.Sin(midAngle));
                    }

                case ExactSegmentType.Ellipse:
                    {
                        double midAngle = (segment.EllipseStartAngle + segment.EllipseEndAngle) / 2.0;
                        double cosRot = Math.Cos(segment.EllipseRotation);
                        double sinRot = Math.Sin(segment.EllipseRotation);
                        double lx = segment.EllipseMajorRadius * Math.Cos(midAngle);
                        double ly = segment.EllipseMinorRadius * Math.Sin(midAngle);
                        return new Point2D(
                            segment.EllipseCenter.X + lx * cosRot - ly * sinRot,
                            segment.EllipseCenter.Y + lx * sinRot + ly * cosRot);
                    }

                default:
                    return new Point2D(
                        (segment.Start.X + segment.End.X) / 2.0,
                        (segment.Start.Y + segment.End.Y) / 2.0);
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  段反转
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     反转精确段的方向（起点↔终点，角度↔，方向取反）.
        ///     用于 B 的子段反向后加入差集结果环.
        /// </summary>
        private static ExactSegment ReverseSegment(ExactSegment seg)
        {
            var reversed = new ExactSegment
            {
                Source = seg.Source,
                SegmentType = seg.SegmentType,
                Start = seg.End,
                End = seg.Start
            };

            switch (seg.SegmentType)
            {
                case ExactSegmentType.Arc:
                    reversed.ArcCenter = seg.ArcCenter;
                    reversed.ArcRadius = seg.ArcRadius;
                    reversed.ArcStartAngle = seg.ArcEndAngle;
                    reversed.ArcEndAngle = seg.ArcStartAngle;
                    reversed.ArcIsClockwise = !seg.ArcIsClockwise;
                    break;

                case ExactSegmentType.Ellipse:
                    reversed.EllipseCenter = seg.EllipseCenter;
                    reversed.EllipseMajorRadius = seg.EllipseMajorRadius;
                    reversed.EllipseMinorRadius = seg.EllipseMinorRadius;
                    reversed.EllipseRotation = seg.EllipseRotation;
                    reversed.EllipseStartAngle = seg.EllipseEndAngle;
                    reversed.EllipseEndAngle = seg.EllipseStartAngle;
                    reversed.EllipseIsClockwise = !seg.EllipseIsClockwise;
                    break;
            }

            return reversed;
        }

        // ──────────────────────────────────────────────────────────────
        //  连接子段为闭合环（支持正向和反向匹配）
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     将保留的子线段按端点连接为闭合环.
        ///     支持正向匹配（seg.Start == currentEnd）和反向匹配（seg.End == currentEnd）.
        /// </summary>
        private List<List<ExactSegment>> ChainSegmentsIntoLoops(
            List<ExactSegment> keptSegments)
        {
            var loops = new List<List<ExactSegment>>();
            var used = new bool[keptSegments.Count];

            for (int i = 0; i < keptSegments.Count; i++)
            {
                if (used[i]) continue;

                var loop = new List<ExactSegment>();
                used[i] = true;
                var current = keptSegments[i];
                loop.Add(current);
                var currentEnd = current.End;

                // 尝试连接后续子段
                for (int safety = 0; safety < keptSegments.Count * 2 + 10; safety++)
                {
                    // 检查是否回到环起点
                    if (PointsEqual(currentEnd, loop[0].Start, MatchTol))
                        break;

                    // 先尝试正向匹配（seg.Start == currentEnd）
                    int nextIdx = FindMatchingSegment(
                        keptSegments, used, currentEnd, false);

                    if (nextIdx >= 0)
                    {
                        used[nextIdx] = true;
                        current = keptSegments[nextIdx];
                        loop.Add(current);
                        currentEnd = current.End;
                    }
                    else
                    {
                        // 尝试反向匹配（seg.End == currentEnd → 反转后使用）
                        nextIdx = FindMatchingSegment(
                            keptSegments, used, currentEnd, true);

                        if (nextIdx >= 0)
                        {
                            used[nextIdx] = true;
                            current = ReverseSegment(keptSegments[nextIdx]);
                            loop.Add(current);
                            currentEnd = current.End;
                        }
                        else
                        {
                            // 无法继续连接 → 环结束
                            break;
                        }
                    }
                }

                loops.Add(loop);
            }

            return loops;
        }

        /// <summary>
        ///     在未使用的子段中查找端点与 currentEnd 匹配的段.
        /// </summary>
        /// <param name="reverse">false=正向匹配(Start)，true=反向匹配(End).</param>
        private static int FindMatchingSegment(
            List<ExactSegment> segments, bool[] used,
            Point2D currentEnd, bool reverse)
        {
            double tolSq = MatchTol * MatchTol;
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i]) continue;
                var seg = segments[i];
                Point2D checkPt = reverse ? seg.End : seg.Start;
                double dx = checkPt.X - currentEnd.X;
                double dy = checkPt.Y - currentEnd.Y;
                if (dx * dx + dy * dy < tolSq)
                    return i;
            }
            return -1;
        }

        // ──────────────────────────────────────────────────────────────
        //  辅助方法
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     判断两点是否相等（容差内）.
        /// </summary>
        private static bool PointsEqual(Point2D a, Point2D b, double tol)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy < tol * tol;
        }
    }
}
