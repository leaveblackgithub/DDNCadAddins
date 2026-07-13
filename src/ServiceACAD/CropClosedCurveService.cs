using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     闭合曲线裁剪服务 — 实现多条 Subject 曲线与一条 Clip 曲线的裁剪运算.
    ///     从 <c>CropClosedCurveCommand</c> 提取的核心方法，无 UI 交互.
    /// </summary>
    public static class CropClosedCurveService
    {
        /// <summary>曲线选择结果.</summary>
        public class CurveSelection
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

        /// <summary>裁剪计算结果.</summary>
        public class CropResult
        {
            /// <summary>操作是否成功.</summary>
            public bool IsSuccess { get; set; }

            /// <summary>结果消息.</summary>
            public string Message { get; set; }

            /// <summary>封闭环数量.</summary>
            public int PolyCount { get; set; }

            /// <summary>总顶点数.</summary>
            public int TotalVertices { get; set; }

            /// <summary>TestRecorder UID.</summary>
            public string Uid { get; set; }

            /// <summary>
            ///     裁剪后新创建的实体 ObjectId 列表（外环在前，内环在后）.
            ///     顺序由 CurveSubtractService 保证，可安全用于 Hatch 边界重建.
            /// </summary>
            public List<ObjectId> CreatedEntityIds { get; set; } = new List<ObjectId>();

            /// <summary>
            ///     裁剪后每个实体的面积（与 CreatedEntityIds 索引对齐）.
            ///     在裁剪过程中自动计算，无需调用方额外开事务.
            /// </summary>
            public double[] CreatedEntityAreas { get; set; } = Array.Empty<double>();

            /// <summary>
            ///     完整方法调用标志 — 用于 CropHatch 验证 CropClosedCurveMulti 打包方法被调用.
            ///     只有此标志为 true 的结果才能被 ProcessHatches 安全使用.
            /// </summary>
            public bool CalledFromCompleteMethod { get; set; }
        }

        /// <summary>
        ///     从 Curve ObjectId 创建 CurveSelection.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="curveId">闭合曲线的 ObjectId.</param>
        /// <returns>曲线选择结果；失败返回 null.</returns>
        public static CurveSelection CreateCurveSelection(ObjectId curveId)
        {
            if (curveId.IsNull || curveId.IsErased) return null;

            CurveSelection sel = null;
            CadServiceManager._.ExecuteInTransactions(null, ts =>
            {
                var curve = ts.GetObject<Curve>(curveId, OpenMode.ForRead);
                if (curve == null || !curve.Closed) return;

                var exactSegments = CurveToExactSegmentConverter.ConvertToExactSegments(curve);
                if (exactSegments == null || exactSegments.Count == 0) return;

                var boundary = CurveToExactSegmentConverter.ConvertToCropBoundary(curve);
                if (boundary == null) return;

                var polygon = new CurveToPolygonConverter().ConvertCurveToPolygon(curve);
                if (polygon == null || polygon.Count < 3) return;

                sel = new CurveSelection
                {
                    Type = curve.GetType().Name,
                    Polygon = polygon,
                    ExactSegments = exactSegments,
                    Boundary = boundary
                };
            });

            return sel;
        }

        /// <summary>
        ///     执行多条闭合曲线 A₁...Aₙ 与一条闭合曲线 B 的裁剪运算（ObjectId 重载）.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        ///     内部自动完成 CreateCurveSelection + 计算 + 绘制.
        /// </summary>
        /// <param name="subjectCurveIds">Subject 曲线的 ObjectId 列表.</param>
        /// <param name="clipCurveId">Clip 曲线 B 的 ObjectId.</param>
        /// <param name="keepInside">true=保留内部（交集 A∩B），false=保留外部（差集 A\B）.</param>
        /// <returns>裁剪计算结果.</returns>
        public static CropResult CropClosedCurveMulti(
            IReadOnlyList<ObjectId> subjectCurveIds, ObjectId clipCurveId,
            bool keepInside)
        {
            // 内部完成 CreateCurveSelection
            var subjectCurves = new List<CurveSelection>();
            foreach (var id in subjectCurveIds)
            {
                var sel = CreateCurveSelection(id);
                if (sel != null)
                    subjectCurves.Add(sel);
            }

            var clipCurve = CreateCurveSelection(clipCurveId);
            return CropClosedCurveMulti(subjectCurves, clipCurve, keepInside);
        }

        /// <summary>
        ///     执行多条闭合曲线 A₁...Aₙ 与一条闭合曲线 B 的裁剪运算.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="subjectCurves">Subject 曲线列表.</param>
        /// <param name="clipCurve">Clip 曲线 B.</param>
        /// <param name="keepInside">true=保留内部（交集 A∩B），false=保留外部（差集 A\B）.</param>
        /// <returns>裁剪计算结果.</returns>
        public static CropResult CropClosedCurveMulti(
            IReadOnlyList<CurveSelection> subjectCurves, CurveSelection clipCurve,
            bool keepInside)
        {
            var result = new CropResult();
            try
            {
                if (subjectCurves == null || subjectCurves.Count == 0)
                {
                    result.Message = "未选择 Subject 曲线。";
                    return result;
                }
                if (clipCurve == null)
                {
                    result.Message = "未选择 Clip 曲线。";
                    return result;
                }

                var subtractService = new CurveSubtractService();

                // 构建 Subject 元组列表
                var subjects = new List<(IReadOnlyList<ExactSegment> Edges, ICropBoundary Boundary)>();
                foreach (var subj in subjectCurves)
                {
                    subjects.Add((subj.ExactSegments, subj.Boundary));
                }

                // 根据方向选择算法
                ExactSubtractResult subtractResult;
                if (keepInside)
                {
                    // 保留内部 = 交集 A ∩ B
                    var serviceResult = subtractService.IntersectMultiSubject(
                        subjects, clipCurve.ExactSegments, clipCurve.Boundary);
                    subtractResult = serviceResult.IsSuccess ? serviceResult.Data : null;
                }
                else
                {
                    // 保留外部 = 差集 A \ B
                    var serviceResult = subtractService.SubtractMultiSubject(
                        subjects, clipCurve.ExactSegments, clipCurve.Boundary);
                    subtractResult = serviceResult.IsSuccess ? serviceResult.Data : null;
                }

                bool noResult = subtractResult == null || subtractResult.IsEmpty;
                int resultPolyCount = 0;
                int totalVertices = 0;

                if (!noResult)
                {
                    var areas = new List<double>();
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var loop in subtractResult.Loops)
                        {
                            if (loop == null || loop.Count == 0) continue;
                            var polyId = CurveToExactSegmentConverter.DrawExactSegments(ts, loop, 3);
                            if (!polyId.IsNull)
                            {
                                resultPolyCount++;
                                // 读取顶点数和面积用于统计
                                var pline = ts.GetObject<Polyline>(polyId);
                                if (pline != null)
                                {
                                    totalVertices += pline.NumberOfVertices;
                                    areas.Add(Math.Abs(pline.Area));
                                }
                                else
                                {
                                    areas.Add(0);
                                }
                                result.CreatedEntityIds.Add(polyId);
                            }
                        }
                    });
                    result.CreatedEntityAreas = areas.ToArray();
                }

                result.IsSuccess = resultPolyCount > 0;
                result.PolyCount = resultPolyCount;
                result.TotalVertices = totalVertices;
                result.CalledFromCompleteMethod = true;
                string directionLabel = keepInside ? "减掉外部-保留内部" : "减掉内部-保留外部";
                result.Message = resultPolyCount > 0
                    ? $"{directionLabel}: {resultPolyCount} 个封闭环，共 {totalVertices} 个顶点"
                    : noResult ? "无结果"
                               : "裁剪绘制失败";
            }
            catch (Exception ex)
            {
                Logger._.Error($"CROPCLOSEDCURVE 失败: {ex.Message}", ex);
                result.Message = $"CROPCLOSEDCURVE 失败: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        ///     执行两条闭合曲线的精确裁剪运算（单 Subject 兼容重载）.
        /// </summary>
        public static CropResult CropClosedCurve(CurveSelection curveA, CurveSelection curveB, bool keepInside)
        {
            return CropClosedCurveMulti(new[] { curveA }, curveB, keepInside);
        }

        /// <summary>
        ///     执行"外环+内环（孔洞）"与一条裁剪曲线的精确裁剪运算，正确处理
        ///     裁剪边界同时与外环、内环相交的场景（凹字形结果）.
        ///     <para>
        ///         与 <see cref="CropClosedCurveMulti"/> 的区别：后者将外环、内环视为
        ///         互相独立的 Subject，各自与 Clip 求交/差集后再合并，当 Clip 跨越
        ///         内外环边界时会产生错误结果（内环孔洞区域被错误地并入结果，或外环
        ///         与内环的裁剪结果未正确挖孔）。此方法将内环视为外环的孔洞，
        ///         内环区域始终不属于结果，无论裁剪方向如何.
        ///     </para>
        ///     <para>
        ///         语义：
        ///         <list type="bullet">
        ///             <item>keepInside=true：结果 = 外环以内 ∩ Clip 以内 ∩ 内环以外.</item>
        ///             <item>keepInside=false：结果 = 外环以内 ∩ Clip 以外 ∩ 内环以外.</item>
        ///         </list>
        ///     </para>
        /// </summary>
        /// <param name="outerRing">外环曲线选择结果.</param>
        /// <param name="holeRing">内环（孔洞）曲线选择结果.</param>
        /// <param name="clipCurve">裁剪曲线 B.</param>
        /// <param name="keepInside">true=保留内部（交集语义），false=保留外部（差集语义）.</param>
        /// <returns>裁剪计算结果.</returns>
        public static CropResult CropRingWithHole(
            CurveSelection outerRing, CurveSelection holeRing, CurveSelection clipCurve,
            bool keepInside)
        {
            var result = new CropResult();
            try
            {
                if (outerRing == null)
                {
                    result.Message = "未选择外环曲线。";
                    return result;
                }
                if (holeRing == null)
                {
                    result.Message = "未选择内环曲线。";
                    return result;
                }
                if (clipCurve == null)
                {
                    result.Message = "未选择 Clip 曲线。";
                    return result;
                }

                var subtractService = new CurveSubtractService();
                var serviceResult = subtractService.CropRingWithHole(
                    outerRing.ExactSegments, outerRing.Boundary,
                    holeRing.ExactSegments, holeRing.Boundary,
                    clipCurve.ExactSegments, clipCurve.Boundary,
                    keepInside);

                var subtractResult = serviceResult.IsSuccess ? serviceResult.Data : null;
                bool noResult = subtractResult == null || subtractResult.IsEmpty;
                int resultPolyCount = 0;
                int totalVertices = 0;

                if (!noResult)
                {
                    var areas = new List<double>();
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var loop in subtractResult.Loops)
                        {
                            if (loop == null || loop.Count == 0) continue;
                            var polyId = CurveToExactSegmentConverter.DrawExactSegments(ts, loop, 3);
                            if (!polyId.IsNull)
                            {
                                resultPolyCount++;
                                var pline = ts.GetObject<Polyline>(polyId);
                                if (pline != null)
                                {
                                    totalVertices += pline.NumberOfVertices;
                                    areas.Add(Math.Abs(pline.Area));
                                }
                                else
                                {
                                    areas.Add(0);
                                }
                                result.CreatedEntityIds.Add(polyId);
                            }
                        }
                    });
                    result.CreatedEntityAreas = areas.ToArray();
                }

                result.IsSuccess = resultPolyCount > 0;
                result.PolyCount = resultPolyCount;
                result.TotalVertices = totalVertices;
                result.CalledFromCompleteMethod = true;
                string directionLabel = keepInside ? "保留内部(交集,挖孔)" : "保留外部(差集,挖孔)";
                result.Message = resultPolyCount > 0
                    ? $"{directionLabel}: {resultPolyCount} 个封闭环，共 {totalVertices} 个顶点"
                    : !serviceResult.IsSuccess ? serviceResult.Message
                                : noResult ? "无结果"
                                           : "裁剪绘制失败";
            }
            catch (Exception ex)
            {
                Logger._.Error($"CropRingWithHole 失败: {ex.Message}", ex);
                result.Message = $"CropRingWithHole 失败: {ex.Message}";
            }
            return result;
        }
    }
}
