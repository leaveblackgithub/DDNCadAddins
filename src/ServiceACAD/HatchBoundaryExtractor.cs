using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     Hatch 边界提取器 — 将 AutoCAD Hatch 实体的边界环（Loop）转换为 Core 层多边形.
    ///     处理关联/非关联 Hatch，支持 LineSegment2d / CircularArc2d / EllipticalArc2d / NurbCurve2d.
    ///     曲线采样算法委托给 <see cref="ICurveSampler"/>，可在 Core.Tests 中纯数据测试.
    /// </summary>
    public class HatchBoundaryExtractor
    {
        private readonly ICurveSampler _sampler;

        /// <summary>
        ///     默认构造函数（使用 CurveSampler 默认实例）.
        /// </summary>
        public HatchBoundaryExtractor()
            : this(new DDNCadAddins.Core.Services.CurveSampler())
        {
        }

        /// <summary>
        ///     构造函数（注入 ICurveSampler，便于 Moq 测试）.
        /// </summary>
        /// <param name="sampler">曲线采样器.</param>
        public HatchBoundaryExtractor(ICurveSampler sampler)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        }

        /// <summary>
        ///     提取 Hatch 的所有边界环，返回闭合多边形列表（每个环一个多边形）.
        /// </summary>
        /// <param name="hatch">Hatch 实体.</param>
        /// <returns>边界多边形列表（每个 List 是一个闭合环的顶点）.</returns>
        public IReadOnlyList<IReadOnlyList<CorePoint2D>> ExtractBoundaries(Hatch hatch)
        {
            if (hatch == null)
                throw new ArgumentNullException(nameof(hatch));

            var boundaries = new List<IReadOnlyList<CorePoint2D>>();
            var loopCount = hatch.NumberOfLoops;

            for (var i = 0; i < loopCount; i++)
            {
                var loop = hatch.GetLoopAt(i);
                var polygon = this.LoopToPolygon(loop);
                if (polygon.Count >= 3)
                    boundaries.Add(polygon);
            }

            return boundaries;
        }

        /// <summary>
        ///     提取 Hatch 的第一个边界环（外环）.
        /// </summary>
        public IReadOnlyList<CorePoint2D> ExtractOuterBoundary(Hatch hatch)
        {
            var boundaries = this.ExtractBoundaries(hatch);
            return boundaries.Count > 0 ? boundaries[0] : Array.Empty<CorePoint2D>();
        }

        /// <summary>
        ///     提取单个 HatchLoop 的闭合多边形（曲线型环）.
        ///     用于调用方按环逐个处理，避免依赖 ExtractBoundaries 返回列表的索引对齐.
        /// </summary>
        /// <param name="loop">Hatch 边界环.</param>
        /// <returns>闭合多边形顶点列表；非曲线环或顶点不足时返回空列表.</returns>
        public IReadOnlyList<CorePoint2D> ExtractLoopBoundary(HatchLoop loop)
        {
            try
            {
                if (loop == null)
                    return Array.Empty<CorePoint2D>();

                return this.LoopToPolygon(loop);
            }
            catch (Exception ex)
            {
                Logger._.Error($"ExtractLoopBoundary 失败: {ex.Message}", ex);
                return Array.Empty<CorePoint2D>();
            }
        }

        /// <summary>
        ///     将单个 HatchLoop 转换为闭合多边形顶点列表.
        /// </summary>
        private IReadOnlyList<CorePoint2D> LoopToPolygon(HatchLoop loop)
        {
            var points = new List<CorePoint2D>();

            if (loop.Curves != null)
            {
                foreach (Curve2d curve in loop.Curves)
                {
                    this.AddCurvePoints(curve, points);
                }
            }

            // 委托给 ICurveSampler 闭合多边形
            var closed = _sampler.ClosePolygon(points);

            // 去重
            return _sampler.RemoveAdjacentDuplicates(closed);
        }

        /// <summary>
        ///     将 Curve2d 的端点/采样点添加到列表中.
        /// </summary>
        private void AddCurvePoints(Curve2d curve, List<CorePoint2D> points)
        {
            if (curve is LineSegment2d line)
            {
                // 直线段 — 添加起点（终点由下一个线段提供）
                var start = line.StartPoint;
                points.Add(new CorePoint2D(start.X, start.Y));
            }
            else if (curve is CircularArc2d arc)
            {
                // 弧段 — 委托 ICurveSampler 采样
                var start = arc.StartPoint;
                var center = arc.Center;
                var arcPoints = _sampler.SampleArc(
                    start.X, start.Y,
                    center.X, center.Y,
                    arc.Radius,
                    arc.StartAngle, arc.EndAngle,
                    arc.IsClockWise);
                points.AddRange(arcPoints);
            }
            else if (curve is EllipticalArc2d ellipse)
            {
                // 椭圆弧 — 委托 ICurveSampler 采样
                var center = ellipse.Center;
                var majorRad = ellipse.MajorRadius;
                var minorRad = ellipse.MinorRadius;
                var minorRatio = minorRad / majorRad;
                var arcPoints = _sampler.SampleEllipticalArc(
                    center.X, center.Y,
                    majorRad, minorRatio,
                    ellipse.StartAngle, ellipse.EndAngle,
                    ellipse.IsClockWise);
                points.AddRange(arcPoints);
            }
            else if (curve is NurbCurve2d nurb)
            {
                // NURBS 曲线 — 委托 ICurveSampler 采样
                // ⚠ 关键：EvaluatePoint 的合法参数范围由 Knots 向量定义，并非固定的 [0,1].
                // 通过 Knots[degree] ~ Knots[n-degree-1] 获取实际范围，避免采样到曲线外.
                var startPt = new CorePoint2D(nurb.StartPoint.X, nurb.StartPoint.Y);
                var endPt   = new CorePoint2D(nurb.EndPoint.X, nurb.EndPoint.Y);
                var nurbPoints = _sampler.SampleGenericCurve(startPt, endPt, 32,
                    t =>
                    {
                        var param = this.MapNurbParameter(nurb, t);
                        var pt = nurb.EvaluatePoint(param);
                        return new CorePoint2D(pt.X, pt.Y);
                    });
                points.AddRange(nurbPoints);
            }
            else if (curve is PolylineCurve2d)
            {
                // PolylineCurve2d — 采样为直线段
                var startPt = new CorePoint2D(curve.StartPoint.X, curve.StartPoint.Y);
                var endPt = new CorePoint2D(curve.EndPoint.X, curve.EndPoint.Y);
                var polyPts = _sampler.SampleGenericCurve(startPt, endPt, 16,
                    t =>
                    {
                        var pt = curve.EvaluatePoint(t);
                        return new CorePoint2D(pt.X, pt.Y);
                    });
                points.AddRange(polyPts);
            }
        }

        /// <summary>
        ///     NURBS 曲线参数范围映射：将 t∈[0,1] 映射到 Knots[degree] ~ Knots[n-degree-1].
        ///     回退：若无法获取合法范围则直接返回 t.
        /// </summary>
        private double MapNurbParameter(NurbCurve2d nurb, double t)
        {
            try
            {
                int degree = nurb.Order - 1;
                if (degree < 0) return t;
                var knots = nurb.Knots;
                if (knots == null || knots.Count < degree * 2 + 2) return t;
                double tStart = knots[degree];
                double tEnd   = knots[knots.Count - degree - 1];
                double range  = tEnd - tStart;
                if (range <= 0) return tStart; // 退化 NURBS，取起点
                return tStart + range * t;
            }
            catch
            {
                return t; // 回退
            }
        }
    }
}
