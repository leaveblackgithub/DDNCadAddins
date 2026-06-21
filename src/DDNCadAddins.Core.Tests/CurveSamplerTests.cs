using System;
using System.Linq;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     CurveSampler 纯逻辑单元测试 — 不依赖 AutoCAD，纯数学运算。
    ///     覆盖：圆弧采样、椭圆弧采样、通用曲线采样、去重、闭合。
    /// </summary>
    [TestFixture]
    public class CurveSamplerTests
    {
        private ICurveSampler _sampler;

        [SetUp]
        public void SetUp()
        {
            this._sampler = new CurveSampler();
        }

        // ========== SampleArc ==========

        [Test]
        public void SampleArc_180Degree_ReturnsHalfCircle()
        {
            // 中心 (0,0)，半径 10，从 0 到 π（逆时针，上半圆）
            var points = this._sampler.SampleArc(
                10, 0,   // startX, startY
                0, 0,    // centerX, centerY
                10,      // radius
                0, Math.PI,   // startAngle=0, endAngle=π
                false);  // isClockwise=false (CCW)

            Assert.Greater(points.Count, 4, "半圆采样应有多个点");
            Assert.That(points[0].X, Is.EqualTo(10).Within(1e-10), "起点应在 (10,0)");
            Assert.That(points[0].Y, Is.EqualTo(0).Within(1e-10));
            Assert.That(points.Last().X, Is.EqualTo(-10).Within(1e-10), "终点应在 (-10,0)");
            Assert.That(points.Last().Y, Is.EqualTo(0).Within(1e-10));

            // 中间点应在上半圆（y>0）
            for (int i = 1; i < points.Count - 1; i++)
            {
                var pt = points[i];
                Assert.That(pt.Y, Is.GreaterThan(0), $"中间点 ({pt.X:F2},{pt.Y:F2}) 应在上半圆");
            }
        }

        [Test]
        public void SampleArc_Clockwise_ReturnsCorrectDirection()
        {
            // 中心 (5,5)，半径 5，从 π 到 0（顺时针，下半圆）
            var points = this._sampler.SampleArc(
                0, 5,    // startPt = arc.StartPoint
                5, 5,    // center
                5,       // radius
                Math.PI, 0,  // startAngle=π, endAngle=0
                true);   // isClockwise=true

            Assert.Greater(points.Count, 4, "顺时钟半圆应有多个点");
            Assert.That(points[0].X, Is.EqualTo(0).Within(1e-10), "起点应在 (0,5)");
        }

        [Test]
        public void SampleArc_FullCircle_ReturnsManyPoints()
        {
            // 中心 (0,0)，半径 5，完整 2π
            var points = this._sampler.SampleArc(
                5, 0, 0, 0, 5,
                0, 2 * Math.PI, false);

            Assert.Greater(points.Count, 16, "整圆应有较多采样点");
        }

        [Test]
        public void SampleArc_ZeroAngle_ReturnsDegenerate()
        {
            var points = this._sampler.SampleArc(
                5, 0, 0, 0, 5,
                0, 0, false);

            Assert.GreaterOrEqual(points.Count, 2);
        }

        // ========== SampleEllipticalArc ==========

        [Test]
        public void SampleEllipticalArc_180Degree_ReturnsPoints()
        {
            // 中心 (0,0)，majorRadius=10，minorRatio=0.5（Y 向短半轴 = 5）
            var points = this._sampler.SampleEllipticalArc(
                0, 0,    // center
                10,      // majorRadius
                0.5,     // minorRatio
                0, Math.PI,   // 0 到 π
                false);  // CCW

            Assert.Greater(points.Count, 4, "半椭圆应有多个点");
            Assert.That(points[0].X, Is.EqualTo(10).Within(1e-10), "起点应在 (10,0)");
        }

        [Test]
        public void SampleEllipticalArc_CircularRatio_MatchesArc()
        {
            // minorRatio=1.0 时，椭圆应等同于圆
            var ellipsePoints = this._sampler.SampleEllipticalArc(
                0, 0, 10, 1.0, 0, Math.PI, false);
            var arcPoints = this._sampler.SampleArc(
                10, 0, 0, 0, 10, 0, Math.PI, false);

            Assert.AreEqual(arcPoints.Count, ellipsePoints.Count);
            for (var i = 0; i < arcPoints.Count; i++)
            {
                Assert.That(ellipsePoints[i].X, Is.EqualTo(arcPoints[i].X).Within(1e-10));
                Assert.That(ellipsePoints[i].Y, Is.EqualTo(arcPoints[i].Y).Within(1e-10));
            }
        }

        // ========== SampleGenericCurve ==========

        [Test]
        public void SampleGenericCurve_Linear_ReturnsEndpoints()
        {
            // 直线段 evaluator
            var points = this._sampler.SampleGenericCurve(
                new Point2D(0, 0),
                new Point2D(100, 0),
                2,  // 2 段 = 3 个点
                t => new Point2D(t * 100, 0));

            Assert.AreEqual(3, points.Count);
            Assert.That(points[0].X, Is.EqualTo(0).Within(1e-10));
            Assert.That(points[1].X, Is.EqualTo(50).Within(1e-10));
            Assert.That(points[2].X, Is.EqualTo(100).Within(1e-10));
        }

        /// <summary>
        ///     退化曲线（startPoint==endPoint，例如闭合 NURBS）：
        ///     不再返回单点，而是通过 evaluator 全范围采样，确保闭合曲线形状正确。
        ///     如果 evaluator 本身是恒等函数，则所有采样点相同，经 RemoveAdjacentDuplicates 后会退化。
        /// </summary>
        [Test]
        public void SampleGenericCurve_ZeroLength_DegenerateEvaluator_ReturnsConstantPoints()
        {
            var points = this._sampler.SampleGenericCurve(
                new Point2D(5, 5),
                new Point2D(5, 5),
                10,
                t => new Point2D(5, 5));

            // 退化曲线返回 samples+1 个点（全范围采样）
            Assert.AreEqual(11, points.Count);
            foreach (var pt in points)
            {
                Assert.That(pt.X, Is.EqualTo(5).Within(1e-10));
                Assert.That(pt.Y, Is.EqualTo(5).Within(1e-10));
            }
        }

        /// <summary>
        ///     退化曲线 + 实际变值 evaluator（模拟闭合 NURBS 曲线）:
        ///     startPoint==endPoint 时正确采样整个曲线形状。
        /// </summary>
        [Test]
        public void SampleGenericCurve_ZeroLength_CircularEvaluator_ReturnsCirclePoints()
        {
            // 模拟闭合圆：EvaluatePoint(t) 返回半径为 10 的圆上的点
            var points = this._sampler.SampleGenericCurve(
                new Point2D(10, 0),   // start (角度 0)
                new Point2D(10, 0),   // end (角度 2π → 与 start 相同)
                8,                    // 8 段
                t =>
                {
                    var angle = 2.0 * Math.PI * t;
                    return new Point2D(10 * Math.Cos(angle), 10 * Math.Sin(angle));
                });

            // 8 段 = 9 个点 (i from 0 to 8)
            Assert.AreEqual(9, points.Count);
            // start/end 相同
            Assert.That(points[0].X, Is.EqualTo(10).Within(1e-10));
            Assert.That(points[0].Y, Is.EqualTo(0).Within(1e-10));
            Assert.That(points[8].X, Is.EqualTo(10).Within(1e-10));
            Assert.That(points[8].Y, Is.EqualTo(0).Within(1e-10));
            // 中间点应有正负分量（圆上不同的点）
            Assert.IsTrue(points[4].Y > 0 || points[4].Y < 0, "中间采样点不应全为零");
        }

        // ========== RemoveAdjacentDuplicates ==========

        [Test]
        public void RemoveAdjacentDuplicates_NoDuplicates_ReturnsOriginal()
        {
            var input = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
            };
            var result = this._sampler.RemoveAdjacentDuplicates(input);

            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void RemoveAdjacentDuplicates_AdjacentDuplicates_Removed()
        {
            var input = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 0),  // duplicate
                new Point2D(10, 10),
            };
            var result = this._sampler.RemoveAdjacentDuplicates(input);

            Assert.AreEqual(3, result.Count);
            Assert.That(result[1].X, Is.EqualTo(10));
            Assert.That(result[1].Y, Is.EqualTo(0));
            Assert.That(result[2].X, Is.EqualTo(10));
            Assert.That(result[2].Y, Is.EqualTo(10));
        }

        [Test]
        public void RemoveAdjacentDuplicates_AllSame_EmptyResult()
        {
            var input = new[]
            {
                new Point2D(5, 5),
                new Point2D(5, 5),
                new Point2D(5, 5),
            };
            var result = this._sampler.RemoveAdjacentDuplicates(input);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void RemoveAdjacentDuplicates_SinglePoint_ReturnsSingle()
        {
            var input = new[] { new Point2D(1, 2) };
            var result = this._sampler.RemoveAdjacentDuplicates(input);

            Assert.AreEqual(1, result.Count);
        }

        // ========== ClosePolygon ==========

        [Test]
        public void ClosePolygon_NotClosed_AddsFirstPoint()
        {
            var input = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
            };
            var result = this._sampler.ClosePolygon(input);

            Assert.AreEqual(4, result.Count, "应添加首点作为终点");
            Assert.That(result[3].X, Is.EqualTo(0));
            Assert.That(result[3].Y, Is.EqualTo(0));
        }

        [Test]
        public void ClosePolygon_AlreadyClosed_NotChanged()
        {
            var input = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 0),
            };
            var result = this._sampler.ClosePolygon(input);

            Assert.AreEqual(4, result.Count, "已闭合不应改变长度");
        }

        [Test]
        public void ClosePolygon_FewerThan3_NotChanged()
        {
            var input = new[] { new Point2D(0, 0), new Point2D(10, 0) };
            var result = this._sampler.ClosePolygon(input);

            Assert.AreEqual(2, result.Count);
        }

        // ========== 组合测试 ==========

        [Test]
        public void ClosePolygon_Then_RemoveAdjacent_WorksTogether()
        {
            // 模拟 HatchBoundaryExtractor.LoopToPolygon 的完整流程
            var input = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 0),  // duplicate
                new Point2D(10, 10),
            };
            var closed = this._sampler.ClosePolygon(input);
            Assert.AreEqual(5, closed.Count, "闭合后应添加首点");

            var deduped = this._sampler.RemoveAdjacentDuplicates(closed);
            // 去重后：回环检查会剔除首尾重复的 (0,0)，剩余 3 个不同顶点
            Assert.AreEqual(3, deduped.Count, "去重后应为 3 个不同顶点");
            Assert.That(deduped[0].X, Is.EqualTo(0));
            Assert.That(deduped[1].X, Is.EqualTo(10));
            Assert.That(deduped[2].Y, Is.EqualTo(10));
        }

        [Test]
        public void SampleArc_ThenCloseAndDedup_ReturnsClosedPolygon()
        {
            // 90 度圆弧 + 闭合 + 去重
            var arc = this._sampler.SampleArc(
                10, 0, 0, 0, 10,
                0, Math.PI / 2, false);

            var closed = this._sampler.ClosePolygon(arc);
            Assert.That(closed.Last().X, Is.EqualTo(arc[0].X).Within(1e-10));

            var deduped = this._sampler.RemoveAdjacentDuplicates(closed);
            Assert.That(deduped.Count, Is.LessThanOrEqualTo(closed.Count));
            Assert.Greater(deduped.Count, 2);
        }
    }
}
