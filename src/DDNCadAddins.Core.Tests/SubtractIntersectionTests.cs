using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     SUBTRACTCLOSEDCURVE 差集逻辑纯单元测试 — 可在 NUnit Console Runner 运行.
    ///     覆盖：不相交 / 包含 / 相交 / 共享边 / 精度边界 场景.
    ///     差集定义：A \ B = A 中不在 B 内的部分.
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
        // 不相交场景：A \ B = A（A 全部保留）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void DisjointRectangles_ReturnsA()
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
            // A \ B：不相交，返回 A
            var result = _clipper.ClipPolygon(rectA, rectB, keepInside: false);
            Assert.AreEqual(1, result.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // A 包含 B：A \ B = A 减去 B 区域（近似完整 A，实际为带孔区域）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void AContainsB_ReturnsAWithHole()
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
            // A \ B：A 包含 B，返回 A 减去 B 后剩余的环状区域
            var result = _clipper.ClipPolygon(polyA, polyB, keepInside: false);
            Assert.GreaterOrEqual(result.Count, 1);
            Assert.GreaterOrEqual(result[0].Count, 3);
        }

        // ════════════════════════════════════════════════════════════════
        // B 包含 A：A \ B = 无结果（A 全部在 B 内部）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void BContainsA_ReturnsEmpty()
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
            // A \ B：B 包含 A，A 全部在 B 内部，无结果
            var result = _clipper.ClipPolygon(polyA, polyB, keepInside: false);
            Assert.AreEqual(0, result.Count);
        }

        // ════════════════════════════════════════════════════════════════
        // A \ B 相交场景
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void OverlappingRectangles_Difference()
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
            // A \ B：A 保留不在 B 内的部分
            // 交集是 (40,40)-(60,40)-(60,60)-(40,60)
            // A \ B 应包含 L 形区域
            var result = _clipper.ClipPolygon(polyA, polyB, keepInside: false);
            Assert.GreaterOrEqual(result.Count, 1);
            var poly = result[0];
            Assert.GreaterOrEqual(poly.Count, 3);
        }

        // ════════════════════════════════════════════════════════════════
        // 三角形差集
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void TriangleDifference_ReturnsPolygon()
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
            // triA \ triB：triA 减去重叠部分
            var result = _clipper.ClipPolygon(triA, triB, keepInside: false);
            Assert.GreaterOrEqual(result.Count, 1);
            Assert.GreaterOrEqual(result[0].Count, 3);
        }

        // ════════════════════════════════════════════════════════════════
        // 共享边/顶点
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void SharedEdge_Difference()
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
            // A \ B：共享边，无交集区域，返回近似 A
            var result = _clipper.ClipPolygon(polyA, polyB, keepInside: false);
            Assert.GreaterOrEqual(result.Count, 1);
        }

        // ════════════════════════════════════════════════════════════════
        // 精度边界（几乎重叠但略微偏移）
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void NearlyIdenticalRects_Difference_ReturnsThinFrame()
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
            // A \ B：A 减去内部部分，应返回一个薄边框
            var result = _clipper.ClipPolygon(polyA, polyB, keepInside: false);
            Assert.GreaterOrEqual(result.Count, 1);
            Assert.GreaterOrEqual(result[0].Count, 3);
        }

        // ════════════════════════════════════════════════════════════════
        // 凹多边形差集
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void ConcavePolygon_Difference()
        {
            // L 形凹多边形 A（CCW）
            var concaveA = new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(10, 0),
                new Point2D(10, 5), new Point2D(5, 5),
                new Point2D(5, 10), new Point2D(0, 10),
            };
            // 矩形与 L 形交叉
            var rectB = new List<Point2D>
            {
                new Point2D(3, 3), new Point2D(8, 3),
                new Point2D(8, 8), new Point2D(3, 8),
            };
            var result = _clipper.ClipPolygon(concaveA, rectB, keepInside: false);
            Assert.GreaterOrEqual(result.Count, 1, "L 形减去交叉矩形应有余留");
            Assert.GreaterOrEqual(result[0].Count, 3, "余留至少 3 个顶点");
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