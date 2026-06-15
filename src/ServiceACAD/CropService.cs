using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropResult = ServiceACAD.OpResult<ServiceACAD.CropResult>;

namespace ServiceACAD
{
    public class CropService : ICropService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropService(ICropGeometryService cropGeometry)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
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
                    ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                    : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside || containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);
                
                bool shouldSplit = containment == DDNCadAddins.Core.Models.ContainmentResult.Intersects;

                if (shouldDelete)
                    return this.TryDeleteEntity(entity, result);
                if (shouldSplit)
                    return this.TrySplitEntity(entity, serviceTrans, result, keepInside, boundaryPoints);

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

        private bool TrySplitEntity(Entity entity, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            if (entity is Curve curve)
                return this.SplitCurve(curve, serviceTrans, result, keepInside, boundaryPoints);
            return this.TryDeleteEntity(entity, result);
        }

        private bool SplitCurve(Curve curve, ITransactionService serviceTrans, CropResult result, bool keepInside, IReadOnlyList<CorePoint2D> boundaryPoints)
        {
            try
            {
                if (curve is Line)
                    return this.SplitLine((Line)curve, serviceTrans, result, keepInside, boundaryPoints);
                return this.SplitGenericCurve(curve, serviceTrans, result, keepInside, boundaryPoints);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"拆分曲线失败: {ex.Message}");
                return this.TryDeleteEntity(curve, result);
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

                // 沿线节点序列：起点 + 已排序交点 + 终点，相邻节点构成一段
                var nodes = new List<Point3d> { start3d };
                foreach (var p in intersections)
                    nodes.Add(new Point3d(p.X, p.Y, InterpolateZ(start3d, end3d, p)));
                nodes.Add(end3d);

                // 逐段用中点判断在内/在外，决定保留还是裁掉
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

                // 没有任何交点：整条线在边界一侧，按中点结果保留或删除
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

                // 有交点但保留段为空：整条线被裁掉
                if (segmentsToKeep.Count == 0)
                    return this.TryDeleteEntity(line, result);

                // 删除原线，加入保留下来的分段
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

        /// <summary>
        ///     按 2D 投影参数在直线起止点之间线性插值出 Z 值.
        /// </summary>
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
