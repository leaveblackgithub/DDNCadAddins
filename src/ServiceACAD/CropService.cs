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
                [typeof(Polyline)] = (e, bp, ki, ts, r) => this.CropCurveWithBoundary((Polyline)e, bp, ki, ts, r,
                    (b, ids, s) => ki ? this._polylineService.CropPolylinesInside(b, ids, s)
                                       : this._polylineService.CropPolylinesOutside(b, ids, s)),
                [typeof(Line)] = (e, bp, ki, ts, r) => this.CropCurveWithBoundary((Line)e, bp, ki, ts, r,
                    (b, ids, s) => ki ? this._lineService.CropLinesInside(b, ids, s)
                                       : this._lineService.CropLinesOutside(b, ids, s)),
                [typeof(Circle)] = (e, bp, ki, ts, r) => this.CropCurveWithBoundary((Circle)e, bp, ki, ts, r,
                    (b, ids, s) => ki ? this._circleService.CropCirclesInside(b, ids, s)
                                       : this._circleService.CropCirclesOutside(b, ids, s)),
                [typeof(Arc)] = (e, bp, ki, ts, r) => this.CropCurveWithBoundary((Arc)e, bp, ki, ts, r,
                    (b, ids, s) => ki ? this._arcService.CropArcsInside(b, ids, s)
                                       : this._arcService.CropArcsOutside(b, ids, s)),
                [typeof(Spline)] = (e, bp, ki, ts, r) => this.CropCurveWithPolygon((Spline)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._splineService.CropSplinesInside(p, ids, s)
                                       : this._splineService.CropSplinesOutside(p, ids, s)),
                [typeof(Ellipse)] = (e, bp, ki, ts, r) => this.CropCurveWithPolygon((Ellipse)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._ellipseService.CropEllipsesInside(p, ids, s)
                                       : this._ellipseService.CropEllipsesOutside(p, ids, s)),
                [typeof(Polyline3d)] = (e, bp, ki, ts, r) => this.CropCurveWithPolygon((Polyline3d)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._polyline3dService.Crop3DPolylinesInside(p, ids, s)
                                       : this._polyline3dService.Crop3DPolylinesOutside(p, ids, s)),
                [typeof(Leader)] = (e, bp, ki, ts, r) => this.CropCurveWithPolygon((Leader)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._leaderService.CropLeadersInside(p, ids, s)
                                       : this._leaderService.CropLeadersOutside(p, ids, s)),
            };

            this._nonCurveHandlers = new Dictionary<Type, EntityHandler>
            {
                [typeof(Hatch)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((Hatch)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._hatchService.CropHatchesInside(p, ids, s)
                                       : this._hatchService.CropHatchesOutside(p, ids, s)),
                [typeof(BlockReference)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((BlockReference)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._blockService.CropBlocksInside(p, ids, s)
                                       : this._blockService.CropBlocksOutside(p, ids, s)),
                [typeof(DBText)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((DBText)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._textService.CropTextsInside(p, ids, s)
                                       : this._textService.CropTextsOutside(p, ids, s)),
                [typeof(MText)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((MText)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._mtextService.CropMTextsInside(p, ids, s)
                                       : this._mtextService.CropMTextsOutside(p, ids, s)),
                [typeof(Dimension)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((Dimension)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._dimService.CropDimsInside(p, ids, s)
                                       : this._dimService.CropDimsOutside(p, ids, s)),
                [typeof(DBPoint)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((DBPoint)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._pointService.CropPointsInside(p, ids, s)
                                       : this._pointService.CropPointsOutside(p, ids, s)),
                [typeof(Solid)] = (e, bp, ki, ts, r) => this.CropNonCurveWithPolygon((Solid)e, bp, ki, ts, r,
                    (p, ids, s) => ki ? this._solidService.CropSolidsInside(p, ids, s)
                                       : this._solidService.CropSolidsOutside(p, ids, s)),
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

        /// <summary>
        ///     使用精确 ICropBoundary 裁剪曲线（Polyline/Line/Circle/Arc）.
        /// </summary>
        private bool CropCurveWithBoundary<T>(
            T entity, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropResult r,
            Func<ICropBoundary, List<ObjectId>, ITransactionService, dynamic> cropFunc)
            where T : Entity
        {
            var ids = new List<ObjectId> { entity.ObjectId };
            dynamic result = cropFunc(boundary, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(entity, r);
            r.DeletedCount += (int)result.Data.DeletedCount;
            r.SplitCount += (int)result.Data.SplitCount;
            r.KeptCount += (int)result.Data.KeptCount;
            r.SkippedCount += (int)result.Data.SkippedCount;
            return true;
        }

        /// <summary>
        ///     使用采样多边形裁剪曲线（Spline/Ellipse/Polyline3d/Leader）.
        /// </summary>
        private bool CropCurveWithPolygon<T>(
            T entity, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropResult r,
            Func<IReadOnlyList<CorePoint2D>, List<ObjectId>, ITransactionService, dynamic> cropFunc)
            where T : Entity
        {
            var ids = new List<ObjectId> { entity.ObjectId };
            var polygon = boundary.GetApproximatePolygon();
            dynamic result = cropFunc(polygon, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(entity, r);
            r.DeletedCount += (int)result.Data.DeletedCount;
            r.SplitCount += (int)result.Data.SplitCount;
            return true;
        }

        /// <summary>
        ///     使用采样多边形裁剪非曲线实体（Hatch/BlockRef/Text/Dim/Point/Solid）.
        /// </summary>
        private bool CropNonCurveWithPolygon<T>(
            T entity, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropResult r,
            Func<IReadOnlyList<CorePoint2D>, List<ObjectId>, ITransactionService, dynamic> cropFunc)
            where T : Entity
        {
            var ids = new List<ObjectId> { entity.ObjectId };
            var polygon = boundary.GetApproximatePolygon();
            dynamic result = cropFunc(polygon, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(entity, r);
            r.DeletedCount += (int)result.Data.DeletedCount;
            r.KeptCount += (int)result.Data.KeptCount;
            return true;
        }

        /// <summary>
        ///     非曲线类型裁剪处理 — 使用采样多边形边界.
        /// </summary>
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

        // ════════════════════════════════════════════════════════════════
        //  通用裁剪辅助方法（替代 16 个 HandleXXX 方法）
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     使用精确 ICropBoundary 裁剪曲线（Polyline/Line/Circle/Arc）.
        /// </summary>
        private bool CropCurveWithBoundary<T>(
            T entity, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropResult r,
            Func<ICropBoundary, List<ObjectId>, ITransactionService, OpResult<CropResult>> cropFunc)
            where T : Entity
        {
            var ids = new List<ObjectId> { entity.ObjectId };
            var result = cropFunc(boundary, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(entity, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            r.KeptCount += result.Data.KeptCount;
            r.SkippedCount += result.Data.SkippedCount;
            return true;
        }

        /// <summary>
        ///     使用采样多边形裁剪曲线（Spline/Ellipse/Polyline3d/Leader）.
        /// </summary>
        private bool CropCurveWithPolygon<T>(
            T entity, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropResult r,
            Func<IReadOnlyList<CorePoint2D>, List<ObjectId>, ITransactionService, OpResult<CropResult>> cropFunc)
            where T : Entity
        {
            var ids = new List<ObjectId> { entity.ObjectId };
            var polygon = boundary.GetApproximatePolygon();
            var result = cropFunc(polygon, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(entity, r);
            r.DeletedCount += result.Data.DeletedCount;
            r.SplitCount += result.Data.SplitCount;
            return true;
        }

        /// <summary>
        ///     使用采样多边形裁剪非曲线实体（Hatch/BlockRef/Text/Dim/Point/Solid）.
        /// </summary>
        private bool CropNonCurveWithPolygon<T>(
            T entity, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropResult r,
            Func<IReadOnlyList<CorePoint2D>, List<ObjectId>, ITransactionService, OpResult<CropResult>> cropFunc)
            where T : Entity
        {
            var ids = new List<ObjectId> { entity.ObjectId };
            var polygon = boundary.GetApproximatePolygon();
            var result = cropFunc(polygon, ids, ts);
            if (!result.IsSuccess) return this.TryDeleteEntity(entity, r);
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
