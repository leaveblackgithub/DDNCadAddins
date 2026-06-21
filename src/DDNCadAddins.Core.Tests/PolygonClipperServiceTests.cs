using System.Collections.Generic;
using System.Linq;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     PolygonClipperService 纯逻辑单元测试 — Sutherland-Hodgman 算法.
    /// </summary>
    [TestFixture]
    public class PolygonClipperServiceTests
    {
        private PolygonClipperService _service;

        // 10×10 矩形（CCW）
        private readonly List<Point2D> _rect10 = new List<Point2D>
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(10, 10),
            new Point2D(0, 10),
        };

        // 5×5 矩形，偏移到 (5,5)-(10,10)（CCW）
        private readonly List<Point2D> _rectSmallTopRight = new List<Point2D>
        {
            new Point2D(5, 5),
            new Point2D(10, 5),
            new Point2D(10, 10),
            new Point2D(5, 10),
        };

        // 三角形
        private readonly List<Point2D> _triangle = new List<Point2D>
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(5, 10),
        };

        [SetUp]
        public void SetUp()
        {
            this._service = new PolygonClipperService();
        }

        // ========== 基本功能：KeepInside ==========

        [Test]
        public void ClipPolygon_SubjectFullyInside_ReturnsOriginal()
        {
            var small = new List<Point2D>
            {
                new Point2D(2, 2),
                new Point2D(8, 2),
                new Point2D(8, 8),
                new Point2D(2, 8),
            };
            var result = this._service.ClipPolygon(small, this._rect10, keepInside: true);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(4, result[0].Count);
            Assert.AreEqual(2, result[0][0].X, 1e-9);
            Assert.AreEqual(2, result[0][0].Y, 1e-9);
        }

        [Test]
        public void ClipPolygon_SubjectFullyOutside_ReturnsEmpty()
        {
            var outside = new List<Point2D>
            {
                new Point2D(20, 20),
                new Point2D(30, 20),
                new Point2D(30, 30),
                new Point2D(20, 30),
            };
            var result = this._service.ClipPolygon(outside, this._rect10, keepInside: true);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ClipPolygon_SubjectPartiallyOverlapping_ReturnsClipped()
        {
            var halfIn = new List<Point2D>
            {
                new Point2D(5, -5),
                new Point2D(15, -5),
                new Point2D(15, 5),
                new Point2D(5, 5),
            };
            var result = this._service.ClipPolygon(halfIn, this._rect10, keepInside: true);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(4, result[0].Count);
            Assert.That(result[0].Any(p => System.Math.Abs(p.X - 5) < 1e-9 && System.Math.Abs(p.Y - 0) < 1e-9));
            Assert.That(result[0].Any(p => System.Math.Abs(p.X - 10) < 1e-9 && System.Math.Abs(p.Y - 0) < 1e-9));
            Assert.That(result[0].Any(p => System.Math.Abs(p.X - 10) < 1e-9 && System.Math.Abs(p.Y - 5) < 1e-9));
            Assert.That(result[0].Any(p => System.Math.Abs(p.X - 5) < 1e-9 && System.Math.Abs(p.Y - 5) < 1e-9));
        }

        // ========== KeepInside 面积守恒验证 ==========

        [Test]
        public void ClipPolygon_Rect10x10_CropRect5x5_KeepInside_AreaEquals25()
        {
            var smallClip = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(5, 0),
                new Point2D(5, 5),
                new Point2D(0, 5),
            };
            var result = this._service.ClipPolygon(this._rect10, smallClip, keepInside: true);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(25.0, this.PolygonArea(result[0]), 1e-9);
        }

        [Test]
        public void ClipPolygon_Rect10x10_CropRectAtCorner_KeepInside_AreaEquals25()
        {
            var cornerClip = new List<Point2D>
            {
                new Point2D(5, 5),
                new Point2D(15, 5),
                new Point2D(15, 15),
                new Point2D(5, 15),
            };
            var result = this._service.ClipPolygon(this._rect10, cornerClip, keepInside: true);
            Assert.AreEqual(1, result.Count);
            this.AssertPolygonAreaClose(25.0, result[0]);
        }

        // ========== KeepOutside ==========

        [Test]
        public void ClipPolygon_Rect10x10_CropRect5x5_KeepOutside_AreaEquals75()
        {
            var smallClip = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(5, 0),
                new Point2D(5, 5),
                new Point2D(0, 5),
            };
            var result = this._service.ClipPolygon(this._rect10, smallClip, keepInside: false);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(75.0, this.PolygonArea(result[0]), 1e-9);
        }

        [Test]
        public void ClipPolygon_Rect10x10_CropCorner_KeepOutside_AreaEquals75()
        {
            // clip (5,5)-(15,5)-(15,15)-(5,15) 与被裁矩形 (0,0)-(10,10) 的
            // 交集为 (5,5)-(10,5)-(10,10)-(5,10)，面积 25.
            // keepOutside 差集面积 = 100 - 25 = 75.
            var cornerClip = new List<Point2D>
            {
                new Point2D(5, 5),
                new Point2D(15, 5),
                new Point2D(15, 15),
                new Point2D(5, 15),
            };
            var result = this._service.ClipPolygon(this._rect10, cornerClip, keepInside: false);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(75.0, this.PolygonArea(result[0]), 1.0,
                "差集面积 = 原始 100 - 交集 25 = 75");
        }

        // ========== 边界情况 ==========

        [Test]
        public void ClipPolygon_SubjectTouchingEdge_ReturnsPolygonOnEdge()
        {
            var touching = new List<Point2D>
            {
                new Point2D(0, 2),
                new Point2D(5, 2),
                new Point2D(5, 8),
                new Point2D(0, 8),
            };
            var result = this._service.ClipPolygon(touching, this._rect10, keepInside: true);
            Assert.AreEqual(1, result.Count);
            Assert.That(result[0].Count >= 4);
        }

        [Test]
        public void ClipPolygon_EmptySubject_ReturnsEmpty()
        {
            var result = this._service.ClipPolygon(new List<Point2D>(), this._rect10, keepInside: true);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ClipPolygon_EmptyClip_ReturnsEmpty()
        {
            var result = this._service.ClipPolygon(this._rect10, new List<Point2D>(), keepInside: true);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ClipPolygon_TriangleClip_WorksCorrectly()
        {
            var result = this._service.ClipPolygon(this._rect10, this._triangle, keepInside: true);
            Assert.AreEqual(1, result.Count);
            foreach (var pt in result[0])
            {
                var isInside = this.IsPointInTriangle(pt, this._triangle);
                Assert.IsTrue(isInside);
            }
        }

        // ============ CW 边界方向修复 ============
        // Sutherland-Hodgman 要求裁剪多边形为 CCW；从曲线采样可能得到 CW 边界.
        // 见 UID 20260617-172108-5745：CW 边界导致 IsInsideEdge 半平面反转，Hatch 被全部删除.
        // 修复：ClipPolygon 入口处调用 EnsureCCW() 将 CW 归一化为 CCW.

        [Test]
        public void CwBoundary_KeepInside_ProducesSameResultAsCcw()
        {
            // 同一矩形用两种方向定义
            var rectCcw = new List<Point2D> { new Point2D(0,0), new Point2D(10,0), new Point2D(10,10), new Point2D(0,10) };
            var rectCw  = new List<Point2D> { new Point2D(0,0), new Point2D(0,10), new Point2D(10,10), new Point2D(10,0) };

            var subject = new List<Point2D> { new Point2D(-5,5), new Point2D(15,5), new Point2D(15,15), new Point2D(-5,15) };

            var resultCcw = _service.ClipPolygon(subject, rectCcw, keepInside: true);
            var resultCw  = _service.ClipPolygon(subject, rectCw,  keepInside: true);

            Assert.AreEqual(1, resultCcw.Count, "CCW 边界应产生 1 个多边形");
            Assert.AreEqual(1, resultCw.Count,  "CW 边界也应产生 1 个多边形（归一化后等价）");
            Assert.AreEqual(resultCcw[0].Count, resultCw[0].Count, "两者顶点数应相同");
            // subject 从 y=5 到 y=15, clip 从 y=0 到 y=10 => 交集 y=5 到 y=10 = 高度 5
            Assert.AreEqual(50.0, this.PolygonArea(resultCcw[0]), 1.0,
                "CCW 交集面积应为 50（subject 与 clip 的 Y 重叠 5 单位）");
            Assert.AreEqual(50.0, this.PolygonArea(resultCw[0]),  1.0,
                "CW  交集面积同样应为 50（归一化后等价）");
        }

        [Test]
        public void CwBoundary_KeepOutside_ProducesSameResultAsCcw()
        {
            var rectCcw = new List<Point2D> { new Point2D(0,0), new Point2D(10,0), new Point2D(10,10), new Point2D(0,10) };
            var rectCw  = new List<Point2D> { new Point2D(0,0), new Point2D(0,10), new Point2D(10,10), new Point2D(10,0) };

            var subject = new List<Point2D> { new Point2D(-5,5), new Point2D(15,5), new Point2D(15,15), new Point2D(-5,15) };

            var resultCcw = _service.ClipPolygon(subject, rectCcw, keepInside: false);
            var resultCw  = _service.ClipPolygon(subject, rectCw,  keepInside: false);

            Assert.AreEqual(1, resultCcw.Count, "CCW keepOutside 应产生 1 个多边形");
            Assert.AreEqual(1, resultCw.Count,  "CW  keepOutside 也应产生 1 个多边形");
            var areaCcw = this.PolygonArea(resultCcw[0]);
            var areaCw  = this.PolygonArea(resultCw[0]);
            Assert.AreEqual(areaCcw, areaCw, 1.0, "两者面积应相同");
        }

        [Test]
        public void CwBoundary_SimulateBugRecord_RectInCwBoundary_KeepInside()
        {
            // 模拟 UID 20260617-172108-5745 的场景：
            // 矩形 Hatch (0,0)-(20,20)，裁剪边界为 CW 矩形 (5,5)-(15,15)
            var hatch = new List<Point2D> { new Point2D(0,0), new Point2D(20,0), new Point2D(20,20), new Point2D(0,20) };
            var boundaryCw = new List<Point2D> { new Point2D(5,5), new Point2D(5,15), new Point2D(15,15), new Point2D(15,5) };

            var result = _service.ClipPolygon(hatch, boundaryCw, keepInside: true);

            Assert.AreEqual(1, result.Count, "CW 边界裁剪：keepInside 应返回 1 个多边形（修复后）");
            Assert.Greater(this.PolygonArea(result[0]), 0, "面积应大于 0");
            // 交集应为 (5,5)-(15,5)-(15,15)-(5,15)，面积 100
            Assert.AreEqual(100.0, this.PolygonArea(result[0]), 1.0, "交集面积应为 100");
        }

        // ========== 辅助方法 ==========

        private bool IsPointInTriangle(Point2D pt, List<Point2D> tri)
        {
            var d1 = Sign(pt, tri[0], tri[1]);
            var d2 = Sign(pt, tri[1], tri[2]);
            var d3 = Sign(pt, tri[2], tri[0]);
            var hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            var hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        private static double Sign(Point2D p1, Point2D p2, Point2D p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        private bool IsPointInRect(Point2D pt, List<Point2D> rect)
        {
            return pt.X >= rect[0].X - 1e-9 && pt.X <= rect[2].X + 1e-9
                && pt.Y >= rect[0].Y - 1e-9 && pt.Y <= rect[2].Y + 1e-9;
        }

        private bool IsPointOnPolygonEdge(Point2D pt, List<Point2D> poly)
        {
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                var cross = (b.X - a.X) * (pt.Y - a.Y) - (b.Y - a.Y) * (pt.X - a.X);
                if (System.Math.Abs(cross) > 1e-9) continue;
                var dot = (pt.X - a.X) * (b.X - a.X) + (pt.Y - a.Y) * (b.Y - a.Y);
                var lenSq = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
                if (lenSq > 0 && dot >= -1e-9 && dot <= lenSq + 1e-9)
                    return true;
            }
            return false;
        }

        private void AssertPolygonAreaClose(double expected, IReadOnlyList<Point2D> poly)
        {
            var actual = this.PolygonArea(poly);
            Assert.AreEqual(expected, actual, 1e-9,
                $"多边形面积应接近 {expected}，实际为 {actual}");
        }

        private double PolygonArea(IReadOnlyList<Point2D> poly)
        {
            var n = poly.Count;
            var area = 0.0;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                area += poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
            }
            return System.Math.Abs(area) / 2.0;
        }
    }
}
