using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     Hatch 边界提取器 — 将 AutoCAD Hatch 实体的边界环（Loop）转换为 Core 层多边形.
    ///     处理关联/非关联 Hatch，支持 LineSegment2d / CircularArc2d / EllipticalArc2d / NurbCurve2d.
    ///     曲线生成委托给 <see cref="CurveToPolygonConverter"/>，自动选择精确/拟合策略.
    /// </summary>
    public class HatchBoundaryExtractor
    {
        private readonly CurveToPolygonConverter _generator;

        /// <summary>
        ///     默认构造函数（使用 CurveToPolygonConverter 默认实例）.
        /// </summary>
        public HatchBoundaryExtractor()
        {
            this._generator = new CurveToPolygonConverter();
        }

        /// <summary>
        ///     构造函数（注入 CurveToPolygonConverter，便于 Moq 测试）.
        /// </summary>
        /// <param name="generator">曲线→多边形转换器.</param>
        public HatchBoundaryExtractor(CurveToPolygonConverter generator)
        {
            this._generator = generator ?? throw new ArgumentNullException(nameof(generator));
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
                var polygon = this._generator.ConvertLoopToPolygon(loop);
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

                return this._generator.ConvertLoopToPolygon(loop);
            }
            catch (Exception ex)
            {
                Logger._.Error($"ExtractLoopBoundary 失败: {ex.Message}", ex);
                return Array.Empty<CorePoint2D>();
            }
        }
    }
}
