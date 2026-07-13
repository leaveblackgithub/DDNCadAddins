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
        private const double MatchTol = 5e-4;

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

                // ── 2. 拆分 B 的边，保留在 A 内部（非边界上）的子段（反向，标记 Clip） ──
                //    共线子段（在 A 边界上）不保留，避免差集结果中出现多余的共边
                var keptFromB = new List<ExactSegment>();

                foreach (var edge in clipEdges)
                {
                    var subSegments = SplitEdgeByBoundary(edge, subjectBoundary);
                    foreach (var sub in subSegments)
                    {
                        if (IsSegmentInsideBoundary(sub, subjectBoundary) &&
                            !IsSegmentOnBoundaryEdge(sub, subjectEdges))
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

        /// <summary>
        ///     计算多个 Subject 减去同一个 Clip 的差集：(A₁ ∪ A₂ ∪ ...) \ B.
        ///     <para>
        ///         对每个 Subject Aᵢ 独立执行 Aᵢ \ B 差集运算，
        ///         汇总所有结果环。各 Subject 结果互不影响。
        ///     </para>
        /// </summary>
        /// <param name="subjects">Subject 列表（每条包含边列表和边界）.</param>
        /// <param name="clipEdges">Clip 曲线 B 的原子边列表.</param>
        /// <param name="clipBoundary">Clip 曲线 B 的精确裁剪边界.</param>
        /// <returns>差集结果（0 个或多个闭合环）.</returns>
        public OpResult<ExactSubtractResult> SubtractMultiSubject(
            IReadOnlyList<(IReadOnlyList<ExactSegment> Edges, ICropBoundary Boundary)> subjects,
            IReadOnlyList<ExactSegment> clipEdges,
            ICropBoundary clipBoundary)
        {
            try
            {
                if (subjects == null || subjects.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Subject 列表为空");
                if (clipEdges == null || clipEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边列表为空");
                if (clipBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边界为空");

                var allLoops = new List<List<ExactSegment>>();

                foreach (var subject in subjects)
                {
                    if (subject.Edges == null || subject.Edges.Count == 0)
                        continue;
                    if (subject.Boundary == null)
                        continue;

                    var result = this.Subtract(
                        subject.Edges, subject.Boundary,
                        clipEdges, clipBoundary);

                    if (result.IsSuccess && !result.Data.IsEmpty)
                    {
                        allLoops.AddRange(result.Data.Loops);
                    }
                }

                var finalResult = new ExactSubtractResult();
                foreach (var loop in allLoops)
                {
                    if (loop.Count >= 1)
                        finalResult.Loops.Add(loop);
                }

                return OpResult<ExactSubtractResult>.Success(finalResult);
            }
            catch (Exception ex)
            {
                return OpResult<ExactSubtractResult>.Fail(
                    $"多 Subject 差集计算失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     计算精确交集 A ∩ B.
        ///     <para>
        ///         与 <see cref="Subtract"/> 的区别：
        ///         Subtract 保留 A 中不在 B 内部的子段 + B 中在 A 内部的子段（反向标记 Clip）；
        ///         Intersect 保留 A 中在 B 内部的子段 + B 中在 A 内部的子段（不反向标记 Clip）。
        ///     </para>
        /// </summary>
        /// <param name="subjectEdges">曲线 A 的原子边列表.</param>
        /// <param name="subjectBoundary">曲线 A 的精确裁剪边界.</param>
        /// <param name="clipEdges">曲线 B 的原子边列表.</param>
        /// <param name="clipBoundary">曲线 B 的精确裁剪边界.</param>
        /// <returns>交集结果（0 个或多个闭合环）.</returns>
        public OpResult<ExactSubtractResult> Intersect(
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

                // ── 1. 拆分 A 的边，保留在 B 内部的子段 ──────────────
                var keptFromA = new List<ExactSegment>();

                foreach (var edge in subjectEdges)
                {
                    var subSegments = SplitEdgeByBoundary(edge, clipBoundary);
                    foreach (var sub in subSegments)
                    {
                        if (IsSegmentInsideBoundary(sub, clipBoundary))
                            keptFromA.Add(sub);
                    }
                }

                // ── 2. 拆分 B 的边，保留在 A 内部的子段（不反向，标记 Clip） ──
                //    与 Subtract 不同：不反向，因为交集边界直接沿 B 的原始方向
                var keptFromB = new List<ExactSegment>();

                foreach (var edge in clipEdges)
                {
                    var subSegments = SplitEdgeByBoundary(edge, subjectBoundary);
                    foreach (var sub in subSegments)
                    {
                        if (IsSegmentInsideBoundary(sub, subjectBoundary) &&
                            !IsSegmentOnBoundaryEdge(sub, subjectEdges))
                        {
                            sub.Source = SegmentSource.Clip;
                            keptFromB.Add(sub);
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
                    $"精确交集计算失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     计算多个 Subject 与同一个 Clip 的交集：(A₁ ∩ B) ∪ (A₂ ∩ B) ∪ ...
        ///     <para>
        ///         对每个 Subject Aᵢ 独立执行 Aᵢ ∩ B 交集运算，
        ///         汇总所有结果环。各 Subject 结果互不影响。
        ///     </para>
        /// </summary>
        /// <param name="subjects">Subject 列表（每条包含边列表和边界）.</param>
        /// <param name="clipEdges">Clip 曲线 B 的原子边列表.</param>
        /// <param name="clipBoundary">Clip 曲线 B 的精确裁剪边界.</param>
        /// <returns>交集结果（0 个或多个闭合环）.</returns>
        public OpResult<ExactSubtractResult> IntersectMultiSubject(
            IReadOnlyList<(IReadOnlyList<ExactSegment> Edges, ICropBoundary Boundary)> subjects,
            IReadOnlyList<ExactSegment> clipEdges,
            ICropBoundary clipBoundary)
        {
            try
            {
                if (subjects == null || subjects.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Subject 列表为空");
                if (clipEdges == null || clipEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边列表为空");
                if (clipBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边界为空");

                var allLoops = new List<List<ExactSegment>>();

                foreach (var subject in subjects)
                {
                    if (subject.Edges == null || subject.Edges.Count == 0)
                        continue;
                    if (subject.Boundary == null)
                        continue;

                    var result = this.Intersect(
                        subject.Edges, subject.Boundary,
                        clipEdges, clipBoundary);

                    if (result.IsSuccess && !result.Data.IsEmpty)
                    {
                        allLoops.AddRange(result.Data.Loops);
                    }
                }

                var finalResult = new ExactSubtractResult();
                foreach (var loop in allLoops)
                {
                    if (loop.Count >= 1)
                        finalResult.Loops.Add(loop);
                }

                return OpResult<ExactSubtractResult>.Success(finalResult);
            }
            catch (Exception ex)
            {
                return OpResult<ExactSubtractResult>.Fail(
                    $"多 Subject 交集计算失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     计算"外环+内环（孔洞）"与一条 Clip 曲线的裁剪运算，正确处理
        ///     Clip 同时与外环、内环相交的场景（凹字形结果）.
        ///     <para>
        ///         语义：内环（孔洞）区域始终不属于结果，无论裁剪方向如何；
        ///         裁剪方向只影响外环与 Clip 的取舍：
        ///         <list type="bullet">
        ///             <item>keepInside=true（保留内部/交集）：结果 = 外环以内 ∩ Clip 以内 ∩ 内环以外.</item>
        ///             <item>keepInside=false（保留外部/差集）：结果 = 外环以内 ∩ Clip 以外 ∩ 内环以外.</item>
        ///         </list>
        ///     </para>
        ///     <para>
        ///         算法：对外环边、Clip 边、内环边分别按另外两个边界级联切分，
        ///         保留满足对应条件的子段（外环子段与 Clip 子段方向不变/按需反向，
        ///         内环子段始终反向标记为 Clip，代表挖孔边界），最终统一连接成闭合环.
        ///     </para>
        /// </summary>
        /// <param name="outerEdges">外环原子边列表.</param>
        /// <param name="outerBoundary">外环精确裁剪边界.</param>
        /// <param name="holeEdges">内环（孔洞）原子边列表.</param>
        /// <param name="holeBoundary">内环（孔洞）精确裁剪边界.</param>
        /// <param name="clipEdges">Clip 曲线原子边列表.</param>
        /// <param name="clipBoundary">Clip 曲线精确裁剪边界.</param>
        /// <param name="keepInside">true=保留内部（外环∩Clip\内环），false=保留外部（外环\(Clip∪内环)）.</param>
        /// <returns>裁剪结果（0 个或多个闭合环）.</returns>
        public OpResult<ExactSubtractResult> CropRingWithHole(
            IReadOnlyList<ExactSegment> outerEdges,
            ICropBoundary outerBoundary,
            IReadOnlyList<ExactSegment> holeEdges,
            ICropBoundary holeBoundary,
            IReadOnlyList<ExactSegment> clipEdges,
            ICropBoundary clipBoundary,
            bool keepInside)
        {
            try
            {
                if (outerEdges == null || outerEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("外环边列表为空");
                if (outerBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("外环边界为空");
                if (holeEdges == null || holeEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("内环边列表为空");
                if (holeBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("内环边界为空");
                if (clipEdges == null || clipEdges.Count == 0)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边列表为空");
                if (clipBoundary == null)
                    return OpResult<ExactSubtractResult>.Fail("Clip 边界为空");

                // ── 1. 外环边：按 Clip + 内环级联切分，保留满足条件的子段 ──────
                var keptFromOuter = new List<ExactSegment>();
                foreach (var edge in outerEdges)
                {
                    var subs = SplitEdgeByMultipleBoundaries(edge, clipBoundary, holeBoundary);
                    foreach (var sub in subs)
                    {
                        bool insideClip = IsSegmentInsideBoundary(sub, clipBoundary);
                        bool insideHole = IsSegmentInsideBoundary(sub, holeBoundary);
                        bool keep = keepInside
                            ? insideClip && !insideHole
                            : !insideClip && !insideHole;
                        if (keep)
                            keptFromOuter.Add(sub);
                    }
                }

                // ── 2. Clip 边：按外环 + 内环级联切分，保留在外环内、内环外的子段 ──
                var keptFromClip = new List<ExactSegment>();
                foreach (var edge in clipEdges)
                {
                    var subs = SplitEdgeByMultipleBoundaries(edge, outerBoundary, holeBoundary);
                    foreach (var sub in subs)
                    {
                        bool insideOuter = IsSegmentInsideBoundary(sub, outerBoundary);
                        bool insideHole = IsSegmentInsideBoundary(sub, holeBoundary);
                        if (!insideOuter || insideHole)
                            continue;
                        if (IsSegmentOnBoundaryEdge(sub, outerEdges))
                            continue;

                        if (keepInside)
                        {
                            sub.Source = SegmentSource.Clip;
                            keptFromClip.Add(sub);
                        }
                        else
                        {
                            var reversed = ReverseSegment(sub);
                            reversed.Source = SegmentSource.Clip;
                            keptFromClip.Add(reversed);
                        }
                    }
                }

                // ── 3. 内环边：按外环 + Clip 级联切分，始终反向标记为挖孔边界 ──
                //    保留条件与外环自身条件一致（保证挖孔边界只出现在结果保留区域内）
                var keptFromHole = new List<ExactSegment>();
                foreach (var edge in holeEdges)
                {
                    var subs = SplitEdgeByMultipleBoundaries(edge, outerBoundary, clipBoundary);
                    foreach (var sub in subs)
                    {
                        bool insideOuter = IsSegmentInsideBoundary(sub, outerBoundary);
                        bool insideClip = IsSegmentInsideBoundary(sub, clipBoundary);
                        bool keepCondition = keepInside ? insideClip : !insideClip;
                        if (!insideOuter || !keepCondition)
                            continue;
                        if (IsSegmentOnBoundaryEdge(sub, outerEdges))
                            continue;

                        var reversed = ReverseSegment(sub);
                        reversed.Source = SegmentSource.Clip;
                        keptFromHole.Add(reversed);
                    }
                }

                // ── 4. 合并并连接成闭合环 ──────────────────────────────
                var allKept = new List<ExactSegment>(
                    keptFromOuter.Count + keptFromClip.Count + keptFromHole.Count);
                allKept.AddRange(keptFromOuter);
                allKept.AddRange(keptFromClip);
                allKept.AddRange(keptFromHole);

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
                    $"环形裁剪计算失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     将一条原子边依次按多个裁剪边界级联切分（先按第一个边界切分，
        ///     再对每个子段按第二个边界继续切分，以此类推）.
        ///     用于 <see cref="CropRingWithHole"/> 中需要同时按两个独立边界
        ///     （如外环+内环）切分同一条边的场景.
        /// </summary>
        /// <param name="edge">原始边.</param>
        /// <param name="boundaries">依次应用的裁剪边界列表.</param>
        /// <returns>级联切分后的最终子段列表.</returns>
        private List<ExactSegment> SplitEdgeByMultipleBoundaries(
            ExactSegment edge, params ICropBoundary[] boundaries)
        {
            var current = new List<ExactSegment> { edge };
            foreach (var boundary in boundaries)
            {
                var next = new List<ExactSegment>();
                foreach (var seg in current)
                    next.AddRange(SplitEdgeByBoundary(seg, boundary));
                current = next;
            }
            return current;
        }

        // ──────────────────────────────────────────────────────────────
        //  逐边求交与切分
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        ///     将一条原子边按与 clip 边界的交点切分为子线段.
        ///     返回所有子线段（含交点端点），保留/丢弃由调用方决定.
        ///     <para>
        ///         对于椭圆弧段，使用精确角度参数求交，避免采样折线
        ///         参数与角度参数不一致导致的切割错误。
        ///         所有子段的端点直接使用交点坐标，确保不同边切割
        ///         产生的子段在连接点处坐标完全一致。
        ///     </para>
        /// </summary>
        private List<ExactSegment> SplitEdgeByBoundary(
            ExactSegment edge, ICropBoundary boundary)
        {
            // 对于椭圆弧段，使用精确角度参数求交
            if (edge.SegmentType == ExactSegmentType.Ellipse)
            {
                return SplitEllipseEdgeByBoundary(edge, boundary);
            }

            // 对于圆弧段，使用精确角度参数求交（避免弦近似误差）
            if (edge.SegmentType == ExactSegmentType.Arc)
            {
                return SplitArcEdgeByBoundary(edge, boundary);
            }

            // 获取边的采样点（用于求交的直线段近似）
            var edgePoints = edge.ToPolylinePoints();
            if (edgePoints.Count < 2)
                return new List<ExactSegment> { edge };

            // 收集交点：按参数 t 排序的 (t, 交点坐标) 对
            var cutPoints = new List<KeyValuePair<double, Point2D>>();

            for (int i = 0; i < edgePoints.Count - 1; i++)
            {
                var p1 = edgePoints[i];
                var p2 = edgePoints[i + 1];
                var intersections = boundary.FindLineIntersections(p1, p2);

                foreach (var ix in intersections)
                {
                    double t = ParamAlongEdge(edge, edgePoints, i, ix, p1, p2);
                    if (t > Tol && t < 1.0 - Tol)
                        cutPoints.Add(new KeyValuePair<double, Point2D>(t, ix));
                }
            }

            // 公共处理：排序、去重、构建节点、生成子段
            return BuildSubSegmentsFromCutPoints(edge, cutPoints);
        }

        /// <summary>
        ///     圆弧段的专用切分方法 — 使用精确角度参数求交.
        ///     <para>
        ///         圆弧的弧长与角度参数是线性关系，但采样弦与圆弧之间存在偏差。
        ///         此方法对每个交点直接计算其在圆弧上的精确角度参数，
        ///         并直接使用交点坐标作为子段端点，确保与直线段子段的端点一致。
        ///     </para>
        /// </summary>
        private List<ExactSegment> SplitArcEdgeByBoundary(
            ExactSegment edge, ICropBoundary boundary)
        {
            // 计算圆弧的角度范围
            double fullSpan = edge.ArcIsClockwise
                ? edge.ArcStartAngle - edge.ArcEndAngle
                : edge.ArcEndAngle - edge.ArcStartAngle;
            if (fullSpan < 0) fullSpan += 2.0 * Math.PI;

            double dir = edge.ArcIsClockwise ? -1.0 : 1.0;

            // 在角度空间均匀采样，用于求交
            const int angleSamples = 64;
            var cutPoints = new List<KeyValuePair<double, Point2D>>();

            for (int i = 0; i < angleSamples; i++)
            {
                double t = (double)i / angleSamples;
                double angle = edge.ArcStartAngle + dir * fullSpan * t;
                double nextAngle = edge.ArcStartAngle + dir * fullSpan * ((double)(i + 1) / angleSamples);
                var p1 = new Point2D(
                    edge.ArcCenter.X + edge.ArcRadius * Math.Cos(angle),
                    edge.ArcCenter.Y + edge.ArcRadius * Math.Sin(angle));
                var p2 = new Point2D(
                    edge.ArcCenter.X + edge.ArcRadius * Math.Cos(nextAngle),
                    edge.ArcCenter.Y + edge.ArcRadius * Math.Sin(nextAngle));
                var intersections = boundary.FindLineIntersections(p1, p2);

                foreach (var ix in intersections)
                {
                    double angleT = ComputeArcIntersectionParam(edge, ix, dir, fullSpan);
                    if (angleT > Tol && angleT < 1.0 - Tol)
                        cutPoints.Add(new KeyValuePair<double, Point2D>(angleT, ix));
                }
            }

            return BuildSubSegmentsFromCutPoints(edge, cutPoints);
        }

        /// <summary>
        ///     计算交点在圆弧上的精确角度参数 t ∈ [0,1].
        ///     将交点从 WCS 变换到圆弧的极坐标系，使用 Atan2 计算精确角度，
        ///     再映射到弧段参数空间。
        /// </summary>
        private static double ComputeArcIntersectionParam(
            ExactSegment edge, Point2D intersection,
            double dir, double fullSpan)
        {
            // 计算交点相对于圆心的角度
            double angle = Math.Atan2(
                intersection.Y - edge.ArcCenter.Y,
                intersection.X - edge.ArcCenter.X);

            // 将角度映射到弧段参数空间 t ∈ [0,1]
            double angleOffset = angle - edge.ArcStartAngle;
            // 处理角度环绕
            if (dir > 0) // CCW
            {
                if (angleOffset < 0) angleOffset += 2.0 * Math.PI;
            }
            else // CW
            {
                if (angleOffset > 0) angleOffset -= 2.0 * Math.PI;
            }

            double t = angleOffset / (dir * fullSpan);
            return Math.Max(0.0, Math.Min(1.0, t));
        }

        /// <summary>
        ///     椭圆弧段的专用切分方法 — 使用精确角度参数求交.
        ///     <para>
        ///         椭圆弧的弧长与角度参数不是线性关系，因此不能使用
        ///         采样折线的线性参数来切割椭圆弧。此方法对每个交点
        ///         直接计算其在椭圆弧上的精确角度参数，并直接使用
        ///         交点坐标作为子段端点，确保与直线段子段的端点一致。
        ///     </para>
        /// </summary>
        private List<ExactSegment> SplitEllipseEdgeByBoundary(
            ExactSegment edge, ICropBoundary boundary)
        {
            // 计算椭圆弧的角度范围
            double fullSpan = edge.EllipseIsClockwise
                ? edge.EllipseStartAngle - edge.EllipseEndAngle
                : edge.EllipseEndAngle - edge.EllipseStartAngle;
            if (fullSpan < 0) fullSpan += 2.0 * Math.PI;

            double dir = edge.EllipseIsClockwise ? -1.0 : 1.0;
            double cosRot = Math.Cos(edge.EllipseRotation);
            double sinRot = Math.Sin(edge.EllipseRotation);

            // 在角度空间均匀采样，用于求交
            const int angleSamples = 128;
            var cutPoints = new List<KeyValuePair<double, Point2D>>();

            for (int i = 0; i < angleSamples; i++)
            {
                double t = (double)i / angleSamples;
                double angle = edge.EllipseStartAngle + dir * fullSpan * t;
                double nextAngle = edge.EllipseStartAngle + dir * fullSpan * ((double)(i + 1) / angleSamples);
                double lx = edge.EllipseMajorRadius * Math.Cos(angle);
                double ly = edge.EllipseMinorRadius * Math.Sin(angle);
                double nx = edge.EllipseMajorRadius * Math.Cos(nextAngle);
                double ny = edge.EllipseMinorRadius * Math.Sin(nextAngle);
                var p1 = new Point2D(
                    edge.EllipseCenter.X + lx * cosRot - ly * sinRot,
                    edge.EllipseCenter.Y + lx * sinRot + ly * cosRot);
                var p2 = new Point2D(
                    edge.EllipseCenter.X + nx * cosRot - ny * sinRot,
                    edge.EllipseCenter.Y + nx * sinRot + ny * cosRot);
                var intersections = boundary.FindLineIntersections(p1, p2);

                foreach (var ix in intersections)
                {
                    double angleT = ComputeEllipseIntersectionParam(edge, ix, cosRot, sinRot, dir, fullSpan);
                    if (angleT > Tol && angleT < 1.0 - Tol)
                        cutPoints.Add(new KeyValuePair<double, Point2D>(angleT, ix));
                }
            }

            return BuildSubSegmentsFromCutPoints(edge, cutPoints);
        }

        /// <summary>
        ///     计算交点在椭圆弧上的精确角度参数 t ∈ [0,1].
        ///     <para>
        ///         将交点从 WCS 变换到椭圆局部坐标系，然后使用 Atan2
        ///         计算精确角度，再映射到弧段参数空间。
        ///     </para>
        /// </summary>
        private static double ComputeEllipseIntersectionParam(
            ExactSegment edge, Point2D intersection,
            double cosRot, double sinRot, double dir, double fullSpan)
        {
            // 将交点变换到椭圆局部坐标系
            double dx = intersection.X - edge.EllipseCenter.X;
            double dy = intersection.Y - edge.EllipseCenter.Y;
            double localX = dx * cosRot + dy * sinRot;
            double localY = -dx * sinRot + dy * cosRot;

            // 在局部坐标系中计算精确角度（使用 Atan2，处理所有象限）
            double angle = Math.Atan2(localY / edge.EllipseMinorRadius,
                                      localX / edge.EllipseMajorRadius);

            // 将角度映射到弧段参数空间 t ∈ [0,1]
            double angleOffset = angle - edge.EllipseStartAngle;
            // 处理角度环绕
            if (dir > 0) // CCW
            {
                if (angleOffset < 0) angleOffset += 2.0 * Math.PI;
            }
            else // CW
            {
                if (angleOffset > 0) angleOffset -= 2.0 * Math.PI;
            }

            double t = angleOffset / (dir * fullSpan);
            return Math.Max(0.0, Math.Min(1.0, t));
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
        ///     公共方法：对收集的切分点进行排序、去重、构建节点列表、生成子段.
        ///     被 <see cref="SplitEdgeByBoundary"/>、<see cref="SplitArcEdgeByBoundary"/>
        ///     和 <see cref="SplitEllipseEdgeByBoundary"/> 共享.
        /// </summary>
        /// <param name="edge">原始边.</param>
        /// <param name="cutPoints">未排序的切分点列表（t, 交点坐标）.</param>
        /// <returns>子段列表.</returns>
        private List<ExactSegment> BuildSubSegmentsFromCutPoints(
            ExactSegment edge, List<KeyValuePair<double, Point2D>> cutPoints)
        {
            // 按 t 排序
            cutPoints.Sort((a, b) => a.Key.CompareTo(b.Key));

            // 去重
            var uniqueCutPoints = new List<KeyValuePair<double, Point2D>>();
            foreach (var cp in cutPoints)
            {
                if (uniqueCutPoints.Count == 0 ||
                    cp.Key - uniqueCutPoints[uniqueCutPoints.Count - 1].Key > Tol)
                    uniqueCutPoints.Add(cp);
            }

            // 构建切分节点列表：起点, 交点1, 交点2, ..., 终点
            var nodes = new List<(double t, Point2D pt)>();
            nodes.Add((0.0, edge.Start));
            foreach (var cp in uniqueCutPoints)
                nodes.Add((cp.Key, cp.Value));
            nodes.Add((1.0, edge.End));

            // 逐子段生成 ExactSegment，使用精确交点坐标作为端点
            var result = new List<ExactSegment>();
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                double tStart = nodes[i].t;
                double tEnd = nodes[i + 1].t;
                if (tEnd - tStart < Tol) continue;

                var subSegment = CreateSubSegmentWithEndpoints(
                    edge, tStart, tEnd, nodes[i].pt, nodes[i + 1].pt);
                if (subSegment != null)
                    result.Add(subSegment);
            }

            return result;
        }

        /// <summary>
        ///     根据参数范围 [tStart, tEnd] 从原始边创建子线段.
        ///     使用精确的端点坐标，确保不同边切割产生的子段
        ///     在连接点处坐标完全一致。
        /// </summary>
        /// <summary>
        ///     根据参数范围 [tStart, tEnd] 从原始边创建子线段。
        ///     ★ 所有类型都使用 exactStart/exactEnd 作为端点坐标，
        ///     确保共享同一交点的不同边切割产生的子段端点坐标完全一致，
        ///     避免绘制的 Polyline 环之间出现浮点偏差缝隙导致 Hatch 泄漏。
        /// </summary>
        private static ExactSegment CreateSubSegmentWithEndpoints(
            ExactSegment edge, double tStart, double tEnd,
            Point2D exactStart, Point2D exactEnd)
        {
            switch (edge.SegmentType)
            {
                case ExactSegmentType.Line:
                    return new ExactSegment
                    {
                        Source = edge.Source,
                        SegmentType = ExactSegmentType.Line,
                        Start = exactStart,
                        End = exactEnd
                    };

                case ExactSegmentType.Arc:
                    return CreateSubArcWithEndpoints(
                        edge, tStart, tEnd, exactStart, exactEnd);

                case ExactSegmentType.Ellipse:
                    return CreateSubEllipseWithEndpoints(
                        edge, tStart, tEnd, exactStart, exactEnd);

                default:
                    return null;
            }
        }

        /// <summary>
        ///     创建圆弧子段（使用精确端点坐标）.
        ///     保留圆弧几何属性（圆心、半径），但端点使用交点精确坐标.
        /// </summary>
        private static ExactSegment CreateSubArcWithEndpoints(
            ExactSegment edge, double tStart, double tEnd,
            Point2D exactStart, Point2D exactEnd)
        {
            double fullSpan = edge.ArcIsClockwise
                ? edge.ArcStartAngle - edge.ArcEndAngle
                : edge.ArcEndAngle - edge.ArcStartAngle;
            if (fullSpan < 0) fullSpan += 2.0 * Math.PI;

            double dir = edge.ArcIsClockwise ? -1.0 : 1.0;
            double subStartAngle = edge.ArcStartAngle + dir * fullSpan * tStart;
            double subEndAngle = edge.ArcStartAngle + dir * fullSpan * tEnd;

            return new ExactSegment
            {
                Source = edge.Source,
                SegmentType = ExactSegmentType.Arc,
                Start = exactStart,
                End = exactEnd,
                ArcCenter = edge.ArcCenter,
                ArcRadius = edge.ArcRadius,
                ArcStartAngle = subStartAngle,
                ArcEndAngle = subEndAngle,
                ArcIsClockwise = edge.ArcIsClockwise
            };
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

        /// <summary>创建椭圆弧子段（使用精确端点坐标）.</summary>
        private static ExactSegment CreateSubEllipseWithEndpoints(
            ExactSegment edge, double tStart, double tEnd,
            Point2D exactStart, Point2D exactEnd)
        {
            double fullSpan = edge.EllipseIsClockwise
                ? edge.EllipseStartAngle - edge.EllipseEndAngle
                : edge.EllipseEndAngle - edge.EllipseStartAngle;
            if (fullSpan < 0) fullSpan += 2.0 * Math.PI;

            double dir = edge.EllipseIsClockwise ? -1.0 : 1.0;
            double subStartAngle = edge.EllipseStartAngle + dir * fullSpan * tStart;
            double subEndAngle = edge.EllipseStartAngle + dir * fullSpan * tEnd;

            return new ExactSegment
            {
                Source = edge.Source,
                SegmentType = ExactSegmentType.Ellipse,
                Start = exactStart,
                End = exactEnd,
                EllipseCenter = edge.EllipseCenter,
                EllipseMajorRadius = edge.EllipseMajorRadius,
                EllipseMinorRadius = edge.EllipseMinorRadius,
                EllipseRotation = edge.EllipseRotation,
                EllipseStartAngle = subStartAngle,
                EllipseEndAngle = subEndAngle,
                EllipseIsClockwise = edge.EllipseIsClockwise
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
        ///     判断子线段是否与给定的边列表中的某条边共线重叠.
        ///     用于排除 B 的子段中与 A 的边共线的部分，避免差集结果出现多余共边.
        ///     仅对直线段有效（弧/椭圆弧不适用共线检测）.
        /// </summary>
        private static bool IsSegmentOnBoundaryEdge(
            ExactSegment segment, IReadOnlyList<ExactSegment> boundaryEdges)
        {
            if (segment.SegmentType != ExactSegmentType.Line)
                return false;

            // 取子段中点，检查是否在任意一条边界直线段上
            var midPt = GetSegmentMidpoint(segment);
            const double collinearTol = 1e-6;

            foreach (var edge in boundaryEdges)
            {
                if (edge.SegmentType != ExactSegmentType.Line)
                    continue;

                // 检查中点是否在 edge 上（共线 + 在线段范围内）
                double dx = edge.End.X - edge.Start.X;
                double dy = edge.End.Y - edge.Start.Y;
                double lenSq = dx * dx + dy * dy;
                if (lenSq < Tol * Tol) continue;

                // 共线性检查：叉积 ≈ 0
                double cross = (midPt.Y - edge.Start.Y) * dx -
                               (midPt.X - edge.Start.X) * dy;
                if (Math.Abs(cross) > collinearTol) continue;

                // 在线段范围内
                double dot = (midPt.X - edge.Start.X) * dx +
                             (midPt.Y - edge.Start.Y) * dy;
                if (dot >= -collinearTol && dot <= lenSq + collinearTol)
                    return true;
            }

            return false;
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
                    // 优先匹配不同 Source 类型的段（Subject ↔ Clip 交替），
                    // 确保差集边界正确连通而非形成多个独立小环
                    int nextIdx = FindMatchingSegment(
                        keptSegments, used, currentEnd, false, current.Source);

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
                            keptSegments, used, currentEnd, true, current.Source);

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
        ///     优先匹配与当前段不同 Source 类型的段（Subject ↔ Clip 交替），
        ///     确保差集边界在交点处正确交替连接，避免形成多个独立小环.
        /// </summary>
        /// <param name="reverse">false=正向匹配(Start)，true=反向匹配(End).</param>
        /// <param name="currentSource">当前段的 Source 类型，用于优先匹配不同 Source 的段.</param>
        private static int FindMatchingSegment(
            List<ExactSegment> segments, bool[] used,
            Point2D currentEnd, bool reverse,
            SegmentSource currentSource = SegmentSource.Subject)
        {
            double tolSq = MatchTol * MatchTol;

            // 第一遍：优先匹配同 Source 类型的段（继续走同一条曲线的边界）
            // 在交点处，Subject 应继续走 Subject，Clip 应继续走 Clip，
            // 只有在两条曲线的交界点（切分交点）才切换 Source。
            // 这样 B 完全在 A 内部时，B 的所有反向段会被正确连入内孔环。
            int fallback = -1;
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i]) continue;
                var seg = segments[i];
                Point2D checkPt = reverse ? seg.End : seg.Start;
                double dx = checkPt.X - currentEnd.X;
                double dy = checkPt.Y - currentEnd.Y;
                if (dx * dx + dy * dy < tolSq)
                {
                    if (seg.Source == currentSource)
                        return i;
                    if (fallback < 0)
                        fallback = i;
                }
            }

            // 第二遍：如果没有同 Source 的匹配，切换到不同 Source 的匹配
            return fallback;
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
