using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropCircleResult = ServiceACAD.OpResult<ServiceACAD.CropCircleResult>;

namespace ServiceACAD
{
    public class CropCircleResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     圆裁剪服务 — 求圆与边界多边形的精确交点，按交点拆分为 Arc，逐弧段中点判断保留/删除.
    /// </summary>
    public class CropCircleService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropCircleService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        public OpResultOfCropCircleResult CropCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> circleIds, ITransactionService transactionService)
            => this.CropCircles(boundaryPoints, circleIds, transactionService, keepInside: true);

        public OpResultOfCropCircleResult CropCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> circleIds, ITransactionService transactionService)
            => this.CropCircles(boundaryPoints, circleIds, transactionService, keepInside: false);

        public OpResultOfCropCircleResult CropAllCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService transactionService)
            => this.CropAllCircles(boundaryPoints, transactionService, keepInside: true);

        public OpResultOfCropCircleResult CropAllCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService transactionService)
            => this.CropAllCircles(boundaryPoints, transactionService, keepInside: false);

        private OpResultOfCropCircleResult CropAllCircles(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService transactionService, bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropCircleResult.Fail("裁剪边界顶点不足");
                if (transactionService == null)
                    return OpResultOfCropCircleResult.Fail("事务服务引用为空");

                var allIds = transactionService.GetChildObjectsFromModelspace<Circle>();
                if (allIds == null || allIds.Count == 0)
                    return OpResultOfCropCircleResult.Fail("图纸中没有找到任何圆");

                return this.CropCircles(boundaryPoints, allIds, transactionService, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllCircles 操作失败: {ex.Message}", ex);
                return OpResultOfCropCircleResult.Fail($"自动裁剪圆失败: {ex.Message}");
            }
        }

        private OpResultOfCropCircleResult CropCircles(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> circleIds, ITransactionService transactionService, bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropCircleResult.Fail("裁剪边界顶点不足");
                if (circleIds == null || circleIds.Count == 0)
                    return OpResultOfCropCircleResult.Fail("待裁剪的圆列表为空");
                if (transactionService == null)
                    return OpResultOfCropCircleResult.Fail("事务服务引用为空");

                var result = new CropCircleResult();
                foreach (var circleId in circleIds)
                {
                    try
                    {
                        if (!circleId.IsValid || circleId.IsErased) { result.SkippedCount++; continue; }
                        var entity = transactionService.GetObject<Entity>(circleId);
                        if (entity == null || entity.IsErased) { result.SkippedCount++; continue; }
                        if (!(entity is Circle circle)) { result.SkippedCount++; continue; }
                        this.ProcessCircle(circle, boundaryPoints, keepInside, transactionService, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理圆 {circleId} 时异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                var total = result.DeletedCount + result.SplitCount + result.KeptCount;
                if (total == 0)
                {
                    if (result.SkippedCount > 0)
                        return OpResultOfCropCircleResult.Success(result);
                    return OpResultOfCropCircleResult.Fail("没有圆被处理");
                }
                return OpResultOfCropCircleResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropCircles 失败: {ex.Message}", ex);
                return OpResultOfCropCircleResult.Fail($"圆裁剪失败: {ex.Message}");
            }
        }

        private void ProcessCircle(
            Circle circle, IReadOnlyList<CorePoint2D> boundaryPoints, bool keepInside,
            ITransactionService transactionService, CropCircleResult result)
        {
            var extents = circle.GeometricExtents;
            if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9) return;

            var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
            var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
            var containment = this._cropGeometry.ClassifyBoundingBox(minPt, maxPt, boundaryPoints);

            bool shouldDelete = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);

            if (shouldDelete) { this.DeleteCircle(circle, result); return; }
            if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }

            this.SplitCircleAndKeep(circle, boundaryPoints, keepInside, transactionService, result);
        }

        /// <summary>
        ///     圆与边界多边形各边求精确交点，交点转角度排序，逐弧段中点判断保留/删除.
        /// </summary>
        private void SplitCircleAndKeep(
            Circle circle, IReadOnlyList<CorePoint2D> boundaryPoints, bool keepInside,
            ITransactionService transactionService, CropCircleResult result)
        {
            try
            {
                var centerX = circle.Center.X;
                var centerY = circle.Center.Y;
                var radius = circle.Radius;
                var normal = circle.Normal;

                // 1. 求圆与多边形各边的精确交点
                var allIx = new List<CorePoint2D>();
                for (int i = 0, j = boundaryPoints.Count - 1; i < boundaryPoints.Count; j = i++)
                {
                    var p1 = boundaryPoints[j];
                    var p2 = boundaryPoints[i];
                    var segInters = this.LineCircleIntersection(
                        p1.X, p1.Y, p2.X, p2.Y, centerX, centerY, radius);
                    foreach (var pt in segInters)
                    {
                        if (this.PointOnSegment(pt, p1, p2))
                            allIx.Add(pt);
                    }
                }

                // 2. 脱重（距离 < 1e-6 的点合并为一个）
                var deduped = new List<CorePoint2D>();
                foreach (var pt in allIx)
                {
                    var isDup = false;
                    foreach (var existing in deduped)
                    {
                        var dx = pt.X - existing.X;
                        var dy = pt.Y - existing.Y;
                        if ((dx * dx + dy * dy) < 1e-10) { isDup = true; break; }
                    }
                    if (!isDup) deduped.Add(pt);
                }

                // 3. 交点转为角度 [0, 2π)
                var intersectionAngles = new List<double>();
                foreach (var pt in deduped)
                {
                    var angle = Math.Atan2(pt.Y - centerY, pt.X - centerX);
                    if (angle < 0) angle += 2.0 * Math.PI;
                    intersectionAngles.Add(angle);
                }

                // 4. 排序交点角度
                intersectionAngles.Sort();

                // 5. 构造节点序列: 0 → 交点 → 2π
                var angleNodes = new List<double> { 0.0 };
                angleNodes.AddRange(intersectionAngles);
                angleNodes.Add(2.0 * Math.PI);

                // 6. 逐弧段中点判断保留/删除
                var arcsToKeep = new List<Tuple<double, double>>();
                for (var i = 0; i < angleNodes.Count - 1; i++)
                {
                    var a = angleNodes[i];
                    var b = angleNodes[i + 1];
                    if (Math.Abs(b - a) < 1e-9) continue;

                    var midAngle = (a + b) / 2.0;
                    var midX = centerX + radius * Math.Cos(midAngle);
                    var midY = centerY + radius * Math.Sin(midAngle);
                    var inside = this._cropGeometry.IsPointInPolygon(new CorePoint2D(midX, midY), boundaryPoints);

                    if ((keepInside && inside) || (!keepInside && !inside))
                        arcsToKeep.Add(Tuple.Create(a, b));
                }

                // 7. 无保留弧段则删除
                if (arcsToKeep.Count == 0) { this.DeleteCircle(circle, result); return; }

                // 无交点且全部保留 → 保留原圆
                if (intersectionAngles.Count == 0 && arcsToKeep.Count == 1) { result.KeptCount++; return; }

                // 8. 删除原圆，创建保留的 Arc
                if (!circle.IsWriteEnabled) circle.UpgradeOpen();
                circle.Erase();

                foreach (var seg in arcsToKeep)
                {
                    var startA = seg.Item1;
                    var endA = seg.Item2;
                    // 避免 0 长度弧段
                    if (Math.Abs(endA - startA) < 1e-9) continue;

                    var newArc = new Arc(circle.Center, normal, radius, startA, endA);
                    newArc.Layer = circle.Layer;
                    newArc.Color = circle.Color;
                    newArc.Linetype = circle.Linetype;
                    newArc.LineWeight = circle.LineWeight;
                    transactionService.AppendEntityToCurrentSpace(newArc);
                }

                result.SplitCount++;
                result.DeletedCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"拆分圆失败 (ID={circle.ObjectId}): {ex.Message}");
                this.DeleteCircle(circle, result);
            }
        }

        // ── 辅助: 圆与直线的交点 ──

        /// <summary>
        ///     计算圆 (cx, cy, r) 与线段两端所在直线的交点（不检查线段范围）.
        /// </summary>
        private List<CorePoint2D> LineCircleIntersection(
            double x1, double y1, double x2, double y2,
            double cx, double cy, double r)
        {
            var result = new List<CorePoint2D>();
            var dx = x2 - x1;
            var dy = y2 - y1;
            var fx = x1 - cx;
            var fy = y1 - cy;

            var a = dx * dx + dy * dy;
            var b = 2.0 * (dx * fx + dy * fy);
            var c = fx * fx + fy * fy - r * r;

            if (Math.Abs(a) < 1e-12) return result; // 退化线段

            var discriminant = b * b - 4.0 * a * c;
            if (discriminant < 0) return result;

            var sqrtD = Math.Sqrt(discriminant);
            var t1 = (-b - sqrtD) / (2.0 * a);
            var t2 = (-b + sqrtD) / (2.0 * a);

            result.Add(new CorePoint2D(x1 + t1 * dx, y1 + t1 * dy));
            if (Math.Abs(discriminant) > 1e-12)
                result.Add(new CorePoint2D(x1 + t2 * dx, y1 + t2 * dy));

            return result;
        }

        /// <summary>
        ///     判断点是否在线段上（含端点）.
        /// </summary>
        private bool PointOnSegment(CorePoint2D pt, CorePoint2D a, CorePoint2D b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12) return false;

            var t = ((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / lenSq;
            return t >= -1e-10 && t <= 1.0 + 1e-10;
        }

        private void DeleteCircle(Circle circle, CropCircleResult result)
        {
            try
            {
                if (!circle.IsWriteEnabled) circle.UpgradeOpen();
                circle.Erase();
                result.DeletedCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"删除圆失败 (ID={circle.ObjectId}): {ex.Message}");
                result.SkippedCount++;
            }
        }
    }
}