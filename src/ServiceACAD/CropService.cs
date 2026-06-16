using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Models;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropResult = ServiceACAD.OpResult<ServiceACAD.CropResult>;

namespace ServiceACAD
{
    public class CropService : ICropService
    {
        private readonly ICropGeometryService _cropGeometry;
        // ── 精确求交服务 ──
        private readonly CropPolylineService _polylineService;
        private readonly CropLineService _lineService;
        private readonly CropCircleService _circleService;
        private readonly CropArcService _arcService;
        // ── 曲线采样服务（曲线专用）──
        private readonly CropSplineService _splineService;
        private readonly CropEllipseService _ellipseService;
        private readonly Crop3DPolylineService _polyline3dService;
        private readonly CropMLineService _mlineService;
        private readonly CropLeaderService _leaderService;
        // ── 非曲线服务（边界框分类）──
        private readonly CropHatchService _hatchService;
        private readonly CropBlockService _blockService;
        private readonly CropTextService _textService;
        private readonly CropMTextService _mtextService;
        private readonly CropDimService _dimService;
        private readonly CropPointService _pointService;
        private readonly CropSolidService _solidService;

        public CropService(ICropGeometryService cropGeometry)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
            this._polylineService = new CropPolylineService();
            this._lineService = new CropLineService();
            this._circleService = new CropCircleService(this._cropGeometry);
            this._arcService = new CropArcService(this._cropGeometry);
            this._splineService = new CropSplineService(this._cropGeometry);
            this._ellipseService = new CropEllipseService(this._cropGeometry);
            this._polyline3dService = new Crop3DPolylineService(this._cropGeometry);
            this._mlineService = new CropMLineService(this._cropGeometry);
            this._leaderService = new CropLeaderService(this._cropGeometry);
            this._hatchService = new CropHatchService(this._cropGeometry);
            this._blockService = new CropBlockService(this._cropGeometry);
            this._textService = new CropTextService(this._cropGeometry);
            this._mtextService = new CropMTextService(this._cropGeometry);
            this._dimService = new CropDimService(this._cropGeometry);
            this._pointService = new CropPointService(this._cropGeometry);
            this._solidService = new CropSolidService(this._cropGeometry);
        }

        public OpResultOfCropResult CropInside(CropInput input)
        {
            return this.Crop(input, keepInside: true);
        }

        public OpResultOfCropResult CropOutside(CropInput input)
        {
            return this.Crop(input, keepInside: false);
        }

        private OpResultOfCropResult Crop(CropInput input, bool keepInside)
        {
            try
            {
                if (input == null)
                    return OpResultOfCropResult.Fail("裁剪输入参数为空");
                if (input.BoundaryPoints == null || input.BoundaryPoints.Count < 3)
                    return OpResultOfCropResult.Fail("裁剪边界顶点不足");
                if (input.EntityIds == null || input.EntityIds.Count == 0)
                    return OpResultOfCropResult.Fail("待裁剪的实体列表为空");
                if (input.TransactionService == null)
                    return OpResultOfCropResult.Fail("事务服务引用为空");

                var result = new CropResult();
                foreach (var entityId in input.EntityIds)
                {
                    try
                    {
                        if (!entityId.IsValid || entityId.IsErased)
                            continue;

                        var entity = input.TransactionService.GetObject<Entity>(entityId);
                        if (entity == null || entity.IsErased)
                            continue;

                        var handled = this.CropEntity(entity, input.BoundaryPoints, keepInside, input.TransactionService, result);
                        if (!handled)
                        {
                            result.SkippedCount++;
                            Logger._.Warn($"跳过未识别的实体类型: {entity.GetType().Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"处理实体时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                if (result.DeletedCount == 0 && result.SplitCount == 0 && result.KeptCount == 0)
                    return OpResultOfCropResult.Fail("没有实体被处理");

                return OpResultOfCropResult.Success(result);
            }
            catch (Exception ex)
            {
                Logger._.Error($"Crop 操作失败: {ex.Message}", ex);
                return OpResultOfCropResult.Fail($"裁剪操作失败: {ex.Message}");
            }
        }

        private bool CropEntity(Entity entity, IReadOnlyList<CorePoint2D> boundaryPoints, bool keepInside, ITransactionService serviceTrans, CropResult result)
        {
            try
            {
                var extents = entity.GeometricExtents;
                if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9)
                    return false;

                var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
                var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
                var containment = this._cropGeometry.ClassifyBoundingBox(minPt, maxPt, boundaryPoints);

                bool shouldDelete = keepInside 
                    ? containment == ContainmentResult.Outside
                    : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);
                
                bool shouldSplit = containment == ContainmentResult.Intersects;

                if (shouldDelete)
                    return this.TryDeleteEntity(entity, result);
                if (shouldSplit)
                    return this.TrySplitOrProcessEntity(entity, serviceTrans, result, keepInside, boundaryPoints);

                result.KeptCount++;
                return true;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"CropEntity 失败: {ex.Message}");
                return false;
            }
        }

        private bool TryDeleteEntity(Entity entity, CropResult result)
        {
            try
            {
                if (!entity.IsWriteEnabled)
                    entity.UpgradeOpen();
                entity.Erase();
                result.DeletedCount++;
                return true;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"删除实体失败: {ex.Message}");
                result.SkippedCount++;
                return false;
            }
        }

        /// <summary>分发到曲线服务或非曲线 placeholder 服务.</summary>
        private bool TrySplitOrProcessEntity(Entity entity, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            if (entity is Curve curve)
                return this.SplitCurve(curve, serviceTrans, result, keepInside, boundaryPoints);
            return this.ProcessNonCurveEntity(entity, serviceTrans, result, keepInside, boundaryPoints);
        }

        private bool SplitCurve(Curve curve, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            try
            {
                var id = curve.ObjectId;
                var ids = new List<ObjectId> { id };

                // ── 精确求交谈型 ──
                if (curve is Polyline)
                {
                    if (keepInside)
                    {
                        var r = this._polylineService.CropPolylinesInside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    else
                    {
                        var r = this._polylineService.CropPolylinesOutside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    return true;
                }
                if (curve is Line)
                {
                    if (keepInside)
                    {
                        var r = this._lineService.CropLinesInside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    else
                    {
                        var r = this._lineService.CropLinesOutside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    return true;
                }
                if (curve is Circle)
                {
                    if (keepInside)
                    {
                        var r = this._circleService.CropCirclesInside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    else
                    {
                        var r = this._circleService.CropCirclesOutside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    return true;
                }
                if (curve is Arc)
                {
                    if (keepInside)
                    {
                        var r = this._arcService.CropArcsInside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    else
                    {
                        var r = this._arcService.CropArcsOutside(boundaryPoints, ids, serviceTrans);
                        if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                        result.DeletedCount += r.Data.DeletedCount;
                        result.SplitCount += r.Data.SplitCount;
                        result.KeptCount += r.Data.KeptCount;
                        result.SkippedCount += r.Data.SkippedCount;
                    }
                    return true;
                }
                // ── 采样型曲线 placeholder（采样 + 中点分类）──
                if (curve is Spline)
                {
                    var r = keepInside
                        ? this._splineService.CropSplinesInside(boundaryPoints, ids, serviceTrans)
                        : this._splineService.CropSplinesOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.SplitCount += r.Data.SplitCount;
                    return true;
                }
                if (curve is Ellipse)
                {
                    var r = keepInside
                        ? this._ellipseService.CropEllipsesInside(boundaryPoints, ids, serviceTrans)
                        : this._ellipseService.CropEllipsesOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.SplitCount += r.Data.SplitCount;
                    return true;
                }
                if (curve is Polyline3d)
                {
                    var r = keepInside
                        ? this._polyline3dService.Crop3DPolylinesInside(boundaryPoints, ids, serviceTrans)
                        : this._polyline3dService.Crop3DPolylinesOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.SplitCount += r.Data.SplitCount;
                    return true;
                }
                if (curve is Leader)
                {
                    var r = keepInside
                        ? this._leaderService.CropLeadersInside(boundaryPoints, ids, serviceTrans)
                        : this._leaderService.CropLeadersOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(curve, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.SplitCount += r.Data.SplitCount;
                    return true;
                }
                return this.SplitGenericCurve(curve, serviceTrans, result, keepInside, boundaryPoints);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"拆分曲线失败: {ex.Message}");
                return this.TryDeleteEntity(curve, result);
            }
        }

        /// <summary>非曲线实体 placeholder 分发（Hatch/BlockRef/Text/MText/Dim/Point）.</summary>
        private bool ProcessNonCurveEntity(Entity entity, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            try
            {
                var id = entity.ObjectId;
                var ids = new List<ObjectId> { id };

                if (entity is Hatch)
                {
                    var r = keepInside
                        ? this._hatchService.CropHatchesInside(boundaryPoints, ids, serviceTrans)
                        : this._hatchService.CropHatchesOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                if (entity is BlockReference)
                {
                    var r = keepInside
                        ? this._blockService.CropBlocksInside(boundaryPoints, ids, serviceTrans)
                        : this._blockService.CropBlocksOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                if (entity is DBText)
                {
                    var r = keepInside
                        ? this._textService.CropTextsInside(boundaryPoints, ids, serviceTrans)
                        : this._textService.CropTextsOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                if (entity is MText)
                {
                    var r = keepInside
                        ? this._mtextService.CropMTextsInside(boundaryPoints, ids, serviceTrans)
                        : this._mtextService.CropMTextsOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                if (entity is Dimension)
                {
                    var r = keepInside
                        ? this._dimService.CropDimsInside(boundaryPoints, ids, serviceTrans)
                        : this._dimService.CropDimsOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                if (entity is DBPoint)
                {
                    var r = keepInside
                        ? this._pointService.CropPointsInside(boundaryPoints, ids, serviceTrans)
                        : this._pointService.CropPointsOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                if (entity is Solid)
                {
                    var r = keepInside
                        ? this._solidService.CropSolidsInside(boundaryPoints, ids, serviceTrans)
                        : this._solidService.CropSolidsOutside(boundaryPoints, ids, serviceTrans);
                    if (!r.IsSuccess) return this.TryDeleteEntity(entity, result);
                    result.DeletedCount += r.Data.DeletedCount;
                    result.KeptCount += r.Data.KeptCount;
                    return true;
                }
                // 未知非曲线类型 → 直接删除
                return this.TryDeleteEntity(entity, result);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"处理非曲线实体失败: {ex.Message}");
                return this.TryDeleteEntity(entity, result);
            }
        }

        private bool SplitLine(Line line, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            try
            {
                var start3d = line.StartPoint;
                var end3d = line.EndPoint;
                var startPt = new CorePoint2D(start3d.X, start3d.Y);
                var endPt = new CorePoint2D(end3d.X, end3d.Y);
                var intersections = this._cropGeometry.FindLineSegmentIntersections(startPt, endPt, boundaryPoints);

                var nodes = new List<Point3d> { start3d };
                foreach (var p in intersections)
                    nodes.Add(new Point3d(p.X, p.Y, InterpolateZ(start3d, end3d, p)));
                nodes.Add(end3d);

                var segmentsToKeep = new List<Line>();
                for (var i = 0; i < nodes.Count - 1; i++)
                {
                    var a = nodes[i];
                    var b = nodes[i + 1];
                    if (a.DistanceTo(b) < 1e-9)
                        continue;

                    var midPt = new CorePoint2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                    var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);
                    if ((keepInside && isInside) || (!keepInside && !isInside))
                    {
                        var seg = new Line(a, b)
                        {
                            Layer = line.Layer,
                            Color = line.Color,
                            Linetype = line.Linetype,
                        };
                        segmentsToKeep.Add(seg);
                    }
                }

                if (intersections.Count == 0)
                {
                    foreach (var seg in segmentsToKeep)
                        seg.Dispose();

                    if (segmentsToKeep.Count > 0)
                    {
                        result.KeptCount++;
                        return true;
                    }

                    return this.TryDeleteEntity(line, result);
                }

                if (segmentsToKeep.Count == 0)
                    return this.TryDeleteEntity(line, result);

                if (!line.IsWriteEnabled)
                    line.UpgradeOpen();
                line.Erase();
                foreach (var seg in segmentsToKeep)
                    serviceTrans.AppendEntityToCurrentSpace(seg);
                result.SplitCount++;
                return true;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"拆分直线失败: {ex.Message}");
                return this.TryDeleteEntity(line, result);
            }
        }

        private static double InterpolateZ(Point3d start, Point3d end, CorePoint2D p)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lenSq = (dx * dx) + (dy * dy);
            if (lenSq < 1e-12)
                return start.Z;

            var t = (((p.X - start.X) * dx) + ((p.Y - start.Y) * dy)) / lenSq;
            return start.Z + (t * (end.Z - start.Z));
        }

        private bool SplitGenericCurve(Curve curve, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            try
            {
                var segments = SampleCurveIntoSegments(curve, 50);
                if (segments.Count == 0)
                    return this.TryDeleteEntity(curve, result);

                var allSegmentsToKeep = new List<Curve>();
                for (var i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    var midPt = new CorePoint2D((seg.StartPoint.X + seg.EndPoint.X) / 2.0, (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0);
                    var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);
                    if ((keepInside && isInside) || (!keepInside && !isInside))
                        allSegmentsToKeep.Add(seg);
                    else
                        seg.Dispose();
                }

                if (allSegmentsToKeep.Count > 0)
                {
                    if (!curve.IsWriteEnabled)
                        curve.UpgradeOpen();
                    curve.Erase();
                    foreach (var seg in allSegmentsToKeep)
                        serviceTrans.AppendEntityToCurrentSpace(seg);
                    result.SplitCount++;
                    result.DeletedCount++;
                }
                else
                {
                    foreach (var seg in allSegmentsToKeep)
                        seg.Dispose();
                    return this.TryDeleteEntity(curve, result);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"拆分通用曲线失败: {ex.Message}");
                return this.TryDeleteEntity(curve, result);
            }
        }

        private static List<Line> SampleCurveIntoSegments(Curve curve, int segmentCount)
        {
            var result = new List<Line>();
            try
            {
                var startParam = curve.StartParam;
                var endParam = curve.EndParam;
                var step = (endParam - startParam) / segmentCount;
                var prevPoint = curve.GetPointAtParameter(startParam);

                for (var i = 1; i <= segmentCount; i++)
                {
                    var param = startParam + (step * i);
                    if (param > endParam)
                        param = endParam;

                    var currPoint = curve.GetPointAtParameter(param);
                    var line = new Line(prevPoint, currPoint);
                    line.Layer = curve.Layer;
                    line.Color = curve.Color;
                    line.Linetype = curve.Linetype;
                    result.Add(line);
                    prevPoint = currPoint;
                }
            }
            catch (Exception ex)
            {
                Logger._.Warn($"采样曲线失败: {ex.Message}");
                foreach (var line in result)
                    line.Dispose();
                return new List<Line>();
            }
            return result;
        }
    }
}
