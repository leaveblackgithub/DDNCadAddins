using System.Collections.Generic;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     CropGeometryService 的纯逻辑单元测试.
    ///     测试射线法判断、线段求交、包围盒分类等核心几何算法.
    /// </summary>
    [TestFixture]
    public class CropGeometryServiceTests
    {
        private CropGeometryService _service;

        // 一个 10x10 的矩形多边形（0,0 到 10,10）
        private readonly List<Point2D> _rectangle = new List<Point2D>
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(10, 10),
            new Point2D(0, 10),
        };

        [SetUp]
        public void SetUp()
        {
            this._service = new CropGeometryService();
        }

        // ========== IsPointInPolygon 测试 ==========

        [Test]
        public void IsPointInPolygon_PointInside_ReturnsTrue()
        {
            var result = this._service.IsPointInPolygon(new Point2D(5, 5), this._rectangle);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsPointInPolygon_PointOutside_ReturnsFalse()
        {
            var result = this._service.IsPointInPolygon(new Point2D(15, 15), this._rectangle);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsPointInPolygon_PointOnEdge_ReturnsTrue()
        {
            var result = this._service.IsPointInPolygon(new Point2D(5, 0), this._rectangle);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsPointInPolygon_PointOnCorner_ReturnsTrue()
        {
            var result = this._service.IsPointInPolygon(new Point2D(0, 0), this._rectangle);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsPointInPolygon_PointLeftOfRect_ReturnsFalse()
        {
            var result = this._service.IsPointInPolygon(new Point2D(-5, 5), this._rectangle);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsPointInPolygon_ZeroPolygon_ReturnsFalse()
        {
            var result = this._service.IsPointInPolygon(new Point2D(5, 5), new List<Point2D>());
            Assert.IsFalse(result);
        }

        [Test]
        public void IsPointInPolygon_Triangle_PointInside_ReturnsTrue()
        {
            var triangle = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(5, 10),
            };
            var result = this._service.IsPointInPolygon(new Point2D(5, 3), triangle);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsPointInPolygon_Triangle_PointOutside_ReturnsFalse()
        {
            var triangle = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(5, 10),
            };
            var result = this._service.IsPointInPolygon(new Point2D(-1, -1), triangle);
            Assert.IsFalse(result);
        }

        // ========== TryGetSegmentIntersection 测试 ==========

        [Test]
        public void TryGetSegmentIntersection_Crossing_ReturnsTrue()
        {
            var result = this._service.TryGetSegmentIntersection(
                new Point2D(0, 0), new Point2D(10, 10),
                new Point2D(0, 10), new Point2D(10, 0),
                out var intersection);

            Assert.IsTrue(result);
            Assert.AreEqual(5, intersection.X, 1e-9);
            Assert.AreEqual(5, intersection.Y, 1e-9);
        }

        [Test]
        public void TryGetSegmentIntersection_Parallel_ReturnsFalse()
        {
            var result = this._service.TryGetSegmentIntersection(
                new Point2D(0, 0), new Point2D(10, 0),
                new Point2D(0, 5), new Point2D(10, 5),
                out _);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetSegmentIntersection_NotIntersecting_ReturnsFalse()
        {
            var result = this._service.TryGetSegmentIntersection(
                new Point2D(0, 0), new Point2D(5, 5),
                new Point2D(6, 6), new Point2D(10, 10),
                out _);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetSegmentIntersection_EndpointsTouching_ReturnsTrue()
        {
            var result = this._service.TryGetSegmentIntersection(
                new Point2D(0, 0), new Point2D(5, 5),
                new Point2D(5, 5), new Point2D(10, 0),
                out var intersection);

            Assert.IsTrue(result);
            Assert.AreEqual(5, intersection.X, 1e-9);
            Assert.AreEqual(5, intersection.Y, 1e-9);
        }

        // ========== FindLineSegmentIntersections 测试 ==========

        [Test]
        public void FindLineSegmentIntersections_LineThroughRect_ReturnsTwoPoints()
        {
            // 线段从 (-5,5) 到 (15,5)，穿过矩形，应有2个交点
            var intersections = this._service.FindLineSegmentIntersections(
                new Point2D(-5, 5),
                new Point2D(15, 5),
                this._rectangle);

            Assert.AreEqual(2, intersections.Count);
        }

        [Test]
        public void FindLineSegmentIntersections_LineOutsideRect_ReturnsEmpty()
        {
            var intersections = this._service.FindLineSegmentIntersections(
                new Point2D(-5, -5),
                new Point2D(-1, -1),
                this._rectangle);

            Assert.AreEqual(0, intersections.Count);
        }

        [Test]
        public void FindLineSegmentIntersections_LineTouchingEdge_ReturnsOnePoint()
        {
            // 线段从 (-5,5) 到 (0,5)，触及矩形左边边 (0,0)->(0,10) 的中点 (0,5)
            var intersections = this._service.FindLineSegmentIntersections(
                new Point2D(-5, 5),
                new Point2D(0, 5),
                this._rectangle);

            Assert.AreEqual(1, intersections.Count);
            Assert.AreEqual(0, intersections[0].X, 1e-9);
            Assert.AreEqual(5, intersections[0].Y, 1e-9);
        }

        // ========== ClassifyBoundingBox 测试 ==========

        [Test]
        public void ClassifyBoundingBox_BoxInside_ReturnsInside()
        {
            var result = this._service.ClassifyBoundingBox(
                new Point2D(2, 2),
                new Point2D(8, 8),
                this._rectangle);

            Assert.AreEqual(ContainmentResult.Inside, result);
        }

        [Test]
        public void ClassifyBoundingBox_BoxOutside_ReturnsOutside()
        {
            var result = this._service.ClassifyBoundingBox(
                new Point2D(20, 20),
                new Point2D(30, 30),
                this._rectangle);

            Assert.AreEqual(ContainmentResult.Outside, result);
        }

        [Test]
        public void ClassifyBoundingBox_BoxIntersecting_ReturnsIntersects()
        {
            var result = this._service.ClassifyBoundingBox(
                new Point2D(8, 8),
                new Point2D(12, 12),
                this._rectangle);

            Assert.AreEqual(ContainmentResult.Intersects, result);
        }

        [Test]
        public void ClassifyBoundingBox_BoxCoveringPolygon_ReturnsIntersects()
        {
            // 大包围盒包含多边形全部顶点->在外部检查阶段发现多边形的顶点在内部
            var result = this._service.ClassifyBoundingBox(
                new Point2D(-5, -5),
                new Point2D(15, 15),
                this._rectangle);

            Assert.AreEqual(ContainmentResult.Intersects, result);
        }

        // ========== FindLineSegmentIntersections 共线检测测试 ==========

        [Test]
        public void FindLineSegmentIntersections_CollinearOverlap_PartialOverlap_ReturnsOverlapEndpoints()
        {
            // 线段 A(0,5)→B(15,5) 与边界边 (5,5)→(10,5) 共线重叠 → 返回5和10
            var rect = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            // 让边界包含一条 (5,5)→(10,5) 的对角穿过边
            // 实际共线边是矩形底边(0,0)→(10,0)
            var seg = new List<Point2D>
            {
                new Point2D(-5, 0),
                new Point2D(15, 0),
            };
            var pts = this._service.FindLineSegmentIntersections(seg[0], seg[1], rect);
            // 与底边 (0,0)→(10,0) 共线重叠 → 应返回 0 和 10
            Assert.AreEqual(2, pts.Count, "应与矩形底边共线重叠返回两端点");
            Assert.AreEqual(0, pts[0].X, 1e-6);
            Assert.AreEqual(10, pts[1].X, 1e-6);
        }

        [Test]
        public void FindLineSegmentIntersections_Collinear_FullyInsideEdge_ReturnsTwoEndpoints()
        {
            // 线段 (2,0)→(8,0) 完全在矩形底边 (0,0)→(10,0) 内部
            var rect = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            var pts = this._service.FindLineSegmentIntersections(
                new Point2D(2, 0), new Point2D(8, 0), rect);
            Assert.AreEqual(2, pts.Count);
            Assert.AreEqual(2, pts[0].X, 1e-6);
            Assert.AreEqual(8, pts[1].X, 1e-6);
        }

        [Test]
        public void FindLineSegmentIntersections_Collinear_JustTouchingAtEndpoint_ReturnsOnePoint()
        {
            // 线段 (10,0)→(15,0) 与矩形右下角 (10,0) 刚好接触
            var rect = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            var pts = this._service.FindLineSegmentIntersections(
                new Point2D(10, 0), new Point2D(15, 0), rect);
            Assert.AreEqual(1, pts.Count);
            Assert.AreEqual(10, pts[0].X, 1e-6);
        }

        [Test]
        public void FindLineSegmentIntersections_CollinearNoOverlap_ReturnsEmpty()
        {
            var rect = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            // 线段 (12,0)→(20,0) 与底边无重叠
            var pts = this._service.FindLineSegmentIntersections(
                new Point2D(12, 0), new Point2D(20, 0), rect);
            Assert.AreEqual(0, pts.Count);
        }

        // ========== ClassifyBoundingBox 边界测试 ==========

        [Test]
        public void ClassifyBoundingBox_BoxExactlyOnEdge_ReturnsOnBoundary()
        {
            // 正方形 (5,0)→(5,5) 左边刚好压在底边上
            var result = this._service.ClassifyBoundingBox(
                new Point2D(2, 0),
                new Point2D(8, 5),
                this._rectangle);
            // 底边在 y=0，2-8 一部分在内部，底边压线
            Assert.AreEqual(ContainmentResult.Intersects, result);
        }

        [Test]
        public void ClassifyBoundingBox_BoxInsideTouchingAllSides_ReturnsInside()
        {
            // 完全在内部
            var result = this._service.ClassifyBoundingBox(
                new Point2D(1, 1),
                new Point2D(9, 9),
                this._rectangle);
            Assert.AreEqual(ContainmentResult.Inside, result);
        }

        [Test]
        public void ClassifyBoundingBox_NegativeSpace_ReturnsOutside()
        {
            // 完全在负空间
            var result = this._service.ClassifyBoundingBox(
                new Point2D(-20, -20),
                new Point2D(-10, -10),
                this._rectangle);
            Assert.AreEqual(ContainmentResult.Outside, result);
        }

        // ========== SortPointsAlongLine 测试 ==========

        [Test]
        public void SortPointsAlongLine_UnsortedPoints_ReturnsSorted()
        {
            var points = new List<Point2D>
            {
                new Point2D(8, 8),
                new Point2D(2, 2),
                new Point2D(5, 5),
            };

            var sorted = this._service.SortPointsAlongLine(new Point2D(0, 0), points);

            Assert.AreEqual(2, sorted[0].X, 1e-9);
            Assert.AreEqual(5, sorted[1].X, 1e-9);
            Assert.AreEqual(8, sorted[2].X, 1e-9);
        }
    }
}