using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropPolylineResult = ServiceACAD.OpResult<ServiceACAD.CropPolylineResult>;

namespace ServiceACAD
{
    /// <summary>
    ///     多段线裁剪结果.
    /// </summary>
    public class CropPolylineResult
    {
        /// <summary>
        ///     被删除的多段线数量.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        ///     被拆分的多段线数量.
        /// </summary>
        public int SplitCount { get; set; }

        /// <summary>
        ///     保留的多段线数量（完全在目标侧无需处理）.
        /// </summary>
        public int KeptCount { get; set; }

        /// <summary>
        ///     跳过的多段线数量（无效或错误）.
        /// </summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     多段线裁剪服务 - 专门处理 Polyline 类型的裁剪操作.
    ///     支持保留边界内部或外部的多段线，自动将跨越边界的多段线拆分为多段.
    /// </summary>
    public class CropPolylineService
    {
        private readonly ICropGeometryService _cropGeometry;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="cropGeometry">几何计算服务，为空时使用默认实现.</param>
        public CropPolylineService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        /// <summary>
        ///     裁剪多段线：保留边界内部的多段线.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="polylineIds">待裁剪多段线的 ObjectId 列表.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        public OpResultOfCropPolylineResult CropPolylinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> polylineIds,
            ITransactionService transactionService)
        {
            return this.CropPolylines(boundaryPoints, polylineIds, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪多段线：保留边界外部的多段线.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="polylineIds">待裁剪多段线的 ObjectId 列表.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        public OpResultOfCropPolylineResult CropPolylinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> polylineIds,
            ITransactionService transactionService)
        {
            return this.CropPolylines(boundaryPoints, polylineIds, transactionService, keepInside: false);
        }

        /// <summary>
        ///     裁剪所有多段线：保留边界内部的多段线，自动选择图纸中所有 Polyline 对象.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果.</returns>
        public OpResultOfCropPolylineResult CropAllPolylinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllPolylines(boundaryPoints, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪所有多段线：保留边界外部的多段线，自动选择图纸中所有 Polyline 对象.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果.</returns>
        public OpResultOfCropPolylineResult CropAllPolylinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllPolylines(boundaryPoints, transactionService, keepInside: false);
        }

        /// <summary>
        ///     自动选择图纸中所有 Polyline 对象进行裁剪.
        /// </summary>
        private OpResultOfCropPolylineResult CropAllPolylines(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足（至少需要3个点）");
                }

                if (transactionService == null)
                {
                    return OpResultOfCropPolylineResult.Fail("事务服务引用为空");
                }

                // 获取模型空间中所有 Polyline 对象
                var allPolylineIds = transactionService.GetChildObjectsFromModelspace<Polyline>();

                if (allPolylineIds == null || allPolylineIds.Count == 0)
                {
                    return OpResultOfCropPolylineResult.Fail("图纸中没有找到任何多段线");
                }

                return this.CropPolylines(boundaryPoints, allPolylineIds, transactionService, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllPolylines 操作失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"自动裁剪多段线失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     核心多段线裁剪逻辑.
        /// </summary>
        private OpResultOfCropPolylineResult CropPolylines(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> polylineIds,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足（至少需要3个点）");
                }

                if (polylineIds == null || polylineIds.Count == 0)
                {
                    return OpResultOfCropPolylineResult.Fail("待裁剪的多段线列表为空");
                }

                if (transactionService == null)
                {
                    return OpResultOfCropPolylineResult.Fail("事务服务引用为空");
                }

                var result = new CropPolylineResult();

                foreach (var polylineId in polylineIds)
                {
                    try
                    {
                        if (!polylineId.IsValid || polylineId.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        var entity = transactionService.GetObject<Entity>(polylineId);
                        if (entity == null || entity.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        if (!(entity is Polyline polyline))
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        this.ProcessPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理多段线 {polylineId} 时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                if (result.DeletedCount == 0 && result.SplitCount == 0 && result.KeptCount == 0)
                {
                    return OpResultOfCropPolylineResult.Fail("没有多段线被处理");
                }

                return OpResultOfCropPolylineResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropPolylines 操作失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"多段线裁剪失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理单条多段线的裁剪：逐段计算与边界的交点，拆分并保留目标侧段.
        /// </summary>
        private void ProcessPolyline(
            Polyline polyline,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropPolylineResult result)
        {
            if (!polyline.Closed)
            {
                // 开放多段线：按线段拆分为独立 Line 段处理后再重组
                this.ProcessOpenPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
                return;
            }

            // 闭合多段线：先判断整体与边界的关系
            var extents = polyline.GeometricExtents;
            var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
            var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
            var containment = this._cropGeometry.ClassifyBoundingBox(minPt, maxPt, boundaryPoints);

            bool shouldDelete = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);

            if (shouldDelete)
            {
                this.DeletePolyline(polyline, result);
                return;
            }

            bool shouldSplit = containment == DDNCadAddins.Core.Models.ContainmentResult.Intersects;
            if (!shouldSplit)
            {
                result.KeptCount++;
                return;
            }

            // 闭合多段线需要拆分：采样为线段处理
            this.ProcessOpenPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
        }

        /// <summary>
        ///     处理开放多段线（或闭合多段线按线段处理）：逐段求交、拆分段、重组保留段.
        /// </summary>
        private void ProcessOpenPolyline(
            Polyline polyline,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropPolylineResult result)
        {
            try
            {
                var vertexCount = polyline.NumberOfVertices;
                if (vertexCount < 2)
                {
                    this.DeletePolyline(polyline, result);
                    return;
                }

                // 收集所有沿多段线的线段节点（顶点位置 + 交点位置）
                // 每条线段独立处理
                var segmentsToKeep = new List<List<Point2d>>();
                List<Point2d> currentGroup = null;

                // 遍历多段线的每条边
                for (var i = 0; i < vertexCount - 1; i++)
                {
                    var segType = polyline.GetSegmentType(i);
                    if (segType == SegmentType.Line)
                    {
                        var lineSeg = polyline.GetLineSegment2dAt(i);
                        var start2d = lineSeg.StartPoint;
                        var end2d = lineSeg.EndPoint;

                        var startCorePt = new CorePoint2D(start2d.X, start2d.Y);
                        var endCorePt = new CorePoint2D(end2d.X, end2d.Y);

                        var intersections = this._cropGeometry.FindLineSegmentIntersections(
                            startCorePt, endCorePt, boundaryPoints);

                        this.ProcessSegment(startCorePt, endCorePt, intersections, keepInside,
                            ref currentGroup, segmentsToKeep);
                    }
                    else if (segType == SegmentType.Arc)
                    {
                        // 弧线段：采样为多段直线段处理
                        var arcSeg = polyline.GetArcSegment2dAt(i);
                        var sampledStarts = new List<CorePoint2D>();
                        var sampledEnds = new List<CorePoint2D>();
                        this.SampleArcSegment(arcSeg, 16, sampledStarts, sampledEnds);

                        for (var j = 0; j < sampledStarts.Count; j++)
                        {
                            var intersections = this._cropGeometry.FindLineSegmentIntersections(
                                sampledStarts[j], sampledEnds[j], boundaryPoints);

                            this.ProcessSegment(sampledStarts[j], sampledEnds[j], intersections, keepInside,
                                ref currentGroup, segmentsToKeep);
                        }
                    }
                }

                // 处理闭合多段线的最后一段（连接最后一个顶点到第一个顶点）
                if (polyline.Closed)
                {
                    var segType = polyline.GetSegmentType(vertexCount - 1);
                    if (segType == SegmentType.Line)
                    {
                        var lineSeg = polyline.GetLineSegment2dAt(vertexCount - 1);
                        var start2d = lineSeg.StartPoint;
                        var end2d = lineSeg.EndPoint;
                        var startCorePt = new CorePoint2D(start2d.X, start2d.Y);
                        var endCorePt = new CorePoint2D(end2d.X, end2d.Y);
                        var intersections = this._cropGeometry.FindLineSegmentIntersections(
                            startCorePt, endCorePt, boundaryPoints);
                        this.ProcessSegment(startCorePt, endCorePt, intersections, keepInside,
                            ref currentGroup, segmentsToKeep);
                    }
                    else if (segType == SegmentType.Arc)
                    {
                        var arcSeg = polyline.GetArcSegment2dAt(vertexCount - 1);
                        var sampledStarts = new List<CorePoint2D>();
                        var sampledEnds = new List<CorePoint2D>();
                        this.SampleArcSegment(arcSeg, 16, sampledStarts, sampledEnds);
                        for (var j = 0; j < sampledStarts.Count; j++)
                        {
                            var intersections = this._cropGeometry.FindLineSegmentIntersections(
                                sampledStarts[j], sampledEnds[j], boundaryPoints);
                            this.ProcessSegment(sampledStarts[j], sampledEnds[j], intersections, keepInside,
                                ref currentGroup, segmentsToKeep);
                        }
                    }
                }

                // 结束当前组
                if (currentGroup != null && currentGroup.Count >= 2)
                {
                    segmentsToKeep.Add(currentGroup);
                }

                if (segmentsToKeep.Count == 0)
                {
                    this.DeletePolyline(polyline, result);
                    return;
                }

                // 创建新的多段线
                if (!polyline.IsWriteEnabled)
                {
                    polyline.UpgradeOpen();
                }

                polyline.Erase();

                foreach (var vertexList in segmentsToKeep)
                {
                    var newPoly = new Polyline();
                    newPoly.Layer = polyline.Layer;
                    newPoly.Color = polyline.Color;
                    newPoly.Linetype = polyline.Linetype;
                    newPoly.LineWeight = polyline.LineWeight;
                    newPoly.ConstantWidth = polyline.ConstantWidth;

                    for (var k = 0; k < vertexList.Count; k++)
                    {
                        newPoly.AddVertexAt(k, vertexList[k], 0.0, 0.0, 0.0);
                    }

                    transactionService.AppendEntityToCurrentSpace(newPoly);
                }

                result.SplitCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"拆分多段线失败 (ID={polyline.ObjectId}): {ex.Message}");
                this.DeletePolyline(polyline, result);
            }
        }

        /// <summary>
        ///     处理一个子线段：判断其各子段在边界内/外，分组保留目标侧段.
        /// </summary>
        /// <param name="segStart">线段起点.</param>
        /// <param name="segEnd">线段终点.</param>
        /// <param name="intersections">线段与边界的交点（已排序）.</param>
        /// <param name="keepInside">true 保留内部，false 保留外部.</param>
        /// <param name="currentGroup">当前正在积累的保留顶点组（可修改）.</param>
        /// <param name="segmentsToKeep">已完成的分组顶点列表集合.</param>
        private void ProcessSegment(
            CorePoint2D segStart,
            CorePoint2D segEnd,
            List<CorePoint2D> intersections,
            bool keepInside,
            ref List<Point2d> currentGroup,
            List<List<Point2d>> segmentsToKeep)
        {
            // 构造节点序列：起点 + 交点 + 终点
            var nodes = new List<CorePoint2D> { segStart };
            nodes.AddRange(intersections);
            nodes.Add(segEnd);

            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var a = nodes[i];
                var b = nodes[i + 1];

                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                if ((dx * dx) + (dy * dy) < 1e-12)
                {
                    continue;
                }

                var midPt = new CorePoint2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);

                if ((keepInside && isInside) || (!keepInside && !isInside))
                {
                    // 该段在目标侧
                    if (currentGroup == null)
                    {
                        currentGroup = new List<Point2d> { new Point2d(a.X, a.Y) };
                    }

                    currentGroup.Add(new Point2d(b.X, b.Y));
                }
                else
                {
                    // 该段不在目标侧：结束当前组
                    if (currentGroup != null && currentGroup.Count >= 2)
                    {
                        segmentsToKeep.Add(currentGroup);
                    }

                    currentGroup = null;
                }
            }
        }

        /// <summary>
        ///     将弧线段采样为多段直线段.
        /// </summary>
        private void SampleArcSegment(
            CircularArc2d arc,
            int sampleCount,
            List<CorePoint2D> starts,
            List<CorePoint2D> ends)
        {
            var startAngle = arc.StartAngle;
            var endAngle = arc.EndAngle;
            var totalAngle = endAngle - startAngle;

            var prevPt = arc.StartPoint;
            for (var i = 1; i <= sampleCount; i++)
            {
                var t = (double)i / sampleCount;
                var angle = startAngle + totalAngle * t;
                var currPt = arc.EvaluatePoint(angle);

                starts.Add(new CorePoint2D(prevPt.X, prevPt.Y));
                ends.Add(new CorePoint2D(currPt.X, currPt.Y));
                prevPt = currPt;
            }
        }

        /// <summary>
        ///     删除多段线并更新统计.
        /// </summary>
        private void DeletePolyline(Polyline polyline, CropPolylineResult result)
        {
            try
            {
                if (!polyline.IsWriteEnabled)
                {
                    polyline.UpgradeOpen();
                }

                polyline.Erase();
                result.DeletedCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"删除多段线失败 (ID={polyline.ObjectId}): {ex.Message}");
                result.SkippedCount++;
            }
        }
    }
}