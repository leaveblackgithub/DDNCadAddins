using System;
using System.Collections.Generic;
using System.Linq;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     CurveSubtractService 精确差集纯单元测试 — 可在 NUnit Console Runner 运行.
    ///     覆盖：不相交 / 包含 / 相交 / 圆-矩形 / 矩形-圆 / 多环 场景.
    ///     <para>
    ///         新算法双向拆分：A 按 B 交点拆分 + B 按 A 交点拆分，
    ///         保留 A 不在 B 内部的子段 + B 在 A 内部的子段（反向标记 Clip）.
    ///     </para>
    /// </summary>
    [TestFixture]
    public class CurveSubtractServiceTests
    {
        private CurveSubtractService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new CurveSubtractService();
        }

        // ════════════════════════════════════════════════════════════════
        // 辅助：创建矩形边列表（4 条直线段，CCW）+ 对应的 PolygonCropBoundary
        // ════════════════════════════════════════════════════════════════

        private static List<ExactSegment> MakeRectEdges(
            double x0, double y0, double x1, double y1)
        {
            var p0 = new Point2D(x0, y0);
            var p1 = new Point2D(x1, y0);
            var p2 = new Point2D(x1, y1);
            var p3 = new Point2D(x0, y1);
            return new List<ExactSegment>
            {
                LineSeg(p0, p1),
                LineSeg(p1, p2),
                LineSeg(p2, p3),
                LineSeg(p3, p0),
            };
        }

        private static ICropBoundary MakeRectBoundary(
            double x0, double y0, double x1, double y1)
        {
            return new PolygonCropBoundary(new List<Point2D>
            {
                new Point2D(x0, y0),
                new Point2D(x1, y0),
                new Point2D(x1, y1),
                new Point2D(x0, y1),
            });
        }

        /// <summary>
        ///     封装矩形-矩形的 Subtract 调用（同时提供 edges 和 boundary）.
        /// </summary>
        private OpResult<ExactSubtractResult> SubtractRects(
            double ax0, double ay0, double ax1, double ay1,
            double bx0, double by0, double bx1, double by1)
        {
            var edgesA = MakeRectEdges(ax0, ay0, ax1, ay1);
            var bndA = MakeRectBoundary(ax0, ay0, ax1, ay1);
            var edgesB = MakeRectEdges(bx0, by0, bx1, by1);
            var bndB = MakeRectBoundary(bx0, by0, bx1, by1);
            return _service.Subtract(edgesA, bndA, edgesB, bndB);
        }

        private static ExactSegment LineSeg(Point2D s, Point2D e)
        {
            return new ExactSegment
            {
                Source = SegmentSource.Subject,
                SegmentType = ExactSegmentType.Line,
                Start = s,
                End = e
            };
        }

        // ════════════════════════════════════════════════════════════════
        // 不相交：A 和 B 完全分离 → 返回 A 原样
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void Disjoint_ReturnsSubjectAsIs()
        {
            var result = SubtractRects(0, 0, 50, 50, 100, 100, 150, 150);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data.Loops.Count, "不相交应返回 1 个环");
            Assert.AreEqual(4, result.Data.Loops[0].Count, "环应包含 4 条边");
        }

        // ════════════════════════════════════════════════════════════════
        // B 包含 A → 返回空（A 被完全减去）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void AInsideB_ReturnsEmpty()
        {
            var result = SubtractRects(20, 20, 40, 40, 0, 0, 100, 100);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.IsEmpty, "B 包含 A 应返回空");
        }

        // ════════════════════════════════════════════════════════════════
        // A 包含 B → 返回 A 减去 B 区域后的带洞环
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void BInsideA_ReturnsRingWithClipBoundary()
        {
            var result = SubtractRects(0, 0, 100, 100, 30, 30, 70, 70);

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "A 包含 B 应至少返回 1 个环");

            // 环的边数应 > 4（A 外环 4 条 + B 反向 4 条 = 8）
            int totalSegs = result.Data.Loops.Sum(l => l.Count);
            Assert.Greater(totalSegs, 4, "应有 subject 段 + clip 补全段");

            // 验证有 Clip 来源的段（B 的反向段）
            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();
            bool hasClipSeg = allSegs.Any(s => s.Source == SegmentSource.Clip);
            Assert.IsTrue(hasClipSeg, "A 包含 B 时应有 Clip 来源的段");
        }

        // ════════════════════════════════════════════════════════════════
        // 相交：两个矩形部分重叠 → 差集为 L 形
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void OverlappingRects_ReturnsLShape()
        {
            var result = SubtractRects(0, 0, 60, 60, 40, 40, 100, 100);

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "相交应返回至少 1 个环");

            // 验证差集结果中不包含 (50,50)（该点在 B 内部，应被减去）
            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();
            foreach (var seg in allSegs)
            {
                if (seg.Source == SegmentSource.Subject)
                {
                    var pts = seg.ToPolylinePoints();
                    foreach (var pt in pts)
                    {
                        // 确保没有 subject 段的点在 B 内部（(50,50) 在 B 内）
                        Assert.IsFalse(
                            pt.X > 40 + 1e-6 && pt.X < 100 - 1e-6 &&
                            pt.Y > 40 + 1e-6 && pt.Y < 100 - 1e-6,
                            $"Subject 段点 ({pt.X},{pt.Y}) 不应在 B 内部");
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        // 圆形 B 裁剪矩形 A：A 包含圆 B → 应有 Clip 弧段补全
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void RectMinusCircle_ReturnsRingWithArcSegments()
        {
            // A = 100×100 矩形
            var subjectA = MakeRectEdges(0, 0, 100, 100);
            var subjectBnd = MakeRectBoundary(0, 0, 100, 100);

            // B = 圆，圆心 (50,50)，半径 20
            var clipCenter = new Point2D(50, 50);
            double clipR = 20;
            var clipBnd = new CircleCropBoundary(clipCenter, clipR);
            var clipEdges = MakeCircleEdges(clipCenter, clipR);

            var result = _service.Subtract(subjectA, subjectBnd, clipEdges, clipBnd);

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应返回至少 1 个环");

            // 检查结果中是否有 Clip 来源的弧段（B 的子段反向后补全）
            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();
            bool hasClipArcSeg = allSegs.Any(s =>
                s.Source == SegmentSource.Clip && s.SegmentType == ExactSegmentType.Arc);
            Assert.IsTrue(hasClipArcSeg, "A 包含圆 B 时应有 Clip 来源的弧段");
        }

        // ════════════════════════════════════════════════════════════════
        // 圆弧边（Subject）被矩形边界裁剪
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void ArcEdgeSplitByRect_ReturnsSubArcs()
        {
            // Subject = 完整圆（4 条 90° 圆弧），圆心(0,0)，半径 50
            var center = new Point2D(0, 0);
            double r = 50;
            var subjectA = MakeCircleEdges(center, r);
            var subjectBnd = new CircleCropBoundary(center, r);

            // B = 矩形 (-30,-30)~(30,30)，圆部分在矩形外
            var clipEdges = MakeRectEdges(-30, -30, 30, 30);
            var clipBnd = MakeRectBoundary(-30, -30, 30, 30);

            var result = _service.Subtract(subjectA, subjectBnd, clipEdges, clipBnd);

            Assert.IsTrue(result.IsSuccess);
            // 圆大部分在矩形外 → 应有保留的弧段
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "圆减去内部矩形应有结果");

            // 验证保留的弧段中点在矩形外部
            var keptArcSegs = result.Data.Loops
                .SelectMany(l => l)
                .Where(s => s.SegmentType == ExactSegmentType.Arc && s.Source == SegmentSource.Subject)
                .ToList();

            foreach (var seg in keptArcSegs)
            {
                double midAngle = (seg.ArcStartAngle + seg.ArcEndAngle) / 2.0;
                double mx = seg.ArcCenter.X + seg.ArcRadius * Math.Cos(midAngle);
                double my = seg.ArcCenter.Y + seg.ArcRadius * Math.Sin(midAngle);
                // 中点应在矩形外部（差集保留外部）
                bool inside = mx > -30 - 1e-6 && mx < 30 + 1e-6 &&
                              my > -30 - 1e-6 && my < 30 + 1e-6;
                Assert.IsFalse(inside,
                    $"保留弧段中点 ({mx:F3},{my:F3}) 不应在 B 内部");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // L 形结果验证：相交矩形的差集应为 L 形（6 条边 + 2 条 clip 段 = 8）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void OverlappingRects_ResultHasBothSubjectAndClipSegs()
        {
            var result = SubtractRects(0, 0, 60, 60, 40, 40, 100, 100);

            Assert.IsTrue(result.IsSuccess);
            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();

            bool hasSubject = allSegs.Any(s => s.Source == SegmentSource.Subject);
            bool hasClip = allSegs.Any(s => s.Source == SegmentSource.Clip);
            Assert.IsTrue(hasSubject, "应有 Subject 来源的段");
            Assert.IsTrue(hasClip, "应有 Clip 来源的段（B 在 A 内部的反向段）");
        }

        // ════════════════════════════════════════════════════════════════
        // 精确交点验证：圆与直线交点应为解析解
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void CircleLineIntersection_IsExactAnalytical()
        {
            // 圆心 (0,0) 半径 50，与 x=30 的垂直线相交
            // 解析解：y = ±sqrt(50² - 30²) = ±40
            var boundary = new CircleCropBoundary(new Point2D(0, 0), 50);
            var segStart = new Point2D(30, -100);
            var segEnd = new Point2D(30, 100);

            var intersections = boundary.FindLineIntersections(segStart, segEnd);

            Assert.AreEqual(2, intersections.Count, "应有 2 个交点");
            // 验证交点精确性
            bool found40 = false, foundNeg40 = false;
            foreach (var pt in intersections)
            {
                if (Math.Abs(pt.Y - 40) < 1e-6) found40 = true;
                if (Math.Abs(pt.Y + 40) < 1e-6) foundNeg40 = true;
            }
            Assert.IsTrue(found40, "应包含交点 (30, 40)");
            Assert.IsTrue(foundNeg40, "应包含交点 (30, -40)");
        }

        // ════════════════════════════════════════════════════════════════
        // 参数为空时返回失败
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void NullSubjectEdges_ReturnsFail()
        {
            var bnd = MakeRectBoundary(0, 0, 10, 10);
            var edges = MakeRectEdges(0, 0, 10, 10);

            var result = _service.Subtract(null, bnd, edges, bnd);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void NullSubjectBoundary_ReturnsFail()
        {
            var edges = MakeRectEdges(0, 0, 50, 50);
            var bnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.Subtract(edges, null, edges, bnd);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void NullClipEdges_ReturnsFail()
        {
            var edges = MakeRectEdges(0, 0, 50, 50);
            var bnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.Subtract(edges, bnd, null, bnd);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void NullClipBoundary_ReturnsFail()
        {
            var edges = MakeRectEdges(0, 0, 50, 50);
            var bnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.Subtract(edges, bnd, edges, null);

            Assert.IsFalse(result.IsSuccess);
        }

        // ════════════════════════════════════════════════════════════════
        // 辅助方法
        // ════════════════════════════════════════════════════════════════

        private static ExactSegment ArcSeg(
            Point2D center, double radius,
            double startAngle, double endAngle, bool isClockwise)
        {
            return new ExactSegment
            {
                Source = SegmentSource.Subject,
                SegmentType = ExactSegmentType.Arc,
                Start = new Point2D(
                    center.X + radius * Math.Cos(startAngle),
                    center.Y + radius * Math.Sin(startAngle)),
                End = new Point2D(
                    center.X + radius * Math.Cos(endAngle),
                    center.Y + radius * Math.Sin(endAngle)),
                ArcCenter = center,
                ArcRadius = radius,
                ArcStartAngle = startAngle,
                ArcEndAngle = endAngle,
                ArcIsClockwise = isClockwise
            };
        }

        /// <summary>
        ///     创建完整圆的 4 条 90° 圆弧段（CCW）.
        /// </summary>
        private static List<ExactSegment> MakeCircleEdges(Point2D center, double radius)
        {
            var segments = new List<ExactSegment>(4);
            for (int i = 0; i < 4; i++)
            {
                double sa = i * Math.PI / 2.0;
                double ea = (i + 1) * Math.PI / 2.0;
                segments.Add(ArcSeg(center, radius, sa, ea, false));
            }
            return segments;
        }
    }
}
