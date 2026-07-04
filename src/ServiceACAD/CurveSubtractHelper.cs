using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     曲线差集辅助器 — 从 SUBTRACTCLOSEDCURVE 命令提取的可复用工具.
    ///     提供曲线→精确段/裁剪边界转换、差集计算+绘制的共享方法.
    /// </summary>
    public static class CurveSubtractHelper
    {
        /// <summary>曲线选择结果（供 CROPHATCH 等命令复用）.</summary>
        public sealed class CurveSelection
        {
            /// <summary>曲线类型名称.</summary>
            public string Type;

            /// <summary>采样多边形顶点（用于 TestRecorder）.</summary>
            public List<CorePoint2D> Polygon;

            /// <summary>精确段列表（用于精确差集计算）.</summary>
            public List<ExactSegment> ExactSegments;

            /// <summary>精确裁剪边界（用于精确求交和包含测试）.</summary>
            public ICropBoundary Boundary;
        }

        /// <summary>
        ///     将闭合曲线转换为精确段列表和裁剪边界.
        ///     与 SUBTRACTCLOSEDCURVE 命令的 SelectClosedCurve 共享完全相同的转换路径.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="curveId">闭合曲线的 ObjectId.</param>
        /// <returns>曲线选择结果；转换失败返回 null.</returns>
        public static CurveSelection ConvertCurveToSelection(
            ITransactionService ts, ObjectId curveId)
        {
            try
            {
                if (curveId.IsNull || curveId.IsErased) return null;

                var curve = ts.GetObject<Curve>(curveId, OpenMode.ForRead);
                if (curve == null || !curve.Closed) return null;

                // 精确段列表
                var exactSegments = CurveToExactSegmentConverter.ConvertToExactSegments(curve);
                if (exactSegments == null || exactSegments.Count == 0) return null;

                // 精确裁剪边界
                var boundary = CurveToExactSegmentConverter.ConvertToCropBoundary(curve);
                if (boundary == null) return null;

                // 采样多边形
                var polygon = new CurveToPolygonConverter().ConvertCurveToPolygon(curve);
                if (polygon == null || polygon.Count < 3) return null;

                return new CurveSelection
                {
                    Type = curve.GetType().Name,
                    Polygon = polygon,
                    ExactSegments = exactSegments,
                    Boundary = boundary
                };
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"ConvertCurveToSelection 失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        ///     对两条曲线执行差集 A \ B 并绘制结果.
        ///     与 SUBTRACTCLOSEDCURVE 命令共享完全相同的差集 + 绘制路径.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="curveA">曲线 A 的选择结果.</param>
        /// <param name="curveB">曲线 B 的选择结果.</param>
        /// <returns>绘制的 Polyline 的 ObjectId 列表；失败返回空列表.</returns>
        public static List<ObjectId> SubtractAndDraw(
            ITransactionService ts, CurveSelection curveA, CurveSelection curveB)
        {
            var resultIds = new List<ObjectId>();
            try
            {
                var subtractService = new CurveSubtractService();
                var subtractResult = subtractService.Subtract(
                    curveA.ExactSegments, curveA.Boundary,
                    curveB.ExactSegments, curveB.Boundary);

                if (!subtractResult.IsSuccess || subtractResult.Data == null
                    || subtractResult.Data.IsEmpty)
                    return resultIds;

                // 记录绘制前的 Polyline 数量，用于识别新绘制的
                var beforePolys = ts.GetChildObjectsFromCurrentSpace<Polyline>();

                foreach (var loop in subtractResult.Data.Loops)
                {
                    if (loop == null || loop.Count == 0) continue;
                    CurveToExactSegmentConverter.DrawExactSegments(ts, loop, 3);
                }

                // 找出新添加的 Polyline
                var afterPolys = ts.GetChildObjectsFromCurrentSpace<Polyline>();
                foreach (var polyId in afterPolys)
                {
                    if (!beforePolys.Contains(polyId))
                        resultIds.Add(polyId);
                }
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"SubtractAndDraw 失败: {ex.Message}", ex);
            }
            return resultIds;
        }
    }
}
