using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Models;
using ICropBoundary = DDNCadAddins.Core.Interfaces.ICropBoundary;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropResult = ServiceACAD.OpResult<ServiceACAD.CropResult>;

namespace ServiceACAD
{
    public class CropService : ICropService
    {
        private readonly ICropGeometryService _cropGeometry;
        private readonly CropPolylineService _polylineService;
        private readonly CropLineService _lineService;
        private readonly CropCircleService _circleService;
        private readonly CropArcService _arcService;
        private readonly CropSplineService _splineService;
        private readonly CropEllipseService _ellipseService;
        private readonly Crop3DPolylineService _polyline3dService;
        private readonly CropMLineService _mlineService;
        private readonly CropLeaderService _leaderService;
        private readonly CropHatchService _hatchService;
        private readonly CropBlockService _blockService;
        private readonly CropTextService _textService;
        private readonly CropMTextService _mtextService;
        private readonly CropDimService _dimService;
        private readonly CropPointService _pointService;
        private readonly CropSolidService _solidService;

        // ── 注册表：用字典消除 if-else 分派链 ──
        private delegate bool EntityHandler(Entity entity, ICropBoundary boundary,
            bool keepInside, ITransactionService ts, CropResult result);

        private readonly Dictionary<Type, EntityHandler> _curveHandlers;
        private readonly Dictionary<Type, EntityHandler> _nonCurveHandlers;

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

            this._curveHandlers = new Dictionary<Type, EntityHandler>
            {
                [typeof(Polyline)] = (e, bp, ki, ts, r) => this.HandlePolyline((Polyline)e, bp, ki, ts, r),
                [typeof(Line)] = (e, bp, ki, ts, r) => this.HandleLine((Line)e, bp, ki, ts, r),
                [typeof(Circle)] = (e, bp, ki, ts, r) => this.HandleCircle((Circle)e, bp, ki, ts, r),
                [typeof(Arc)] = (e, bp, ki, ts, r) => this.HandleArc((Arc)e, bp, ki, ts, r),
                [typeof(Spline)] = (e, bp, ki, ts, r) => this.HandleSpline((Spline)e, bp, ki, ts, r),
                [typeof(Ellipse)] = (e, bp, ki, ts, r) => this.HandleEllipse((Ellipse)e, bp, ki, ts, r),
                [typeof(Polyline3d)] = (e, bp, ki, ts, r) => this.HandlePolyline3d((Polyline3d)e, bp, ki, ts, r),
                [typeof(Leader)] = (e, bp, ki, ts, r) => this.HandleLeader((Leader)e, bp, ki, ts, r),
            };

            this._nonCurveHandlers = new Dictionary<Type, EntityHandler>
            {
                [typeof(Hatch)] = (e, bp, ki, ts, r) => this.HandleHatch((Hatch)e, bp, ki, ts, r),
                [typeof(BlockReference)] = (e, bp, ki, ts, r) => this.HandleBlockRef((BlockReference)e, bp, ki, ts, r),
                [typeof(DBText)] = (e, bp, ki, ts, r) => this.HandleDBText((DBText)e, bp, ki, ts, r),
                [typeof(MText)] = (e, bp, ki, ts, r) => this.HandleMText((MText)e, bp, ki, ts, r),
                [typeof(Dimension)] = (e, bp, ki, ts, r) => this.HandleDimension((Dimension)e, bp, ki, ts, r),
                [typeof(DBPoint)] = (e, bp, ki, ts, r) => this.HandleDBPoint((DBPoint)e, bp, ki, ts, r),
                [typeof(Solid)] = (e, bp, ki, ts, r) => this.HandleSolid((Solid)e, bp, ki, ts, r),
            };
        }

        public OpResultOfCropResult CropInside(CropInput input) => this.Crop(input, keepInside: true);
        public OpResultOfCropResult CropOutside(CropInput input) => this.Crop(input, keepInside: false);

        private OpResultOfCropResult Crop(CropInput input, bool keepInside)
        {
            try
            {
                if (input == null)
                    return OpResultOfCropResult.Fail("裁剪输入参数为空");
                if (input.EntityIds == null || input.EntityIds.Count == 0)
                    return OpResultOfCropResult.Fail("待裁剪的实体列表为空");
                if (input.TransactionService == null)
                    return OpResultOfCropResult.Fail("事务服务引用为空");

                // ★ 优先使用 ICropBoundary（精确圆/椭圆），兼容旧的多边形顶点
                var boundary = input.GetEffectiveBoundary();
                if (boundary == null)
                    return OpResultOfCropResult.Fail("裁剪边界无效（Boundary 和 BoundaryPoints 均未设置）");

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

                        var handled = this.CropEntity(entity, boundary, keepInside, input.TransactionService, result);
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

        private bool CropEntity(Entity entity, ICropBoundary boundary,
            bool keepInside, ITransactionService serviceTrans, CropResult result)
        {
            try
            {
                var extents = entity.GeometricExtents;
                if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9)
                    return false;

                // Spline/Ellipse/Polyline3d 使用精确参数搜索，跳过包围盒快速分类
                // （因为边界可能是采样多边形，包围盒分类可能误判相交为完全在内/外）
                if (entity is Spline || entity is Ellipse || entity is Polyline3d)
                {
                    return this.TrySplitOrProcessEntity(entity, serviceTrans, result, keepInside, boundary);
                }

                var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
                var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
                var containment = boundary.ClassifyBoundingBox(minPt, maxPt);

                bool shouldDelete = keepInside
                    ? containment == ContainmentResult.Outside
                    : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);

                bool shouldSplit = containment == ContainmentResult.Intersects;

                if (shouldDelete)
                    return this.TryDeleteEntity(entity, result);
                if (shouldSplit)
                    return this.TrySplitOrProcessEntity(entity, serviceTrans, result, keepInside, boundary);

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

        private bool TrySplitOrProcessEntity(Entity entity, ITransactionService serviceTrans,
            CropResult result, bool keepInside, ICropBoundary boundary)
        {
            if (entity is Curve curve)
                return this.SplitCurve(curve, serviceTrans, result, keepInside, boundary);
            return this.ProcessNonCurveEntity(entity, serviceTrans, result, keepInside, boundary);
        }

        // ════════════════════════════════════════════════════════════════
        //  曲线类型 — 字典查找替代 if-else
        // ════════════════════════════════════════════════════════════════

        private bool SplitCurve(Curve curve, ITransactionService serviceTrans,
            CropResult result, bool keepInside, ICropBoundary boundary)
        {
            try
            {
                if (this._curveHandlers.TryGetValue(curve.GetType(), out var handler))
                    return handler(curve, boundary, keepInside, serviceTrans, result);
                return this.SplitGenericCurve(curve, serviceTrans, result, keepInside, boundary);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"拆分曲线失败: {ex.Message}");
                return this.TryDeleteEntity(curve, result);
            }
        }

        private bool HandlePolyline(Polyline pline, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { pline.ObjectId };
            var result = ki
                ? this._polylineService.CropPolylinesInside(bp, ids, ts)
                : this._polylineService.CropPolylinesOutside(bp, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(pline, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            r.KeptCount += result.Data.KeptCount;
            r.SkippedCount += result.Data.SkippedCount;
            return true;
        }

        private bool HandleCircle(Circle circle, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { circle.ObjectId };
            var result = ki
                ? this._circleService.CropCirclesInside(bp, ids, ts)
                : this._circleService.CropCirclesOutside(bp, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(circle, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            r.KeptCount += result.Data.KeptCount;
            r.SkippedCount += result.Data.SkippedCount;
            return true;
        }

        private bool HandleArc(Arc arc, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { arc.ObjectId };
            var result = ki
                ? this._arcService.CropArcsInside(bp, ids, ts)
                : this._arcService.CropArcsOutside(bp, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(arc, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            r.KeptCount += result.Data.KeptCount;
            r.SkippedCount += result.Data.SkippedCount;
            return true;
        }

        private bool HandleLine(Line line, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { line.ObjectId };
            var result = ki
                ? this._lineService.CropLinesInside(bp, ids, ts)
                : this._lineService.CropLinesOutside(bp, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(line, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            r.KeptCount += result.Data.KeptCount;
            r.SkippedCount += result.Data.SkippedCount;
            return true;
        }

        private bool HandleSpline(Spline spline, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { spline.ObjectId };
            var result = ki
                ? this._splineService.CropSplinesInside(bp.GetApproximatePolygon(), ids, ts)
                : this._splineService.CropSplinesOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(spline, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            return true;
        }

        private bool HandleEllipse(Ellipse ellipse, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { ellipse.ObjectId };
            var result = ki
                ? this._ellipseService.CropEllipsesInside(bp.GetApproximatePolygon(), ids, ts)
                : this._ellipseService.CropEllipsesOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(ellipse, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            return true;
        }

        private bool HandlePolyline3d(Polyline3d pl3d, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { pl3d.ObjectId };
            var result = ki
                ? this._polyline3dService.Crop3DPolylinesInside(bp.GetApproximatePolygon(), ids, ts)
                : this._polyline3dService.Crop3DPolylinesOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(pl3d, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            return true;
        }

        private bool HandleLeader(Leader leader, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { leader.ObjectId };
            var result = ki
                ? this._leaderService.CropLeadersInside(bp.GetApproximatePolygon(), ids, ts)
                : this._leaderService.CropLeadersOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(leader, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  非曲线类型 — 字典查找替代 if-else
        // ════════════════════════════════════════════════════════════════

        private bool ProcessNonCurveEntity(Entity entity, ITransactionService serviceTrans,
            CropResult result, bool keepInside, ICropBoundary boundary)
        {
            try
            {
                if (this._nonCurveHandlers.TryGetValue(entity.GetType(), out var handler))
                    return handler(entity, boundary, keepInside, serviceTrans, result);
                return this.TryDeleteEntity(entity, result);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"处理非曲线实体失败: {ex.Message}");
                return this.TryDeleteEntity(entity, result);
            }
        }

        private bool HandleHatch(Hatch hatch, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { hatch.ObjectId };
            var result = ki
                ? this._hatchService.CropHatchesInside(bp.GetApproximatePolygon(), ids, ts)
                : this._hatchService.CropHatchesOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(hatch, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        private bool HandleBlockRef(BlockReference blkRef, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { blkRef.ObjectId };
            var result = ki
                ? this._blockService.CropBlocksInside(bp.GetApproximatePolygon(), ids, ts)
                : this._blockService.CropBlocksOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(blkRef, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        private bool HandleDBText(DBText text, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { text.ObjectId };
            var result = ki
                ? this._textService.CropTextsInside(bp.GetApproximatePolygon(), ids, ts)
                : this._textService.CropTextsOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(text, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        private bool HandleMText(MText mtext, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { mtext.ObjectId };
            var result = ki
                ? this._mtextService.CropMTextsInside(bp.GetApproximatePolygon(), ids, ts)
                : this._mtextService.CropMTextsOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(mtext, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        private bool HandleDimension(Dimension dim, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { dim.ObjectId };
            var result = ki
                ? this._dimService.CropDimsInside(bp.GetApproximatePolygon(), ids, ts)
                : this._dimService.CropDimsOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(dim, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        private bool HandleDBPoint(DBPoint pt, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { pt.ObjectId };
            var result = ki
                ? this._pointService.CropPointsInside(bp.GetApproximatePolygon(), ids, ts)
                : this._pointService.CropPointsOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(pt, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        private bool HandleSolid(Solid solid, ICropBoundary bp,
            bool ki, ITransactionService ts, CropResult r)
        {
            var ids = new List<ObjectId> { solid.ObjectId };
            var result = ki
                ? this._solidService.CropSolidsInside(bp.GetApproximatePolygon(), ids, ts)
                : this._solidService.CropSolidsOutside(bp.GetApproximatePolygon(), ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(solid, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.KeptCount += result.Data.KeptCount;
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  通用拆分方法（保持不变）
        // ════════════════════════════════════════════════════════════════

        private bool SplitLine(Line line, ITransactionService serviceTrans, CropResult result,
            bool keepInside, ICropBoundary boundary)
        {
            try
            {
                var start3d = line.StartPoint;
                var end3d = line.EndPoint;
                var startPt = new CorePoint2D(start3d.X, start3d.Y);
                var endPt = new CorePoint2D(end3d.X, end3d.Y);
                var intersections = boundary.FindLineIntersections(startPt, endPt);

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
                    var isInside = boundary.IsPointInside(midPt);
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

        private bool SplitGenericCurve(Curve curve, ITransactionService serviceTrans, CropResult result,
            bool keepInside, ICropBoundary boundary)
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
                    var midPt = new CorePoint2D(
                        (seg.StartPoint.X + seg.EndPoint.X) / 2.0,
                        (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0);
                    var isInside = boundary.IsPointInside(midPt);
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
                    var line = new Line(prevPoint, currPoint)
                    {
                        Layer = curve.Layer,
                        Color = curve.Color,
                        Linetype = curve.Linetype,
                    };
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
    }
}
