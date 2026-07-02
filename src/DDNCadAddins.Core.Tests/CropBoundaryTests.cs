using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     ICropBoundary 实现类的单元测试 — 圆/椭圆精确边界.
    ///     验证点含判断、线段求交、包围盒分类等核心几何算法.
    /// </summary>
    [TestFixture]
    public class CropBoundaryTests
    {
        // ========== CircleCropBoundary 测试 ==========

        [Test]
        public void Circle_IsPointInside_Center_ReturnsTrue()
        {
            var boundary = new CircleCropBoundary(new Point2D(10, 20), 5);
            Assert.IsTrue(boundary.IsPointInside(new Point2D(10, 20)));
        }

        [Test]
        public void Circle_IsPointInside_OnEdge_ReturnsTrue()
        {
            var boundary = new CircleCropBoundary(new Point2D(10, 20), 5);
            Assert.IsTrue(boundary.IsPointInside(new Point2D(15, 20)));
            Assert.IsTrue(boundary.IsPointInside(new Point2D(5, 20)));
            Assert.IsTrue(boundary.IsPointInside(new Point2D(10, 25)));
            Assert.IsTrue(boundary.IsPointInside(new Point2D(10, 15)));
        }

        [Test]
        public void Circle_IsPointInside_Outside_ReturnsFalse()
        {
            var boundary = new CircleCropBoundary(new Point2D(10, 20), 5);
            Assert.IsFalse(boundary.IsPointInside(new Point2D(20, 20)));
            Assert.IsFalse(boundary.IsPointInside(new Point2D(10, 30)));
            Assert.IsFalse(boundary.IsPointInside(new Point2D(0, 0)));
        }

        [Test]
        public void Circle_FindLineIntersections_ThroughCenter_ReturnsTwo()
        {
            var boundary = new CircleCropBoundary(new Point2D(10, 20), 5);
            var ix = boundary.FindLineIntersections(new Point2D(0, 20), new Point2D(20, 20));
            Assert.AreEqual(2, ix.Count);
            Assert.AreEqual(5, ix[0].X, 1e-6);
            Assert.AreEqual(15, ix[1].X, 1e-6);
        }

        [Test]
        public void Circle_FindLineIntersections_Tangent_ReturnsOne()
        {
            var boundary = new CircleCropBoundary(new Point2D(0, 0), 5);
            // 水平线在 y=5（切线）
            var ix = boundary.FindLineIntersections(new Point2D(-10, 5), new Point2D(10, 5));
            Assert.AreEqual(1, ix.Count);
            Assert.AreEqual(0, ix[0].X, 1e-6);
        }

        [Test]
        public void Circle_FindLineIntersections_NoIntersection_ReturnsEmpty()
        {
            var boundary = new CircleCropBoundary(new Point2D(0, 0), 5);
            var ix = boundary.FindLineIntersections(new Point2D(10, 10), new Point2D(20, 20));
            Assert.AreEqual(0, ix.Count);
        }

        [Test]
        public void Circle_ClassifyBoundingBox_Inside_ReturnsInside()
        {
            var boundary = new CircleCropBoundary(new Point2D(0, 0), 10);
            var result = boundary.ClassifyBoundingBox(new Point2D(-3, -3), new Point2D(3, 3));
            Assert.AreEqual(ContainmentResult.Inside, result);
        }

        [Test]
        public void Circle_ClassifyBoundingBox_Outside_ReturnsOutside()
        {
            var boundary = new CircleCropBoundary(new Point2D(0, 0), 5);
            var result = boundary.ClassifyBoundingBox(new Point2D(20, 20), new Point2D(30, 30));
            Assert.AreEqual(ContainmentResult.Outside, result);
        }

        [Test]
        public void Circle_ClassifyBoundingBox_Intersects_ReturnsIntersects()
        {
            var boundary = new CircleCropBoundary(new Point2D(0, 0), 10);
            var result = boundary.ClassifyBoundingBox(new Point2D(5, 5), new Point2D(20, 20));
            Assert.AreEqual(ContainmentResult.Intersects, result);
        }

        [Test]
        public void Circle_BoundingBox_Correct()
        {
            var boundary = new CircleCropBoundary(new Point2D(10, 20), 5);
            Assert.AreEqual(5, boundary.BoundingBoxMin.X, 1e-9);
            Assert.AreEqual(15, boundary.BoundingBoxMin.Y, 1e-9);
            Assert.AreEqual(15, boundary.BoundingBoxMax.X, 1e-9);
            Assert.AreEqual(25, boundary.BoundingBoxMax.Y, 1e-9);
        }

        // ========== EllipseCropBoundary 测试（无旋转） ==========

        [Test]
        public void Ellipse_IsPointInside_Center_ReturnsTrue()
        {
            var boundary = new EllipseCropBoundary(new Point2D(38.771, -10.943), 3.797, 3.229, 0.0);
            Assert.IsTrue(boundary.IsPointInside(new Point2D(38.771, -10.943)));
        }

        [Test]
        public void Ellipse_IsPointInside_VertexInside_ReturnsTrue()
        {
            var boundary = new EllipseCropBoundary(new Point2D(38.771, -10.943), 3.797, 3.229, 0.0);
            Assert.IsTrue(boundary.IsPointInside(new Point2D(38.699, -12.143)));
        }

        [Test]
        public void Ellipse_IsPointInside_VertexOutside_ReturnsFalse()
        {
            var boundary = new EllipseCropBoundary(new Point2D(38.771, -10.943), 3.797, 3.229, 0.0);
            Assert.IsFalse(boundary.IsPointInside(new Point2D(31.227, -4.390)));
        }

        [Test]
        public void Ellipse_FindLineIntersections_ThroughCenter_ReturnsTwo()
        {
            var boundary = new EllipseCropBoundary(new Point2D(38.771, -10.943), 3.797, 3.229, 0.0);
            // 水平线穿过中心
            var ix = boundary.FindLineIntersections(
                new Point2D(31.0, -10.943), new Point2D(46.0, -10.943));
            Assert.AreEqual(2, ix.Count);
            Assert.AreEqual(38.771 - 3.797, ix[0].X, 1e-4);
            Assert.AreEqual(38.771 + 3.797, ix[1].X, 1e-4);
        }

        [Test]
        public void Ellipse_FindLineIntersections_PartialEntry_ReturnsOne()
        {
            var boundary = new EllipseCropBoundary(new Point2D(38.771, -10.943), 3.797, 3.229, 0.0);
            // 线段从外部进入椭圆内部（终点在内部）
            var ix = boundary.FindLineIntersections(
                new Point2D(31.227, -4.390), new Point2D(38.699, -12.143));
            Assert.AreEqual(1, ix.Count);
        }

        [Test]
        public void Ellipse_ClassifyBoundingBox_Inside_ReturnsInside()
        {
            var boundary = new EllipseCropBoundary(new Point2D(0, 0), 10, 8, 0.0);
            var result = boundary.ClassifyBoundingBox(new Point2D(-2, -2), new Point2D(2, 2));
            Assert.AreEqual(ContainmentResult.Inside, result);
        }

        [Test]
        public void Ellipse_ClassifyBoundingBox_Outside_ReturnsOutside()
        {
            var boundary = new EllipseCropBoundary(new Point2D(0, 0), 5, 3, 0.0);
            var result = boundary.ClassifyBoundingBox(new Point2D(20, 20), new Point2D(30, 30));
            Assert.AreEqual(ContainmentResult.Outside, result);
        }

        // ========== EllipseCropBoundary 测试（旋转） ==========

        [Test]
        public void Ellipse_Rotated_IsPointInside_EndOfMajorAxis_ReturnsTrue()
        {
            // 椭圆旋转 45°，长轴半径 10
            var boundary = new EllipseCropBoundary(new Point2D(0, 0), 10, 5, System.Math.PI / 4.0);
            // 长轴方向：(cos45, sin45) ≈ (0.707, 0.707)
            var majorEnd = new Point2D(10 * 0.7071, 10 * 0.7071);
            Assert.IsTrue(boundary.IsPointInside(majorEnd));
        }

        [Test]
        public void Ellipse_Rotated_IsPointInside_EndOfMinorAxis_ReturnsTrue()
        {
            // 椭圆旋转 45°，短轴半径 5
            var boundary = new EllipseCropBoundary(new Point2D(0, 0), 10, 5, System.Math.PI / 4.0);
            // 短轴方向：(-sin45, cos45) ≈ (-0.707, 0.707)
            var minorEnd = new Point2D(-5 * 0.7071, 5 * 0.7071);
            Assert.IsTrue(boundary.IsPointInside(minorEnd));
        }

        [Test]
        public void Ellipse_Rotated_FindLineIntersections_AlongMajorAxis_ReturnsTwo()
        {
            // 椭圆旋转 45°，长轴半径 10
            var boundary = new EllipseCropBoundary(new Point2D(0, 0), 10, 5, System.Math.PI / 4.0);
            // 沿长轴方向的直线
            var majorDir = new Point2D(0.7071, 0.7071);
            var start = new Point2D(-15 * majorDir.X, -15 * majorDir.Y);
            var end = new Point2D(15 * majorDir.X, 15 * majorDir.Y);
            var ix = boundary.FindLineIntersections(start, end);
            Assert.AreEqual(2, ix.Count);
        }

        // ========== PolygonCropBoundary 测试 ==========

        [Test]
        public void Polygon_IsPointInside_RectangleCenter_ReturnsTrue()
        {
            var polygon = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            var boundary = new PolygonCropBoundary(polygon);
            Assert.IsTrue(boundary.IsPointInside(new Point2D(5, 5)));
        }

        [Test]
        public void Polygon_IsPointInside_Outside_ReturnsFalse()
        {
            var polygon = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            var boundary = new PolygonCropBoundary(polygon);
            Assert.IsFalse(boundary.IsPointInside(new Point2D(15, 15)));
        }

        [Test]
        public void Polygon_BoundingBox_Correct()
        {
            var polygon = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10),
            };
            var boundary = new PolygonCropBoundary(polygon);
            Assert.AreEqual(0, boundary.BoundingBoxMin.X, 1e-9);
            Assert.AreEqual(0, boundary.BoundingBoxMin.Y, 1e-9);
            Assert.AreEqual(10, boundary.BoundingBoxMax.X, 1e-9);
            Assert.AreEqual(10, boundary.BoundingBoxMax.Y, 1e-9);
        }
    }
}
