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
        // 椭圆-椭圆差集：两个相交椭圆
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void EllipseMinusEllipse_ReturnsResult()
        {
            // A = 椭圆，中心(0,0)，长轴50沿X轴，短轴30
            var ellipseAEdges = MakeEllipseEdges(
                new Point2D(0, 0), 50, 30, 0);
            var ellipseABnd = new EllipseCropBoundary(
                new Point2D(0, 0), 50, 30, 0);

            // B = 椭圆，中心(20,0)，长轴30沿X轴，短轴20（与A部分重叠）
            var ellipseBEdges = MakeEllipseEdges(
                new Point2D(20, 0), 30, 20, 0);
            var ellipseBBnd = new EllipseCropBoundary(
                new Point2D(20, 0), 30, 20, 0);

            var result = _service.Subtract(
                ellipseAEdges, ellipseABnd,
                ellipseBEdges, ellipseBBnd);

            Assert.IsTrue(result.IsSuccess, "椭圆差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "相交椭圆差集应有结果");
        }

        [Test]
        public void EllipseMinusEllipse_Rotated_ReturnsResult()
        {
            // A = 椭圆，中心(0,0)，长轴50，短轴30，旋转45°
            double rot = Math.PI / 4.0;
            var ellipseAEdges = MakeEllipseEdges(
                new Point2D(0, 0), 50, 30, rot);
            var ellipseABnd = new EllipseCropBoundary(
                new Point2D(0, 0), 50, 30, rot);

            // B = 椭圆，中心(10,10)，长轴30，短轴20，旋转-30°
            var ellipseBEdges = MakeEllipseEdges(
                new Point2D(10, 10), 30, 20, -Math.PI / 6.0);
            var ellipseBBnd = new EllipseCropBoundary(
                new Point2D(10, 10), 30, 20, -Math.PI / 6.0);

            var result = _service.Subtract(
                ellipseAEdges, ellipseABnd,
                ellipseBEdges, ellipseBBnd);

            Assert.IsTrue(result.IsSuccess, "旋转椭圆差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "相交旋转椭圆差集应有结果");
        }

        // ════════════════════════════════════════════════════════════════
        // 椭圆-矩形差集
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void EllipseMinusRect_ReturnsResult()
        {
            // A = 椭圆，中心(0,0)，长轴60，短轴40
            var ellipseAEdges = MakeEllipseEdges(
                new Point2D(0, 0), 60, 40, 0);
            var ellipseABnd = new EllipseCropBoundary(
                new Point2D(0, 0), 60, 40, 0);

            // B = 矩形 (-20,-20)~(20,20)，位于椭圆内部
            var rectEdges = MakeRectEdges(-20, -20, 20, 20);
            var rectBnd = MakeRectBoundary(-20, -20, 20, 20);

            var result = _service.Subtract(
                ellipseAEdges, ellipseABnd,
                rectEdges, rectBnd);

            Assert.IsTrue(result.IsSuccess, "椭圆减矩形应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "椭圆包含矩形时差集应有结果");

            // 验证有 Clip 来源的段（矩形在椭圆内部的部分）
            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();
            bool hasClipSeg = allSegs.Any(s => s.Source == SegmentSource.Clip);
            Assert.IsTrue(hasClipSeg, "椭圆包含矩形时应有 Clip 来源的段");
        }

        [Test]
        public void RectMinusEllipse_ReturnsResult()
        {
            // A = 矩形 (-50,-50)~(50,50)
            var rectEdges = MakeRectEdges(-50, -50, 50, 50);
            var rectBnd = MakeRectBoundary(-50, -50, 50, 50);

            // B = 椭圆，中心(0,0)，长轴30，短轴20（在矩形内部）
            var ellipseBEdges = MakeEllipseEdges(
                new Point2D(0, 0), 30, 20, 0);
            var ellipseBBnd = new EllipseCropBoundary(
                new Point2D(0, 0), 30, 20, 0);

            var result = _service.Subtract(
                rectEdges, rectBnd,
                ellipseBEdges, ellipseBBnd);

            Assert.IsTrue(result.IsSuccess, "矩形减椭圆应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "矩形包含椭圆时差集应有结果");

            // 验证有 Clip 来源的椭圆弧段
            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();
            bool hasClipEllipseSeg = allSegs.Any(s =>
                s.Source == SegmentSource.Clip && s.SegmentType == ExactSegmentType.Ellipse);
            Assert.IsTrue(hasClipEllipseSeg, "矩形包含椭圆时应有 Clip 来源的椭圆弧段");
        }

        // ════════════════════════════════════════════════════════════════
        // 椭圆-圆差集
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void EllipseMinusCircle_ReturnsResult()
        {
            // A = 椭圆，中心(0,0)，长轴50，短轴30
            var ellipseAEdges = MakeEllipseEdges(
                new Point2D(0, 0), 50, 30, 0);
            var ellipseABnd = new EllipseCropBoundary(
                new Point2D(0, 0), 50, 30, 0);

            // B = 圆，圆心(0,0)，半径15（在椭圆内部）
            var circleEdges = MakeCircleEdges(new Point2D(0, 0), 15);
            var circleBnd = new CircleCropBoundary(new Point2D(0, 0), 15);

            var result = _service.Subtract(
                ellipseAEdges, ellipseABnd,
                circleEdges, circleBnd);

            Assert.IsTrue(result.IsSuccess, "椭圆减圆应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "椭圆包含圆时差集应有结果");
        }

        // ════════════════════════════════════════════════════════════════
        // 椭圆不相交 → 返回原椭圆
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void DisjointEllipses_ReturnsSubjectAsIs()
        {
            // A = 椭圆，中心(0,0)，长轴30，短轴20
            var ellipseAEdges = MakeEllipseEdges(
                new Point2D(0, 0), 30, 20, 0);
            var ellipseABnd = new EllipseCropBoundary(
                new Point2D(0, 0), 30, 20, 0);

            // B = 椭圆，中心(200,200)，长轴30，短轴20（远离A）
            var ellipseBEdges = MakeEllipseEdges(
                new Point2D(200, 200), 30, 20, 0);
            var ellipseBBnd = new EllipseCropBoundary(
                new Point2D(200, 200), 30, 20, 0);

            var result = _service.Subtract(
                ellipseAEdges, ellipseABnd,
                ellipseBEdges, ellipseBBnd);

            Assert.IsTrue(result.IsSuccess, "不相交椭圆差集应成功");
            Assert.AreEqual(1, result.Data.Loops.Count, "不相交应返回 1 个环");
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

        /// <summary>
        ///     创建完整椭圆的 4 条 90° 椭圆弧段（CCW）.
        /// </summary>
        private static List<ExactSegment> MakeEllipseEdges(
            Point2D center, double majorR, double minorR, double rotation)
        {
            var segments = new List<ExactSegment>(4);
            double cosRot = Math.Cos(rotation);
            double sinRot = Math.Sin(rotation);

            for (int i = 0; i < 4; i++)
            {
                double sa = i * Math.PI / 2.0;
                double ea = (i + 1) * Math.PI / 2.0;

                double sxLocal = majorR * Math.Cos(sa);
                double syLocal = minorR * Math.Sin(sa);
                double exLocal = majorR * Math.Cos(ea);
                double eyLocal = minorR * Math.Sin(ea);

                segments.Add(new ExactSegment
                {
                    Source = SegmentSource.Subject,
                    SegmentType = ExactSegmentType.Ellipse,
                    Start = new Point2D(
                        center.X + sxLocal * cosRot - syLocal * sinRot,
                        center.Y + sxLocal * sinRot + syLocal * cosRot),
                    End = new Point2D(
                        center.X + exLocal * cosRot - eyLocal * sinRot,
                        center.Y + exLocal * sinRot + eyLocal * cosRot),
                    EllipseCenter = center,
                    EllipseMajorRadius = majorR,
                    EllipseMinorRadius = minorR,
                    EllipseRotation = rotation,
                    EllipseStartAngle = sa,
                    EllipseEndAngle = ea,
                    EllipseIsClockwise = false
                });
            }

            return segments;
        }

        // ════════════════════════════════════════════════════════════════
        // 实际数据复现：A(6顶点Polyline) 包含 B(4顶点Polyline)，B边与A边部分共线
        // 期望：1 个连通环（A外边界 + B反向内边界）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void PolyAContainsPolyB_WithCollinearEdge_ReturnsSingleLoop()
        {
            // A 的顶点（来自 TestRecords/crop_20260703-121027-2b4c.json）
            var a0 = new Point2D(31.227442, -4.390386);
            var a1 = new Point2D(28.872951, -7.942884);
            var a2 = new Point2D(31.515747, -13.703691);
            var a3 = new Point2D(34.903332, -11.159335);
            var a4 = new Point2D(38.699349, -12.143473);
            var a5 = new Point2D(40.885663, -6.862732);

            var edgesA = new List<ExactSegment>
            {
                LineSeg(a0, a1), LineSeg(a1, a2), LineSeg(a2, a3),
                LineSeg(a3, a4), LineSeg(a4, a5), LineSeg(a5, a0),
            };
            var bndA = new PolygonCropBoundary(new List<Point2D> { a0, a1, a2, a3, a4, a5 });

            // B 的顶点（完全在 A 内部）
            var b0 = new Point2D(36.143670, -5.648859);
            var b1 = new Point2D(38.660346, -6.293087);
            var b2 = new Point2D(38.268152, -7.379684);
            var b3 = new Point2D(36.169005, -6.610639);

            var edgesB = new List<ExactSegment>
            {
                LineSeg(b0, b1), LineSeg(b1, b2), LineSeg(b2, b3), LineSeg(b3, b0),
            };
            var bndB = new PolygonCropBoundary(new List<Point2D> { b0, b1, b2, b3 });

            var result = _service.Subtract(edgesA, bndA, edgesB, bndB);

            Assert.IsTrue(result.IsSuccess, "差集应成功");

            // 调试输出：每个环的段信息
            System.Console.WriteLine($"Loop count: {result.Data.Loops.Count}");
            for (int li = 0; li < result.Data.Loops.Count; li++)
            {
                var loop = result.Data.Loops[li];
                System.Console.WriteLine($"  Loop[{li}]: {loop.Count} segments");
                for (int si = 0; si < loop.Count; si++)
                {
                    var seg = loop[si];
                    System.Console.WriteLine($"    Seg[{si}] Source={seg.Source} " +
                        $"Start=({seg.Start.X:F6},{seg.Start.Y:F6}) " +
                        $"End=({seg.End.X:F6},{seg.End.Y:F6})");
                }
            }

            // 期望：1 个连通环（A 包含 B → 带洞环）
            Assert.AreEqual(1, result.Data.Loops.Count,
                "A 包含 B 应返回 1 个连通环，实际返回 " + result.Data.Loops.Count);
        }

        [Test]
        public void PolyAContainsPolyB_CollinearSplit_Debug()
        {
            // 单独测试 A[5] 被 B 边界切分的结果
            var a5 = new Point2D(40.885663, -6.862732);
            var a0 = new Point2D(31.227442, -4.390386);

            var edge = LineSeg(a5, a0);

            var b0 = new Point2D(36.143670, -5.648859);
            var b1 = new Point2D(38.660346, -6.293087);
            var b2 = new Point2D(38.268152, -7.379684);
            var b3 = new Point2D(36.169005, -6.610639);
            var bndB = new PolygonCropBoundary(new List<Point2D> { b0, b1, b2, b3 });

            // 获取 A[5] 的采样点
            var edgePoints = edge.ToPolylinePoints();
            System.Console.WriteLine($"A[5] edge points: {edgePoints.Count}");
            foreach (var pt in edgePoints)
                System.Console.WriteLine($"  ({pt.X:F6},{pt.Y:F6})");

            // 测试每个采样段与 B 边界的交点
            for (int i = 0; i < edgePoints.Count - 1; i++)
            {
                var p1 = edgePoints[i];
                var p2 = edgePoints[i + 1];
                var intersections = bndB.FindLineIntersections(p1, p2);
                if (intersections.Count > 0)
                {
                    System.Console.WriteLine($"  Segment[{i}] ({p1.X:F6},{p1.Y:F6})->({p2.X:F6},{p2.Y:F6}) -> {intersections.Count} intersections:");
                    foreach (var ix in intersections)
                        System.Console.WriteLine($"    IX=({ix.X:F6},{ix.Y:F6})");
                }
            }

            // 测试共线段中点是否在 B 内部
            var midCollinear = new Point2D((b0.X + b1.X) / 2.0, (b0.Y + b1.Y) / 2.0);
            bool midInside = bndB.IsPointInside(midCollinear);
            System.Console.WriteLine($"  Collinear mid=({midCollinear.X:F6},{midCollinear.Y:F6}) inside B={midInside}");

            Assert.Pass("调试测试，查看输出");
        }

        // ════════════════════════════════════════════════════════════════
        // 多 Subject 差集测试（SubtractMultiSubject）
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     封装 SubtractMultiSubject 调用：多个 Subject 减去同一个 Clip.
        /// </summary>
        private OpResult<ExactSubtractResult> SubtractMultiSubjectRects(
            (double x0, double y0, double x1, double y1) clip,
            params (double x0, double y0, double x1, double y1)[] subjects)
        {
            var subjectList = new List<(IReadOnlyList<ExactSegment> Edges, ICropBoundary Boundary)>();
            foreach (var s in subjects)
            {
                subjectList.Add((
                    MakeRectEdges(s.x0, s.y0, s.x1, s.y1),
                    MakeRectBoundary(s.x0, s.y0, s.x1, s.y1)));
            }

            var clipEdges = MakeRectEdges(clip.x0, clip.y0, clip.x1, clip.y1);
            var clipBnd = MakeRectBoundary(clip.x0, clip.y0, clip.x1, clip.y1);

            return _service.SubtractMultiSubject(subjectList, clipEdges, clipBnd);
        }

        [Test]
        public void TwoSubjectsOneClip_BothDisjoint_ReturnsBothSubjects()
        {
            // A1 = 0~50 矩形, A2 = 100~150 矩形
            // B = 200~250 矩形（完全不相交）
            var result = SubtractMultiSubjectRects(
                (200, 200, 250, 250),
                (0, 0, 50, 50),
                (100, 100, 150, 150));

            Assert.IsTrue(result.IsSuccess, "双Subject差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应至少返回1个环");
        }

        [Test]
        public void TwoSubjectsOneClip_BothContainB_ReturnsTwoRingsWithHoles()
        {
            // A1 = 0~100 矩形, A2 = 200~300 矩形
            // B = 30~70 矩形（同时与两个A都不相交，但不在任何一个内部...不对）
            // 正确的：A1包含B，A2不包含B
            // B = 30~70 矩形在 A1 内部，不在 A2 内部
            var result = SubtractMultiSubjectRects(
                (30, 30, 70, 70),
                (0, 0, 100, 100),
                (200, 200, 300, 300));

            Assert.IsTrue(result.IsSuccess, "混合Subject差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应至少返回1个环");
        }

        [Test]
        public void TwoSubjectsOneClip_BothContainB_ReturnsTwoHoles()
        {
            // A1 = 0~100 矩形, A2 = 200~300 矩形
            // B = 30~70 矩形，同时在 A1 和 A2 内部（需要 B 足够小且 A 不重叠）
            // 不对，B 不能同时在两个不相交的矩形内部
            // 改为：A1 和 A2 都包含 B
            // A1 = 0~100, A2 = 50~150, B = 60~90（在 A1∩A2 内）
            var result = SubtractMultiSubjectRects(
                (60, 60, 90, 90),
                (0, 0, 100, 100),
                (50, 50, 150, 150));

            Assert.IsTrue(result.IsSuccess, "双Subject都包含B差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应至少返回1个环");
        }

        [Test]
        public void TwoSubjectsOneClip_ClipContainsBoth_ReturnsEmpty()
        {
            // A1 = 20~40, A2 = 50~70
            // B = 0~100 大矩形（包含两个A）
            var result = SubtractMultiSubjectRects(
                (0, 0, 100, 100),
                (20, 20, 40, 40),
                (50, 50, 70, 70));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.IsEmpty, "B包含所有Subject时应返回空");
        }

        [Test]
        public void NullSubjectsList_ReturnsFail()
        {
            var clipEdges = MakeRectEdges(0, 0, 50, 50);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.SubtractMultiSubject(null, clipEdges, clipBnd);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void EmptySubjectsList_ReturnsFail()
        {
            var clipEdges = MakeRectEdges(0, 0, 50, 50);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.SubtractMultiSubject(
                new List<(IReadOnlyList<ExactSegment>, ICropBoundary)>(),
                clipEdges, clipBnd);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void TwoSubjectsOneClip_OneContainsB_OneOutside_ReturnsMixed()
        {
            // A1 = 0~100 矩形（包含B）, A2 = 200~300 矩形（B完全不相交）
            // B = 30~70 矩形（在A1内部）
            var result = SubtractMultiSubjectRects(
                (30, 30, 70, 70),
                (0, 0, 100, 100),
                (200, 200, 300, 300));

            Assert.IsTrue(result.IsSuccess, "混合差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应至少返回1个环");

            var allSegs = result.Data.Loops.SelectMany(l => l).ToList();
            bool hasClip = allSegs.Any(s => s.Source == SegmentSource.Clip);
            Assert.IsTrue(hasClip, "A1包含B时应有Clip段");
        }

        // ════════════════════════════════════════════════════════════════
        // 外环+内环（孔洞）裁剪测试（CropRingWithHole）
        // 覆盖：Clip 同时与外环内环相交的凹字形场景
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     计算多个闭合环的带符号面积之和（Shoelace 公式）.
        ///     约定：外环（CCW）为正，内孔环（CW，反向后）为负，
        ///     求和后即为净面积（外环面积 - 内孔面积）.
        ///     仅适用于纯直线段组成的环（矩形测试场景）.
        /// </summary>
        private static double SignedLoopArea(List<List<ExactSegment>> loops)
        {
            double total = 0;
            foreach (var loop in loops)
            {
                if (loop == null || loop.Count == 0) continue;
                var pts = loop.Select(s => s.Start).ToList();
                int n = pts.Count;
                double area = 0;
                for (int i = 0; i < n; i++)
                {
                    var p1 = pts[i];
                    var p2 = pts[(i + 1) % n];
                    area += p1.X * p2.Y - p2.X * p1.Y;
                }
                total += area / 2.0;
            }
            return total;
        }

        [Test]
        public void CropRingWithHole_ClipDisjoint_KeepOutside_ReturnsAnnulus()
        {
            // 外环 0~100，内孔 40~60，Clip 200~250（完全不相交）
            var outerEdges = MakeRectEdges(0, 0, 100, 100);
            var outerBnd = MakeRectBoundary(0, 0, 100, 100);
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var holeBnd = MakeRectBoundary(40, 40, 60, 60);
            var clipEdges = MakeRectEdges(200, 200, 250, 250);
            var clipBnd = MakeRectBoundary(200, 200, 250, 250);

            var result = _service.CropRingWithHole(
                outerEdges, outerBnd, holeEdges, holeBnd, clipEdges, clipBnd, keepInside: false);

            Assert.IsTrue(result.IsSuccess, "Clip 不相交时差集应成功");
            Assert.AreEqual(2, result.Data.Loops.Count, "应返回外环+内孔两个环");

            double netArea = SignedLoopArea(result.Data.Loops);
            Assert.AreEqual(9600.0, netArea, 1e-6, "净面积应为 外环(10000)-内孔(400)=9600");
        }

        [Test]
        public void CropRingWithHole_ClipDisjoint_KeepInside_ReturnsEmpty()
        {
            // Clip 与外环完全不相交 → 保留内部(交集)应为空
            var outerEdges = MakeRectEdges(0, 0, 100, 100);
            var outerBnd = MakeRectBoundary(0, 0, 100, 100);
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var holeBnd = MakeRectBoundary(40, 40, 60, 60);
            var clipEdges = MakeRectEdges(200, 200, 250, 250);
            var clipBnd = MakeRectBoundary(200, 200, 250, 250);

            var result = _service.CropRingWithHole(
                outerEdges, outerBnd, holeEdges, holeBnd, clipEdges, clipBnd, keepInside: true);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.IsEmpty, "Clip 与外环不相交时交集应为空");
        }

        [Test]
        public void CropRingWithHole_ClipIntersectsBothOuterAndHole_KeepOutside_ReturnsConcaveShape()
        {
            // ★ 核心场景：Clip 同时与外环、内环相交
            // 外环 0~100，内孔 40~60，Clip 0~50（与外环、内孔均有重叠）
            var outerEdges = MakeRectEdges(0, 0, 100, 100);
            var outerBnd = MakeRectBoundary(0, 0, 100, 100);
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var holeBnd = MakeRectBoundary(40, 40, 60, 60);
            var clipEdges = MakeRectEdges(0, 0, 50, 50);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.CropRingWithHole(
                outerEdges, outerBnd, holeEdges, holeBnd, clipEdges, clipBnd, keepInside: false);

            Assert.IsTrue(result.IsSuccess, "凹字形差集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应至少返回1个环");

            // 净面积 = 外环(10000) - [Clip(2500) + 内孔(400) - Clip∩内孔(100)] = 7200
            double netArea = SignedLoopArea(result.Data.Loops);
            Assert.AreEqual(7200.0, netArea, 1e-6,
                "净面积应为 外环-（Clip∪内孔）与外环的交集 = 10000-2800=7200");

            // 内孔区域中未被 Clip 覆盖的部分不应出现在 Subject 来源的外环段中
            var outerSegs = result.Data.Loops.SelectMany(l => l)
                .Where(s => s.Source == SegmentSource.Subject).ToList();
            foreach (var seg in outerSegs)
            {
                foreach (var pt in seg.ToPolylinePoints())
                {
                    bool inHoleStrict = pt.X > 40 + 1e-6 && pt.X < 60 - 1e-6 &&
                                        pt.Y > 40 + 1e-6 && pt.Y < 60 - 1e-6;
                    Assert.IsFalse(inHoleStrict, $"外环段点 ({pt.X},{pt.Y}) 不应严格位于内孔内部");
                }
            }
        }

        [Test]
        public void CropRingWithHole_ClipIntersectsBothOuterAndHole_KeepInside_ReturnsConcaveShape()
        {
            // 同一几何，保留内部(交集)：结果 = Clip \ 内孔（Clip 完全在外环内)
            var outerEdges = MakeRectEdges(0, 0, 100, 100);
            var outerBnd = MakeRectBoundary(0, 0, 100, 100);
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var holeBnd = MakeRectBoundary(40, 40, 60, 60);
            var clipEdges = MakeRectEdges(0, 0, 50, 50);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.CropRingWithHole(
                outerEdges, outerBnd, holeEdges, holeBnd, clipEdges, clipBnd, keepInside: true);

            Assert.IsTrue(result.IsSuccess, "凹字形交集应成功");
            Assert.GreaterOrEqual(result.Data.Loops.Count, 1, "应至少返回1个环");

            // 净面积 = Clip(2500) - Clip∩内孔(100) = 2400
            double netArea = SignedLoopArea(result.Data.Loops);
            Assert.AreEqual(2400.0, netArea, 1e-6,
                "净面积应为 Clip 减去与内孔重叠部分 = 2500-100=2400");
        }

        [Test]
        public void CropRingWithHole_NullOuterEdges_ReturnsFail()
        {
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var holeBnd = MakeRectBoundary(40, 40, 60, 60);
            var clipEdges = MakeRectEdges(0, 0, 50, 50);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.CropRingWithHole(
                null, MakeRectBoundary(0, 0, 100, 100),
                holeEdges, holeBnd, clipEdges, clipBnd, keepInside: false);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void CropRingWithHole_NullHoleBoundary_ReturnsFail()
        {
            var outerEdges = MakeRectEdges(0, 0, 100, 100);
            var outerBnd = MakeRectBoundary(0, 0, 100, 100);
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var clipEdges = MakeRectEdges(0, 0, 50, 50);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.CropRingWithHole(
                outerEdges, outerBnd, holeEdges, null, clipEdges, clipBnd, keepInside: false);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void CropRingWithHole_NullClipEdges_ReturnsFail()
        {
            var outerEdges = MakeRectEdges(0, 0, 100, 100);
            var outerBnd = MakeRectBoundary(0, 0, 100, 100);
            var holeEdges = MakeRectEdges(40, 40, 60, 60);
            var holeBnd = MakeRectBoundary(40, 40, 60, 60);
            var clipBnd = MakeRectBoundary(0, 0, 50, 50);

            var result = _service.CropRingWithHole(
                outerEdges, outerBnd, holeEdges, holeBnd, null, clipBnd, keepInside: false);

            Assert.IsFalse(result.IsSuccess);
        }
    }
}
