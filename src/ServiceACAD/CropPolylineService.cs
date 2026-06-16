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
    public class CropPolylineResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     多段线裁剪服务 - 处理直线段用精确交点拆分，弧线段支持凸度保留.
    /// </summary>
    public class CropPolylineService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropPolylineService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        public OpResultOfCropPolylineResult CropPolylinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> polylineIds, ITransactionService transactionService)
            => this.CropPolylines(boundaryPoints, polylineIds, transactionService, keepInside: true);

        public OpResultOfCropPolylineResult CropPolylinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> polylineIds, ITransactionService transactionService)
            => this.CropPolylines(boundaryPoints, polylineIds, transactionService, keepInside: false);

        public OpResultOfCropPolylineResult CropAllPolylinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService transactionService)
            => this.CropAllPolylines(boundaryPoints, transactionService, keepInside: true);

        public OpResultOfCropPolylineResult CropAllPolylinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService transactionService)
            => this.CropAllPolylines(boundaryPoints, transactionService, keepInside: false);

        private OpResultOfCropPolylineResult CropAllPolylines(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService transactionService, bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足");
                if (transactionService == null)
                    return OpResultOfCropPolylineResult.Fail("事务服务引用为空");

                var allIds = transactionService.GetChildObjectsFromModelspace<Polyline>();
                if (allIds == null || allIds.Count == 0)
                    return OpResultOfCropPolylineResult.Fail("图纸中没有找到任何多段线");

                return this.CropPolylines(boundaryPoints, allIds, transactionService, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllPolylines 失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"自动裁剪多段线失败: {ex.Message}");
            }
        }

        private OpResultOfCropPolylineResult CropPolylines(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> polylineIds, ITransactionService transactionService, bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足");
                if (polylineIds == null || polylineIds.Count == 0)
                    return OpResultOfCropPolylineResult.Fail("待裁剪的多段线列表为空");
                if (transactionService == null)
                    return OpResultOfCropPolylineResult.Fail("事务服务引用为空");

                var result = new CropPolylineResult();
                foreach (var polylineId in polylineIds)
                {
                    try
                    {
                        if (!polylineId.IsValid || polylineId.IsErased) { result.SkippedCount++; continue; }
                        var entity = transactionService.GetObject<Entity>(polylineId);
                        if (entity == null || entity.IsErased) { result.SkippedCount++; continue; }
                        if (!(entity is Polyline polyline)) { result.SkippedCount++; continue; }
                        this.ProcessPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理多段线 {polylineId} 时异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                var total = result.DeletedCount + result.SplitCount + result.KeptCount;
                if (total == 0) return OpResultOfCropPolylineResult.Fail("没有多段线被处理");
                return OpResultOfCropPolylineResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropPolylines 失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"多段线裁剪失败: {ex.Message}");
            }
        }

        private void ProcessPolyline(
            Polyline polyline, IReadOnlyList<CorePoint2D> boundaryPoints, bool keepInside,
            ITransactionService transactionService, CropPolylineResult result)
        {
            if (!polyline.Closed)
            {
                this.ProcessOpenPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
                return;
            }

            var extents = polyline.GeometricExtents;
            var containment = this._cropGeometry.ClassifyBoundingBox(
                new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y),
                new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y),
                boundaryPoints);

            bool shouldDelete = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);

            if (shouldDelete) { this.DeletePolyline(polyline, result); return; }
            if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }

            this.ProcessOpenPolyline(polyline, boundaryPoints, keepInside, transactionService, result);
        }

        private void ProcessOpenPolyline(
            Polyline polyline, IReadOnlyList<CorePoint2D> boundaryPoints, bool keepInside,
            ITransactionService transactionService, CropPolylineResult result)
        {
            try
            {
                var vertexCount = polyline.NumberOfVertices;
                if (vertexCount < 2) { this.DeletePolyline(polyline, result); return; }

                // 逐段求交、逐子段判断保留/删除
                // 收集所有保留的段：[起点(AcGePoint2d), 终点, 凸度]
                var keptSegments = new List<Tuple<Point2d, Point2d, double>>();

                for (var i = 0; i < vertexCount - 1; i++)
                {
                    var segType = polyline.GetSegmentType(i);
                    if (segType == SegmentType.Line)
                    {
                        var lineSeg = polyline.GetLineSegment2dAt(i);
                        var startPt = new CorePoint2D(lineSeg.StartPoint.X, lineSeg.StartPoint.Y);
                        var endPt = new CorePoint2D(lineSeg.EndPoint.X, lineSeg.EndPoint.Y);
                        var intersections = this._cropGeometry.FindLineSegmentIntersections(startPt, endPt, boundaryPoints);

                        this.CollectKeptSubSegments(startPt, endPt, 0.0, intersections, keepInside, boundaryPoints, keptSegments);
                    }
                    else if (segType == SegmentType.Arc)
                    {
                        var arcSeg = polyline.GetArcSegment2dAt(i);
                        var bulge = polyline.GetBulgeAt(i);
                        this.ProcessPolyArcSegment(arcSeg, bulge, keepInside, boundaryPoints, keptSegments);
                    }
                }

                // 闭合多段线的最后一段（顶点 n-1 → 0）
                if (polyline.Closed)
                {
                    var segType = polyline.GetSegmentType(vertexCount - 1);
                    if (segType == SegmentType.Line)
                    {
                        var lineSeg = polyline.GetLineSegment2dAt(vertexCount - 1);
                        var startPt = new CorePoint2D(lineSeg.StartPoint.X, lineSeg.StartPoint.Y);
                        var endPt = new CorePoint2D(lineSeg.EndPoint.X, lineSeg.EndPoint.Y);
                        var intersections = this._cropGeometry.FindLineSegmentIntersections(startPt, endPt, boundaryPoints);
                        this.CollectKeptSubSegments(startPt, endPt, 0.0, intersections, keepInside, boundaryPoints, keptSegments);
                    }
                    else if (segType == SegmentType.Arc)
                    {
                        var arcSeg = polyline.GetArcSegment2dAt(vertexCount - 1);
                        var bulge = polyline.GetBulgeAt(vertexCount - 1);
                        this.ProcessPolyArcSegment(arcSeg, bulge, keepInside, boundaryPoints, keptSegments);
                    }
                }

                if (keptSegments.Count == 0) { this.DeletePolyline(polyline, result); return; }

                // 合并相邻段为连续多段线
                var chains = new List<List<Tuple<Point2d, Point2d, double>>>();
                foreach (var seg in keptSegments)
                {
                    if (chains.Count == 0)
                    {
                        chains.Add(new List<Tuple<Point2d, Point2d, double>> { seg });
                    }
                    else
                    {
                        var lastChain = chains[chains.Count - 1];
                        var lastSeg = lastChain[lastChain.Count - 1];
                        var dist = (lastSeg.Item2 - seg.Item1).Length;
                        if (dist < 1e-8)
                            lastChain.Add(seg);
                        else
                            chains.Add(new List<Tuple<Point2d, Point2d, double>> { seg });
                    }
                }

                if (!polyline.IsWriteEnabled) polyline.UpgradeOpen();
                polyline.Erase();

                foreach (var chain in chains)
                {
                    var newPoly = new Polyline();
                    newPoly.Layer = polyline.Layer;
                    newPoly.Color = polyline.Color;
                    newPoly.Linetype = polyline.Linetype;
                    newPoly.LineWeight = polyline.LineWeight;
                    newPoly.ConstantWidth = polyline.ConstantWidth;

                    newPoly.AddVertexAt(0, chain[0].Item1, chain[0].Item3, 0.0, 0.0);
                    for (var j = 0; j < chain.Count; j++)
                    {
                        newPoly.AddVertexAt(j + 1, chain[j].Item2, 0.0, 0.0, 0.0);
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
        ///     将直子线段按交点拆分，逐段中点判断保留/删除，收集保留段.
        /// </summary>
        private void CollectKeptSubSegments(
            CorePoint2D segStart, CorePoint2D segEnd, double bulge,
            List<CorePoint2D> intersections, bool keepInside,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<Tuple<Point2d, Point2d, double>> segments)
        {
            var nodes = new List<CorePoint2D> { segStart };
            nodes.AddRange(intersections);
            nodes.Add(segEnd);

            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var a = nodes[i];
                var b = nodes[i + 1];
                var d = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
                if (d < 1e-12) continue;

                var midPt = new CorePoint2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                var inside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);
                if ((keepInside && inside) || (!keepInside && !inside))
                {
                    segments.Add(Tuple.Create(new Point2d(a.X, a.Y), new Point2d(b.X, b.Y), bulge));
                }
            }
        }

        /// <summary>
        ///     处理多段线中的弧线段：采样为子弧→逐段中点判断→收集保留段（含凸度）.
        /// </summary>
        private void ProcessPolyArcSegment(
            CircularArc2d arc, double bulge, bool keepInside,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<Tuple<Point2d, Point2d, double>> segments)
        {
            const int samples = 16;
            var startAng = arc.StartAngle;
            var endAng = arc.EndAngle;
            var totalAng = endAng - startAng;

            var prevPt = arc.StartPoint;
            for (var i = 1; i <= samples; i++)
            {
                var t = (double)i / samples;
                var angle = startAng + totalAng * t;
                var currPt = arc.EvaluatePoint(angle);

                var startCore = new CorePoint2D(prevPt.X, prevPt.Y);
                var endCore = new CorePoint2D(currPt.X, currPt.Y);

                var intersections = this._cropGeometry.FindLineSegmentIntersections(startCore, endCore, boundaryPoints);

                // 子弧段的凸度：与原弧段相同（弧度比例）
                var subBulge = bulge / samples;

                this.CollectKeptSubSegments(startCore, endCore, subBulge, intersections, keepInside, boundaryPoints, segments);

                prevPt = currPt;
            }
        }

        private void DeletePolyline(Polyline polyline, CropPolylineResult result)
        {
            try
            {
                if (!polyline.IsWriteEnabled) polyline.UpgradeOpen();
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