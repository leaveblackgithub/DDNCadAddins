using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     SUBTRACTCLOSEDCURVE 交集逻辑纯单元测试 — 可在 NUnit Console Runner 运行.
    ///     覆盖：不相交 / 包含 / 相交 / 共享边 / 精度边界 场景.
    /// </summary>
    [TestFixture]
    public class SubtractIntersectionTests
    {
        private PolygonClipperService _clipper;

        [SetUp]
        public void SetUp()
        {
            _clipper = new PolygonClipperService();
        }

        // ════════════════════════════════════════════════════════════════
        // 不相交场景
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void DisjointRectangles_ReturnsEmpty()
        {
            var rectA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(50, 0),
                new Point2D(50, 50), new Point2D(0, 50),
            };
            var rectB = new List<Point2D>
            {
                new Point2D(100, 100), new Point2D(150, 100),
                new Point2D(150, 150), new Point2D(100, 150),
            };
            var result = _clipper.ClipPolygon(rectB, rectA, keepInside: true);
            Assert.AreEqual(0, result.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // B 包含 A → 返回 A
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void AInsideB_ReturnsA()
        {
            var polyA = new List<Point2D>
            {
                new Point2D(20, 20), new Point2D(40, 20),
                new Point2D(40, 40), new Point2D(20, 40),
            };
            var polyB = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(100, 0),
                new Point2D(100, 100), new Point2D(0, 100),
            };
            var result = _clipper.ClipPolygon(polyB, polyA, keepInside: true);
            Assert.AreEqual(1, result.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // A 包含 B → 返回 B
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void BInsideA_ReturnsB()
        {
            var polyA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(100, 0),
                new Point2D(100, 100), new Point2D(0, 100),
            };
            var polyB = new List<Point2D>
            {
                new Point2D(20, 20), new Point2D(40, 20),
                new Point2D(40, 40), new Point2D(20, 40),
            };
            var result = _clipper.ClipPolygon(polyB, polyA, keepInside: true);
            Assert.AreEqual(1, result.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // 相交场景
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void OverlappingRectangles_ReturnsIntersection()
        {
            var polyA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(60, 0),
                new Point2D(60, 60), new Point2D(0, 60),
            };
            var polyB = new List<Point2D>
            {
                new Point2D(40, 40), new Point2D(100, 40),
                new Point2D(100, 100), new Point2D(40, 100),
            };
            var result = _clipper.ClipPolygon(polyB, polyA, keepInside: true);
            Assert.AreEqual(1, result.Count);
            var poly = result[0];
            Assert.GreaterOrEqual(poly.Count, 3);
            // 交集应该是 (40,40)-(60,40)-(60,60)-(40,60)
            Assert.IsTrue(AnyPointNear(poly, 40, 40, 1e-6));
            Assert.IsTrue(AnyPointNear(poly, 60, 60, 1e-6));
        }

        [Test]
        public void CrossShapedIntersection_ReturnsPolygon()
        {
            var polyA = new List<Point2D>
            {
                new Point2D(10, 0), new Point2D(30, 0),
                new Point2D(30, 50), new Point2D(10, 50),
            };
            var polyB = new List<Point2D>
            {
                new Point2D(0, 10), new Point2D(50, 10),
                new Point2D(50, 30), new Point2D(0, 30),
            };
            var result = _clipper.ClipPolygon(polyB, polyA, keepInside: true);
            Assert.AreEqual(1, result.Count);
            var poly = result[0];
            Assert.AreEqual(4, poly.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // 三角形相交
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void TriangleIntersection_ReturnsPolygon()
        {
            var triA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(100, 0),
                new Point2D(50, 100),
            };
            var triB = new List<Point2D>
            {
                new Point2D(50, 0), new Point2D(150, 0),
                new Point2D(100, 100),
            };
            var result = _clipper.ClipPolygon(triB, triA, keepInside: true);
            Assert.AreEqual(1, result.Count);
            Assert.GreaterOrEqual(result[0].Count, 3);
        }

        // ════════════════════════════════════════════════════════════════
        // 共享边/顶点
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void SharedEdge_ReturnsEmpty()
        {
            var polyA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(50, 0),
                new Point2D(50, 50), new Point2D(0, 50),
            };
            var polyB = new List<Point2D>
            {
                new Point2D(50, 0), new Point2D(100, 0),
                new Point2D(100, 50), new Point2D(50, 50),
            };
            var result = _clipper.ClipPolygon(polyB, polyA, keepInside: true);
            Assert.AreEqual(0, result.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // 精度边界（几乎重叠但略微偏移）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void NearlyIdenticalRects_ReturnsNearlyFull()
        {
            var polyA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(100, 0),
                new Point2D(100, 100), new Point2D(0, 100),
            };
            var polyB = new List<Point2D>
            {
                new Point2D(1, 1), new Point2D(99, 1),
                new Point2D(99, 99), new Point2D(1, 99),
            };
            var result = _clipper.ClipPolygon(polyB, polyA, keepInside: true);
            Assert.AreEqual(1, result.Count);
            Assert.GreaterOrEqual(result[0].Count, 3);
        }

        // ════════════════════════════════════════════════════════════════
        // 凹多边形相交（Sutherland-Hodgman 无法处理凹 clip，此处验证修复）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void ConcaveClipRectangle_ReturnsIntersection()
        {
            // L 形凹多边形（CCW）
            var concaveA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(10, 0),
                new Point2D(10, 5), new Point2D(5, 5),
                new Point2D(5, 10), new Point2D(0, 10),
            };
            // 矩形与 L 形的水平和垂直臂都相交
            var rectB = new List<Point2D>
            {
                new Point2D(3, 3), new Point2D(8, 3),
                new Point2D(8, 8), new Point2D(3, 8),
            };
            var result = _clipper.ClipPolygon(rectB, concaveA, keepInside: true);
            Assert.AreEqual(1, result.Count, "凹 clip 与矩形应产生 1 个交集多边形");
            var poly = result[0];
            Assert.GreaterOrEqual(poly.Count, 3, "交集多边形至少 3 个顶点");
            // 验证关键顶点存在
            Assert.IsTrue(AnyPointNear(poly, 3, 3, 1e-6), "应包含 (3,3)");
            Assert.IsTrue(AnyPointNear(poly, 8, 3, 1e-6), "应包含 (8,3)");
            Assert.IsTrue(AnyPointNear(poly, 5, 5, 1e-6), "应包含内角 (5,5)");
            Assert.IsTrue(AnyPointNear(poly, 3, 8, 1e-6), "应包含 (3,8)");
        }

        [Test]
        public void TwoConcavePolygons_ReturnsIntersection()
        {
            // C 形凹多边形 A
            var concaveA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(10, 0),
                new Point2D(10, 3), new Point2D(3, 3),
                new Point2D(3, 7), new Point2D(10, 7),
                new Point2D(10, 10), new Point2D(0, 10),
            };
            // 反向 C 形凹多边形 B
            var concaveB = new List<Point2D>
            {
                new Point2D(2, 2), new Point2D(8, 2),
                new Point2D(8, 5), new Point2D(5, 5),
                new Point2D(5, 6), new Point2D(8, 6),
                new Point2D(8, 8), new Point2D(2, 8),
            };
            var result = _clipper.ClipPolygon(concaveB, concaveA, keepInside: true);
            Assert.AreEqual(1, result.Count, "两个凹多边形应产生 1 个交集");
            Assert.GreaterOrEqual(result[0].Count, 3, "交集至少 3 个顶点");
        }

        // ════════════════════════════════════════════════════════════════
        // 辅助方法
        // ════════════════════════════════════════════════════════════════

        private static bool AnyPointNear(IReadOnlyList<Point2D> poly, double x, double y, double eps)
        {
            foreach (var pt in poly)
                if (Math.Abs(pt.X - x) < eps && Math.Abs(pt.Y - y) < eps)
                    return true;
            return false;
        }
    }
}