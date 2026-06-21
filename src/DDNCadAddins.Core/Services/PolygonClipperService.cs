using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     多边形裁剪服务 — Sutherland-Hodgman 算法实现.
    ///     纯几何运算，无 CAD 依赖.
    ///     keepInside: 标准 Sutherland-Hodgman（边依次裁剪，保留内部交集）.
    ///     keepOutside: 用点-多边形包含测试沿 subject 边界走外部边（差集）.
    /// </summary>
    public class PolygonClipperService : IPolygonClipperService
    {
        /// <inheritdoc />
        public IReadOnlyList<IReadOnlyList<Point2D>> ClipPolygon(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon,
            bool keepInside = true)
        {
            if (subjectPolygon == null || subjectPolygon.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();
            if (clipPolygon == null || clipPolygon.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();

            // Sutherland-Hodgman assumes CCW clip polygon.
            // If the clip polygon is CW (clockwise), reverse to CCW so
            // IsInsideEdge's half-plane test works correctly.
            var normalizedClip = EnsureCCW(clipPolygon);

            if (keepInside)
                return this.ClipKeepInside(subjectPolygon, normalizedClip);
            else
                return this.ClipKeepOutside(subjectPolygon, normalizedClip);
        }

        /// <inheritdoc />
        public IReadOnlyList<ClippedPolygonWithSources> ClipPolygonWithSources(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon,
            bool keepInside = true)
        {
            if (subjectPolygon == null || subjectPolygon.Count < 3)
                return Array.Empty<ClippedPolygonWithSources>();
            if (clipPolygon == null || clipPolygon.Count < 3)
                return Array.Empty<ClippedPolygonWithSources>();

            var normalizedClip = EnsureCCW(clipPolygon);

            if (keepInside)
                return this.ClipKeepInsideWithSources(subjectPolygon, normalizedClip);
            else
                // keepOutside 暂不支持带来源标记，返回空
                return Array.Empty<ClippedPolygonWithSources>();
        }

        /// <summary>
        ///     保留裁剪多边形内部的 subject 部分（交集）.
        ///     使用展开序列 + 边界追踪，支持凹多边形.
        /// </summary>
        private IReadOnlyList<IReadOnlyList<Point2D>> ClipKeepInside(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon)
        {
            // ── 快速路径 1：subject 完全在 clip 内部 ──────────────────
            bool allSubjInside = true;
            foreach (var pt in subjectPolygon)
                if (!this.IsPointInPolygon(pt, clipPolygon)) { allSubjInside = false; break; }
            if (allSubjInside)
                return new[] { subjectPolygon };

            // ── 快速路径 2：clip 完全在 subject 内部 ───────────────────
            bool allClipInside = true;
            foreach (var pt in clipPolygon)
                if (!this.IsPointInPolygon(pt, subjectPolygon)) { allClipInside = false; break; }
            if (allClipInside)
                return new[] { clipPolygon };

            // ── 快速路径 3：完全不相交 ────────────────────────────────
            bool anySubjInside = false;
            foreach (var pt in subjectPolygon)
                if (this.IsPointInPolygon(pt, clipPolygon)) { anySubjInside = true; break; }
            bool anyClipInside = false;
            foreach (var pt in clipPolygon)
                if (this.IsPointInPolygon(pt, subjectPolygon)) { anyClipInside = true; break; }
            if (!anySubjInside && !anyClipInside)
            {
                int sn = subjectPolygon.Count, cn = clipPolygon.Count;
                bool edgeX = false;
                for (int si = 0; si < sn && !edgeX; si++)
                for (int ci = 0; ci < cn && !edgeX; ci++)
                    if (this.TrySegmentIntersection(
                            subjectPolygon[si], subjectPolygon[(si + 1) % sn],
                            clipPolygon[ci], clipPolygon[(ci + 1) % cn], out _))
                        edgeX = true;
                if (!edgeX)
                    return Array.Empty<IReadOnlyList<Point2D>>();
            }

            // ── 展开序列：在每条 subject 边上插入与 clip 的交点 ─────────
            int sCount = subjectPolygon.Count;
            int cCount = clipPolygon.Count;
            var expanded = new List<Point2D>();

            for (int i = 0; i < sCount; i++)
            {
                var a = subjectPolygon[i];
                var b = subjectPolygon[(i + 1) % sCount];
                expanded.Add(a);

                var xpts = new List<KeyValuePair<double, Point2D>>();
                for (int ci = 0; ci < cCount; ci++)
                {
                    if (this.TrySegmentIntersectionParametric(
                            a, b,
                            clipPolygon[ci], clipPolygon[(ci + 1) % cCount],
                            out double t, out Point2D xp))
                    {
                        if (t > 1e-9 && t < 1.0 - 1e-9)
                            xpts.Add(new KeyValuePair<double, Point2D>(t, xp));
                    }
                }
                xpts.Sort((x, y) => x.Key.CompareTo(y.Key));
                foreach (var kv in xpts)
                    expanded.Add(kv.Value);
            }

            // ── 追踪：收集内部顶点，遇到外部段时沿 clip 边界 CCW 绕行 ──
            int eCount = expanded.Count;
            int startIdx = -1;
            for (int k = 0; k < eCount; k++)
                if (this.IsPointInPolygon(expanded[k], clipPolygon)) { startIdx = k; break; }

            if (startIdx < 0)
                return Array.Empty<IReadOnlyList<Point2D>>();

            var output = new List<Point2D>();
            int idx = startIdx;
            int visited = 0;

            while (visited <= eCount)
            {
                var pt = expanded[idx];
                if (this.IsPointInPolygon(pt, clipPolygon))
                {
                    output.Add(pt);
                    idx = (idx + 1) % eCount;
                    visited++;
                    if (visited > 1 && idx == startIdx) break;
                }
                else
                {
                    // 离开 clip 的交点 = 外部段的前一个点
                    var exitPt = expanded[(idx + eCount - 1) % eCount];

                    // 找进入 clip 的交点 = 外部段的终点
                    int j = idx;
                    int safety = 0;
                    while (!this.IsPointInPolygon(expanded[j], clipPolygon) && safety < eCount)
                    {
                        j = (j + 1) % eCount;
                        safety++;
                    }
                    var entry = expanded[j];

                    // 沿 clip 边界 CCW（正向）从 exitPt 绕行到 entry
                    var clipVerts = this.CollectClipBoundaryVertsCCW(exitPt, entry, clipPolygon);
                    output.AddRange(clipVerts);

                    idx = j;
                    visited += safety;
                    if (idx == startIdx) break;
                }
            }

            if (output.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();

            var deduped = this.RemoveAdjacentDuplicates(output);
            if (deduped.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();
            var cleaned = this.RemoveCollinearVertices(deduped);
            if (cleaned.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();
            return new[] { (IReadOnlyList<Point2D>)cleaned };
        }

        /// <summary>
        ///     保留裁剪多边形内部的 subject 部分（交集），返回带来源标记的结果.
        ///     用于混合绘制：曲线段用 CurveFit，折线段保持折线.
        /// </summary>
        private IReadOnlyList<ClippedPolygonWithSources> ClipKeepInsideWithSources(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon)
        {
            // ── 快速路径 1：subject 完全在 clip 内部 ──────────────────
            bool allSubjInside = true;
            foreach (var pt in subjectPolygon)
                if (!this.IsPointInPolygon(pt, clipPolygon)) { allSubjInside = false; break; }
            if (allSubjInside)
            {
                var clippedResult = new ClippedPolygonWithSources();
                clippedResult.Vertices.AddRange(subjectPolygon);
                // 整个多边形来自 Subject
                clippedResult.Segments.Add(new ClippedSegment
                {
                    StartIndex = 0,
                    EndIndex = subjectPolygon.Count - 1,
                    Source = SegmentSource.Subject,
                    Vertices = new List<Point2D>(subjectPolygon)
                });
                return new[] { clippedResult };
            }

            // ── 快速路径 2：clip 完全在 subject 内部 ───────────────────
            bool allClipInside = true;
            foreach (var pt in clipPolygon)
                if (!this.IsPointInPolygon(pt, subjectPolygon)) { allClipInside = false; break; }
            if (allClipInside)
            {
                var clippedResult = new ClippedPolygonWithSources();
                clippedResult.Vertices.AddRange(clipPolygon);
                // 整个多边形来自 Clip
                clippedResult.Segments.Add(new ClippedSegment
                {
                    StartIndex = 0,
                    EndIndex = clipPolygon.Count - 1,
                    Source = SegmentSource.Clip,
                    Vertices = new List<Point2D>(clipPolygon)
                });
                return new[] { clippedResult };
            }

            // ── 快速路径 3：完全不相交 ────────────────────────────────
            bool anySubjInside = false;
            foreach (var pt in subjectPolygon)
                if (this.IsPointInPolygon(pt, clipPolygon)) { anySubjInside = true; break; }
            bool anyClipInside = false;
            foreach (var pt in clipPolygon)
                if (this.IsPointInPolygon(pt, subjectPolygon)) { anyClipInside = true; break; }
            if (!anySubjInside && !anyClipInside)
            {
                int sn = subjectPolygon.Count, cn = clipPolygon.Count;
                bool edgeX = false;
                for (int si = 0; si < sn && !edgeX; si++)
                for (int ci = 0; ci < cn && !edgeX; ci++)
                    if (this.TrySegmentIntersection(
                            subjectPolygon[si], subjectPolygon[(si + 1) % sn],
                            clipPolygon[ci], clipPolygon[(ci + 1) % cn], out _))
                        edgeX = true;
                if (!edgeX)
                    return Array.Empty<ClippedPolygonWithSources>();
            }

            // ── 展开序列：在每条 subject 边上插入与 clip 的交点 ─────────
            int sCount = subjectPolygon.Count;
            int cCount = clipPolygon.Count;
            var expanded = new List<Point2D>();

            for (int i = 0; i < sCount; i++)
            {
                var a = subjectPolygon[i];
                var b = subjectPolygon[(i + 1) % sCount];
                expanded.Add(a);

                var xpts = new List<KeyValuePair<double, Point2D>>();
                for (int ci = 0; ci < cCount; ci++)
                {
                    if (this.TrySegmentIntersectionParametric(
                            a, b,
                            clipPolygon[ci], clipPolygon[(ci + 1) % cCount],
                            out double t, out Point2D xp))
                    {
                        if (t > 1e-9 && t < 1.0 - 1e-9)
                            xpts.Add(new KeyValuePair<double, Point2D>(t, xp));
                    }
                }
                xpts.Sort((x, y) => x.Key.CompareTo(y.Key));
                foreach (var kv in xpts)
                    expanded.Add(kv.Value);
            }

            // ── 追踪：收集内部顶点，遇到外部段时沿 clip 边界绕行 ────────
            int eCount = expanded.Count;
            int startIdx = -1;
            for (int k = 0; k < eCount; k++)
                if (this.IsPointInPolygon(expanded[k], clipPolygon)) { startIdx = k; break; }

            if (startIdx < 0)
                return Array.Empty<ClippedPolygonWithSources>();

            var clippedPoly = new ClippedPolygonWithSources();
            int idx = startIdx;
            int visited = 0;
            int subjSegStart = -1; // 当前 Subject 段的起始索引

            while (visited <= eCount)
            {
                var pt = expanded[idx];
                if (this.IsPointInPolygon(pt, clipPolygon))
                {
                    // 来自 Subject 的顶点：记录段起始位置
                    if (subjSegStart < 0)
                        subjSegStart = clippedPoly.Vertices.Count;

                    clippedPoly.Vertices.Add(pt);
                    idx = (idx + 1) % eCount;
                    visited++;
                    if (visited > 1 && idx == startIdx) break;
                }
                else
                {
                    // 离开 clip 区域：关闭当前 Subject 段
                    if (subjSegStart >= 0)
                    {
                        int subjSegEnd = clippedPoly.Vertices.Count - 1;
                        if (subjSegEnd >= subjSegStart)
                        {
                            var subjVerts = new List<Point2D>();
                            for (int k = subjSegStart; k <= subjSegEnd; k++)
                                subjVerts.Add(clippedPoly.Vertices[k]);
                            clippedPoly.Segments.Add(new ClippedSegment
                            {
                                StartIndex = subjSegStart,
                                EndIndex = subjSegEnd,
                                Source = SegmentSource.Subject,
                                Vertices = subjVerts
                            });
                        }
                        subjSegStart = -1;
                    }

                    // 记录离开 clip 的交点
                    var exitPt = expanded[(idx + eCount - 1) % eCount];

                    // 找进入 clip 的交点
                    int j = idx;
                    int safety = 0;
                    while (!this.IsPointInPolygon(expanded[j], clipPolygon) && safety < eCount)
                    {
                        j = (j + 1) % eCount;
                        safety++;
                    }
                    var entry = expanded[j];

                    // 沿 clip 边界绕行
                    var clipVerts = this.CollectClipBoundaryVertsCCW(exitPt, entry, clipPolygon);

                    // 添加 Clip 段
                    int clipStartIdx = clippedPoly.Vertices.Count;
                    foreach (var cv in clipVerts)
                        clippedPoly.Vertices.Add(cv);

                    clippedPoly.Segments.Add(new ClippedSegment
                    {
                        StartIndex = clipStartIdx,
                        EndIndex = clippedPoly.Vertices.Count - 1,
                        Source = SegmentSource.Clip,
                        Vertices = new List<Point2D>(clipVerts)
                    });

                    idx = j;
                    visited += safety;
                    if (idx == startIdx) break;
                }
            }

            // 关闭最后一个 Subject 段（循环结束时可能还在 clip 内部）
            if (subjSegStart >= 0)
            {
                int subjSegEnd = clippedPoly.Vertices.Count - 1;
                if (subjSegEnd >= subjSegStart)
                {
                    var subjVerts = new List<Point2D>();
                    for (int k = subjSegStart; k <= subjSegEnd; k++)
                        subjVerts.Add(clippedPoly.Vertices[k]);
                    clippedPoly.Segments.Add(new ClippedSegment
                    {
                        StartIndex = subjSegStart,
                        EndIndex = subjSegEnd,
                        Source = SegmentSource.Subject,
                        Vertices = subjVerts
                    });
                }
            }

            if (clippedPoly.Vertices.Count < 3)
                return Array.Empty<ClippedPolygonWithSources>();

            // 后处理：去重和去共线
            // 注意：清理后 clippedPoly.Vertices 可能改变，但 Segments 中的 Vertices 是独立副本，
            // DrawMixedPolygon 使用 seg.Vertices 绘制，不受影响
            var deduped = this.RemoveAdjacentDuplicates(clippedPoly.Vertices);
            if (deduped.Count < 3)
                return Array.Empty<ClippedPolygonWithSources>();
            var cleaned = this.RemoveCollinearVertices(deduped);
            if (cleaned.Count < 3)
                return Array.Empty<ClippedPolygonWithSources>();

            clippedPoly.Vertices = cleaned;

            // 安全网：如果 Segments 为空（不应发生），用整个多边形作为混合来源
            if (clippedPoly.Segments.Count == 0)
            {
                clippedPoly.Segments.Add(new ClippedSegment
                {
                    StartIndex = 0,
                    EndIndex = cleaned.Count - 1,
                    Source = SegmentSource.Intersection,
                    Vertices = new List<Point2D>(cleaned)
                });
            }

            return new[] { clippedPoly };
        }

        /// <summary>
        ///     保留裁剪多边形外部的 subject 部分（差集）.
        ///
        ///     算法：
        ///     1. 在 subject 每条边上插入与 clip 的所有交点（展开序列）
        ///     2. 沿展开序列收集外部顶点
        ///     3. 遇到进入 clip 时：
        ///        entry = 展开序列中第一个 inside 点（即入射交点）
        ///        exit  = 展开序列中最后一个连续 inside 点（即出射交点）
        ///        沿 clip 边界 CW（顺时针/反向）从 entry 绕行到 exit，
        ///        走过的是 clip 内部交集边界的对应段
        ///     4. 挖孔场景（clip 完全在 subject 内，无交点）近似返回 subject 外环
        /// </summary>
        private IReadOnlyList<IReadOnlyList<Point2D>> ClipKeepOutside(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon)
        {
            // ── 快速路径 ──────────────────────────────────────────────
            bool anySubjOutside = false;
            foreach (var pt in subjectPolygon)
                if (!this.IsPointInPolygon(pt, clipPolygon)) { anySubjOutside = true; break; }
            if (!anySubjOutside)
                return Array.Empty<IReadOnlyList<Point2D>>();

            bool anySubjInside = false;
            foreach (var pt in subjectPolygon)
                if (this.IsPointInPolygon(pt, clipPolygon)) { anySubjInside = true; break; }

            if (!anySubjInside)
            {
                // subject 顶点全在 clip 外，检查是否有边相交
                int sn = subjectPolygon.Count, cn = clipPolygon.Count;
                bool edgeX = false;
                for (int si = 0; si < sn && !edgeX; si++)
                for (int ci = 0; ci < cn && !edgeX; ci++)
                    if (this.TrySegmentIntersection(
                            subjectPolygon[si], subjectPolygon[(si + 1) % sn],
                            clipPolygon[ci],    clipPolygon[(ci + 1) % cn], out _))
                        edgeX = true;
                // 无边相交（包括 clip 完全在 subject 内挖孔）→ 近似返回 subject
                if (!edgeX)
                    return new[] { subjectPolygon };
            }

            // ── 展开序列：在每条 subject 边上插入与 clip 的交点 ─────────
            int sCount = subjectPolygon.Count;
            int cCount = clipPolygon.Count;
            var expanded = new List<Point2D>();

            for (int i = 0; i < sCount; i++)
            {
                var a = subjectPolygon[i];
                var b = subjectPolygon[(i + 1) % sCount];
                expanded.Add(a);

                var xpts = new List<KeyValuePair<double, Point2D>>();
                for (int ci = 0; ci < cCount; ci++)
                {
                    if (this.TrySegmentIntersectionParametric(
                            a, b,
                            clipPolygon[ci], clipPolygon[(ci + 1) % cCount],
                            out double t, out Point2D xp))
                    {
                        if (t > 1e-9 && t < 1.0 - 1e-9)
                            xpts.Add(new KeyValuePair<double, Point2D>(t, xp));
                    }
                }
                xpts.Sort((x, y) => x.Key.CompareTo(y.Key));
                foreach (var kv in xpts)
                    expanded.Add(kv.Value);
            }

            // ── 追踪：收集外部顶点，遇到 clip 内部时 CW 绕行 ────────────
            int eCount = expanded.Count;
            int startIdx = -1;
            for (int k = 0; k < eCount; k++)
                if (!this.IsPointInPolygon(expanded[k], clipPolygon)) { startIdx = k; break; }

            if (startIdx < 0)
                return Array.Empty<IReadOnlyList<Point2D>>();

            var output = new List<Point2D>();
            int idx = startIdx;
            int visited = 0;

            while (visited <= eCount)
            {
                var pt = expanded[idx];
                if (!this.IsPointInPolygon(pt, clipPolygon))
                {
                    output.Add(pt);
                    idx = (idx + 1) % eCount;
                    visited++;
                    if (visited > 1 && idx == startIdx) break;
                }
                else
                {
                    // entry = 展开序列中第一个连续 inside 点（入射交点）
                    var entry = expanded[idx];

                    // 找最后一个连续 inside 点（出射交点）
                    int j = idx;
                    int safety = 0;
                    while (this.IsPointInPolygon(expanded[j], clipPolygon) && safety < eCount)
                    {
                        j = (j + 1) % eCount;
                        safety++;
                    }
                    var exitPt = expanded[(j + eCount - 1) % eCount];

                    // 沿 clip 边界 CW（反向）从 entry 绕行到 exitPt
                    var clipVerts = this.CollectClipBoundaryVertsCW(entry, exitPt, clipPolygon);
                    output.AddRange(clipVerts);

                    idx = j;
                    visited += safety;
                    if (idx == startIdx) break;
                }
            }

            if (output.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();

            var deduped = this.RemoveAdjacentDuplicates(output);
            var cleaned = this.RemoveCollinearVertices(deduped);
            if (cleaned.Count < 3)
                return Array.Empty<IReadOnlyList<Point2D>>();
            return new[] { (IReadOnlyList<Point2D>)cleaned };
        }

        /// <summary>
        ///     沿 clip 边界 CW（顺时针/反向）从 entry 绕行到 exitPt（包含两端点）.
        ///
        ///     CW 绕行规则（clip 为 CCW 时）：
        ///       entry 在 clip 边 startEdge 上 → 从 clip[startEdge]（边的起点）反向走
        ///       exit  在 clip 边 endEdge   上 → 走到 clip[(endEdge+1)%n]（边的终点）
        ///       反向遍历：cur = startEdge, startEdge-1, ... 直到走到 endEdge+1
        /// </summary>
        private List<Point2D> CollectClipBoundaryVertsCW(
            Point2D entry, Point2D exitPt, IReadOnlyList<Point2D> clip)
        {
            int n = clip.Count;
            var result = new List<Point2D>();
            result.Add(entry);

            int startEdge = this.FindEdgeContainingPoint(entry,  clip);
            int endEdge   = this.FindEdgeContainingPoint(exitPt, clip);

            if (startEdge < 0 || endEdge < 0 || startEdge == endEdge)
            {
                result.Add(exitPt);
                return result;
            }

            // CW：从 startEdge 的起点反向走到 endEdge 的终点
            // clip[startEdge] = 边起点，clip[(endEdge+1)%n] = 边终点
            // 先加再判，确保 stop 节点也被包含（修复 CW 绕行缺少最后一个顶点的 bug）
            int cur = startEdge;
            int stop = (endEdge + 1) % n;
            for (int safety = 0; safety < n + 2; safety++)
            {
                result.Add(clip[cur]);
                if (cur == stop) break;
                cur = (cur + n - 1) % n;  // 反向（CW）
            }
            result.Add(exitPt);
            return result;
        }

        /// <summary>
        ///     沿 clip 边界从 startPt 绕行到 endPt（包含两端点）.
        ///     自动选择较短路径（内侧段），支持凹多边形.
        /// </summary>
        private List<Point2D> CollectClipBoundaryVertsCCW(
            Point2D startPt, Point2D endPt, IReadOnlyList<Point2D> clip)
        {
            int n = clip.Count;
            var result = new List<Point2D>();
            result.Add(startPt);

            int startEdge = this.FindEdgeContainingPoint(startPt, clip);
            int endEdge = this.FindEdgeContainingPoint(endPt, clip);

            if (startEdge < 0 || endEdge < 0 || startEdge == endEdge)
            {
                result.Add(endPt);
                return result;
            }

            // 选择较短路径（内侧段）
            int ccwSteps = (endEdge - startEdge + n) % n;
            int cwSteps = (startEdge - endEdge + n) % n;

            if (ccwSteps <= cwSteps)
            {
                // CCW 是短路：从 startEdge 的终点正向走到 endEdge 的起点
                int cur = (startEdge + 1) % n;
                int stop = endEdge;
                for (int safety = 0; safety < n + 2; safety++)
                {
                    result.Add(clip[cur]);
                    if (cur == stop) break;
                    cur = (cur + 1) % n;
                }
            }
            else
            {
                // CW 是短路：从 startEdge 的起点反向走到 endEdge 的终点
                int cur = startEdge;
                int stop = (endEdge + 1) % n;
                for (int safety = 0; safety < n + 2; safety++)
                {
                    result.Add(clip[cur]);
                    if (cur == stop) break;
                    cur = (cur + n - 1) % n;
                }
            }
            result.Add(endPt);
            return result;
        }

        /// <summary>找点所在的 clip 边索引（-1 = 未找到）.</summary>
        private int FindEdgeContainingPoint(Point2D pt, IReadOnlyList<Point2D> poly)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                var cross = (b.X - a.X) * (pt.Y - a.Y) - (b.Y - a.Y) * (pt.X - a.X);
                if (Math.Abs(cross) > 1e-6) continue;
                var dot   = (pt.X - a.X) * (b.X - a.X) + (pt.Y - a.Y) * (b.Y - a.Y);
                var lenSq = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
                if (lenSq > 1e-12 && dot >= -1e-6 && dot <= lenSq + 1e-6)
                    return i;
            }
            return -1;
        }

        /// <summary>线段相交并返回参数 t（沿 p1→p2 的比例）.</summary>
        private bool TrySegmentIntersectionParametric(
            Point2D p1, Point2D p2, Point2D p3, Point2D p4,
            out double t, out Point2D intersection)
        {
            t = 0; intersection = default(Point2D);
            var dx1 = p2.X - p1.X; var dy1 = p2.Y - p1.Y;
            var dx2 = p4.X - p3.X; var dy2 = p4.Y - p3.Y;
            var den = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(den) < 1e-12) return false;
            t = ((p3.X - p1.X) * dy2 - (p3.Y - p1.Y) * dx2) / den;
            var u = ((p3.X - p1.X) * dy1 - (p3.Y - p1.Y) * dx1) / den;
            if (t < -1e-12 || t > 1 + 1e-12 || u < -1e-12 || u > 1 + 1e-12) return false;
            intersection = new Point2D(p1.X + t * dx1, p1.Y + t * dy1);
            return true;
        }

        private bool IsInsideEdge(Point2D point, Point2D edgeStart, Point2D edgeEnd)
        {
            var cross = (edgeEnd.X - edgeStart.X) * (point.Y - edgeStart.Y)
                      - (edgeEnd.Y - edgeStart.Y) * (point.X - edgeStart.X);
            return cross >= -1e-12;
        }

        /// <summary>
        ///     射线法判断点是否在闭合多边形内部（含边界）.
        /// </summary>
        private bool IsPointInPolygon(Point2D point, IReadOnlyList<Point2D> polygon)
        {
            var count = polygon.Count;
            if (count < 3)
                return false;

            var inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                // 检查点是否在多边形边上
                var cross = (pi.X - pj.X) * (point.Y - pj.Y) - (pi.Y - pj.Y) * (point.X - pj.X);
                if (Math.Abs(cross) < 1e-12)
                {
                    // 点在边的无限延长线上，检查是否在线段范围内
                    var dot = (point.X - pj.X) * (pi.X - pj.X) + (point.Y - pj.Y) * (pi.Y - pj.Y);
                    var lenSq = (pi.X - pj.X) * (pi.X - pj.X) + (pi.Y - pj.Y) * (pi.Y - pj.Y);
                    if (lenSq > 0 && dot >= 0 && dot <= lenSq)
                        return true;
                }

                if ((pi.Y > point.Y) != (pj.Y > point.Y))
                {
                    var t = (point.X - pj.X) - (pi.X - pj.X) * (point.Y - pj.Y) / (pi.Y - pj.Y);
                    if (t < 1e-12)
                        inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        ///     找 subject 边与 clip 多边形的交点（取离起点最近的交点）.
        /// </summary>
        private Point2D FindClipEdgeIntersection(
            Point2D subjectStart, Point2D subjectEnd, IReadOnlyList<Point2D> clipPolygon)
        {
            var bestDist = double.MaxValue;
            var bestPt = new Point2D(0, 0);
            var clipCount = clipPolygon.Count;

            for (var i = 0; i < clipCount; i++)
            {
                var c1 = clipPolygon[i];
                var c2 = clipPolygon[(i + 1) % clipCount];

                if (this.TrySegmentIntersection(subjectStart, subjectEnd, c1, c2, out var pt))
                {
                    var dx = pt.X - subjectStart.X;
                    var dy = pt.Y - subjectStart.Y;
                    var dist = (dx * dx) + (dy * dy);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestPt = pt;
                    }
                }
            }

            return bestPt;
        }

        /// <summary>
        ///     两条线段是否相交，返回交点.
        /// </summary>
        private bool TrySegmentIntersection(
            Point2D p1, Point2D p2, Point2D p3, Point2D p4, out Point2D intersection)
        {
            intersection = default(Point2D);

            var dx1 = p2.X - p1.X;
            var dy1 = p2.Y - p1.Y;
            var dx2 = p4.X - p3.X;
            var dy2 = p4.Y - p3.Y;

            var denominator = (dx1 * dy2) - (dy1 * dx2);
            if (Math.Abs(denominator) < 1e-12)
                return false;

            var t = ((p3.X - p1.X) * dy2 - (p3.Y - p1.Y) * dx2) / denominator;
            var u = ((p3.X - p1.X) * dy1 - (p3.Y - p1.Y) * dx1) / denominator;

            if (t < -1e-12 || t > 1.0 + 1e-12 || u < -1e-12 || u > 1.0 + 1e-12)
                return false;

            intersection = new Point2D(p1.X + (t * dx1), p1.Y + (t * dy1));
            return true;
        }

        /// <summary>
        ///     无限直线交点（参数法），供 Sutherland-Hodgman 使用.
        /// </summary>
        private Point2D LineLineIntersection(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
        {
            var dx1 = p2.X - p1.X;
            var dy1 = p2.Y - p1.Y;
            var dx2 = p4.X - p3.X;
            var dy2 = p4.Y - p3.Y;

            var denominator = (dx1 * dy2) - (dy1 * dx2);

            // 平行 → 取中点
            if (Math.Abs(denominator) < 1e-12)
                return new Point2D((p2.X + p1.X) / 2.0, (p2.Y + p1.Y) / 2.0);

            var t = ((p3.X - p1.X) * dy2 - (p3.Y - p1.Y) * dx2) / denominator;
            return new Point2D(p1.X + (t * dx1), p1.Y + (t * dy1));
        }

        /// <summary>
        ///     移除相邻重复顶点.
        /// </summary>

        /// <summary>
        ///     确保多边形为 CCW（逆时针）方向。Sutherland-Hodgman 算法要求裁剪多边形为 CCW.
        ///     若多边形面积为负（CW 顺时针），则反转顶点顺序.
        /// </summary>
        private static IReadOnlyList<Point2D> EnsureCCW(IReadOnlyList<Point2D> polygon)
        {
            // Shoelace formula: signed area > 0 = CCW
            double area = 0;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            area /= 2;

            if (area >= 0) return polygon; // Already CCW (or degenerate/zero area)

            // Reverse to make CCW
            var reversed = new List<Point2D>(n);
            for (int i = n - 1; i >= 0; i--)
                reversed.Add(polygon[i]);
            return reversed;
        }

        private List<Point2D> RemoveAdjacentDuplicates(List<Point2D> polygon)
        {
            var result = new List<Point2D>();
            var count = polygon.Count;

            for (var i = 0; i < count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % count];

                var dx = current.X - next.X;
                var dy = current.Y - next.Y;
                if ((dx * dx + dy * dy) >= 1e-20)
                    result.Add(current);
            }

            return result;
        }

        /// <summary>
        ///     移除多边形中近共线的冗余顶点（Sutherland-Hodgman 裁剪后处理）.
        ///     共线顶点会在 AutoCAD 中产生意外的 SOLID 填充或缺口伪影.
        /// </summary>
        private List<Point2D> RemoveCollinearVertices(List<Point2D> polygon)
        {
            int n = polygon.Count;
            if (n < 3) return polygon;

            var result = new List<Point2D>();
            const double areaEps = 1e-12; // 三角形面积阈值（平方单位）

            for (int i = 0; i < n; i++)
            {
                var prev = polygon[(i + n - 1) % n];
                var curr = polygon[i];
                var next = polygon[(i + 1) % n];

                // 三角形 (prev, curr, next) 的有向面积 ×2 = |cross(prev→curr, prev→next)|
                var cross = (curr.X - prev.X) * (next.Y - prev.Y)
                          - (curr.Y - prev.Y) * (next.X - prev.X);
                var area2 = Math.Abs(cross); // 面积 ×2

                // 仅保留非共线顶点（面积足够大的三角）
                if (area2 > areaEps)
                    result.Add(curr);
            }

            // 若清理后顶点太少，回退到原始列表
            return result.Count >= 3 ? result : polygon;
        }
    }
}