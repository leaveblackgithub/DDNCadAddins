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

        // ---- 公共接口 ----

        /// <summary>
        ///     裁剪多段线：保留边界内部的多段线.
        /// </summary>
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
        public OpResultOfCropPolylineResult CropPolylinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> polylineIds,
            ITransactionService transactionService)
        {
            return this.CropPolylines(boundaryPoints, polylineIds, transactionService, keepInside: false);
        }

        /// <summary>
        ///     裁剪所有多段线：保留边界内部，自动选择图纸中所有 Polyline 对象.
        /// </summary>
        public OpResultOfCropPolylineResult CropAllPolylinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllPolylines(boundaryPoints, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪所有多段线：保留边界外部，自动选择图纸中所有 Polyline 对象.
        /// </summary>
        public OpResultOfCropPolylineResult CropAllPolylinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllPolylines(boundaryPoints, transactionService, keepInside: false);
        }

        // ---- 私有实现 ----

        private OpResultOfCropPolylineResult CropAllPolylines(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足（至少需要3个点）");

                if (transactionService == null)
                    return OpResultOfCropPolylineResult.Fail("事务服务引用为空");

                var allPolylineIds = transactionService.GetChildObjectsFromModelspace<Polyline>();
                if (allPolylineIds == null || allPolylineIds.Count == 0)
                    return OpResultOfCropPolylineResult.Fail("图纸中没有找到任何多段线");

                return this.CropPolylines(boundaryPoints, allPolylineIds, transactionService, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllPolylines 操作失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"自动裁剪多段线失败: {ex.Message}");
            }
        }

        private OpResultOfCropPolylineResult CropPolylines(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> polylineIds,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足（至少需要3个点）");

                if (polylineIds == null || polylineIds.Count == 0)
                    return OpResultOfCropPolylineResult.Fail("待裁剪的多段线列表为空");

                if (transactionService == null)
                    return OpResultOfCropPolylineResult.Fail("事务服务引用为空");

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
                    return OpResultOfCropPolylineResult.Fail("没有多段线被处理");

                return OpResultOfCropPolylineResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropPolylines 操作失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"多段线裁剪失败: {ex.Message}");
            }
        }

        private void ProcessPolyline(
            Polyline polyline,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropPolylineResult result)
        {
            if (!polyline.Closed)
            {
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

            this.ProcessOpenPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
        }

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

                var segmentsToKeep = new List<List<Point2d>>();
                List<Point2d> currentGroup = null;

                // 遍历多段线的每条边（顶点 i → i+1）
                for (var i = 0; i < vertexCount - 1; i++)
                {
                    var segType = polyline.GetSegmentType(i);
                    if (segType == SegmentType.Line)
                    {
                        var lineSeg = polyline.GetLineSegment2dAt(i);
                        var startCorePt = new CorePoint2D(lineSeg.StartPoint.X, lineSeg.StartPoint.Y);
                        var endCorePt = new CorePoint2D(lineSeg.EndPoint.X, lineSeg.EndPoint.Y);

                        var intersections = this._cropGeometry.FindLineSegmentIntersections(
                            startCorePt, endCorePt, boundaryPoints);

                        this.ProcessSegment(startCorePt, endCorePt, intersections, keepInside,
                            ref currentGroup, segmentsToKeep, boundaryPoints);
                    }
                    else if (segType == SegmentType.Arc)
                    {
                        var arcSeg = polyline.GetArcSegment2dAt(i);
                        var sampledStarts = new List<CorePoint2D>();
                        var sampledEnds = new List<CorePoint2D>();
                        this.SampleArcSegment(arcSeg, 16, sampledStarts, sampledEnds);

                        for (var j = 0; j < sampledStarts.Count; j++)
                        {
                            var intersections = this._cropGeometry.FindLineSegmentIntersections(
                                sampledStarts[j], sampledEnds[j], boundaryPoints);

                            this.ProcessSegment(sampledStarts[j], sampledEnds[j], intersections, keepInside,
                                ref currentGroup, segmentsToKeep, boundaryPoints);
                        }
                    }
                }

                // 闭合多段线的最后一段（最后一个顶点 → 第一个顶点）
                if (polyline.Closed)
                {
                    var segType = polyline.GetSegmentType(vertexCount - 1);
                    if (segType == SegmentType.Line)
                    {
                        var lineSeg = polyline.GetLineSegment2dAt(vertexCount - 1);
                        var startCorePt = new CorePoint2D(lineSeg.StartPoint.X, lineSeg.StartPoint.Y);
                        var endCorePt = new CorePoint2D(lineSeg.EndPoint.X, lineSeg.EndPoint.Y);

                        var intersections = this._cropGeometry.FindLineSegmentIntersections(
                            startCorePt, endCorePt, boundaryPoints);
                        this.ProcessSegment(startCorePt, endCorePt, intersections, keepInside,
                            ref currentGroup, segmentsToKeep, boundaryPoints);
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
                                ref currentGroup, segmentsToKeep, boundaryPoints);
                        }
                    }
                }

                if (currentGroup != null && currentGroup.Count >= 2)
                    segmentsToKeep.Add(currentGroup);

                if (segmentsToKeep.Count == 0)
                {
                    this.DeletePolyline(polyline, result);
                    return;
                }

                if (!polyline.IsWriteEnabled)
                    polyline.UpgradeOpen();

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
                        newPoly.AddVertexAt(k, vertexList[k], 0.0, 0.0, 0.0);

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

        private void ProcessSegment(
            CorePoint2D segStart,
            CorePoint2D segEnd,
            List<CorePoint2D> intersections,
            bool keepInside,
            ref List<Point2d> currentGroup,
            List<List<Point2d>> segmentsToKeep,
            IReadOnlyList<CorePoint2D> boundaryPoints)
        {
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
                    continue;

                var midPt = new CorePoint2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);

                if ((keepInside && isInside) || (!keepInside && !isInside))
                {
                    if (currentGroup == null)
                        currentGroup = new List<Point2d> { new Point2d(a.X, a.Y) };

                    currentGroup.Add(new Point2d(b.X, b.Y));
                }
                else
                {
                    if (currentGroup != null && currentGroup.Count >= 2)
                        segmentsToKeep.Add(currentGroup);

                    currentGroup = null;
                }
            }
        }

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

        private void DeletePolyline(Polyline polyline, CropPolylineResult result)
        {
            try
            {
                if (!polyline.IsWriteEnabled)
                    polyline.UpgradeOpen();

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