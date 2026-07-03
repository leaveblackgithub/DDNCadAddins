using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
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
    ///     圆裁剪服务 — 求圆与边界的精确交点，按交点拆分为 Arc，逐弧段中点判断保留/删除.
    ///     <para>支持精确边界（ICropBoundary）和折线边界（IReadOnlyList<CorePoint2D>）两种模式.</para>
    /// </summary>
    public class CropCircleService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropCircleService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        // ──────────────────────────────────────────────────────────────
        //  新签名：精确边界（ICropBoundary）— 主方法
        // ──────────────────────────────────────────────────────────────

        public OpResultOfCropCircleResult CropCirclesInside(
            ICropBoundary boundary, List<ObjectId> circleIds, ITransactionService ts)
            => this.CropCircles(boundary, circleIds, ts, keepInside: true);

        public OpResultOfCropCircleResult CropCirclesOutside(
            ICropBoundary boundary, List<ObjectId> circleIds, ITransactionService ts)
            => this.CropCircles(boundary, circleIds, ts, keepInside: false);

        // ──────────────────────────────────────────────────────────────
        //  旧签名：折线边界（IReadOnlyList<CorePoint2D>）— 兼容包装
        // ──────────────────────────────────────────────────────────────

        public OpResultOfCropCircleResult CropCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> circleIds, ITransactionService ts)
            => this.CropCirclesInside(new PolygonCropBoundary(boundaryPoints), circleIds, ts);

        public OpResultOfCropCircleResult CropCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> circleIds, ITransactionService ts)
            => this.CropCirclesOutside(new PolygonCropBoundary(boundaryPoints), circleIds, ts);

        public OpResultOfCropCircleResult CropAllCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService ts)
            => this.CropAllCircles(boundaryPoints, ts, keepInside: true);

        public OpResultOfCropCircleResult CropAllCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService ts)
            => this.CropAllCircles(boundaryPoints, ts, keepInside: false);

        // ──────────────────────────────────────────────────────────────
        //  私有核心逻辑
        // ──────────────────────────────────────────────────────────────

        private OpResultOfCropCircleResult CropAllCircles(
            IReadOnlyList<CorePoint2D> boundaryPoints, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropCircleResult.Fail("裁剪边界顶点不足");
                if (ts == null)
                    return OpResultOfCropCircleResult.Fail("事务服务引用为空");

                var allIds = ts.GetChildObjectsFromModelspace<Circle>();
                if (allIds == null || allIds.Count == 0)
                    return OpResultOfCropCircleResult.Fail("图纸中没有找到任何圆");

                return this.CropCirclesInside(new PolygonCropBoundary(boundaryPoints), allIds, ts);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllCircles 操作失败: {ex.Message}", ex);
                return OpResultOfCropCircleResult.Fail($"自动裁剪圆失败: {ex.Message}");
            }
        }

        private OpResultOfCropCircleResult CropCircles(
            ICropBoundary boundary, List<ObjectId> circleIds, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (boundary == null)
                    return OpResultOfCropCircleResult.Fail("裁剪边界为空");
                if (circleIds == null || circleIds.Count == 0)
                    return OpResultOfCropCircleResult.Fail("待裁剪的圆列表为空");
                if (ts == null)
                    return OpResultOfCropCircleResult.Fail("事务服务引用为空");

                var result = new CropCircleResult();
                foreach (var circleId in circleIds)
                {
                    try
                    {
                        if (!circleId.IsValid || circleId.IsErased) { result.SkippedCount++; continue; }
                        var entity = ts.GetObject<Entity>(circleId);
                        if (entity == null || entity.IsErased) { result.SkippedCount++; continue; }
                        if (!(entity is Circle circle)) { result.SkippedCount++; continue; }
                        this.ProcessCircle(circle, boundary, keepInside, ts, result);
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
            Circle circle, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropCircleResult result)
        {
            var extents = circle.GeometricExtents;
            if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9) return;

            // ★ 用 ICropBoundary.ClassifyBoundingBox() 替代 CropGeometryService.ClassifyBoundingBox()
            var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
            var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
            var containment = boundary.ClassifyBoundingBox(minPt, maxPt);

            bool shouldDelete = keepInside
                ? containment == ContainmentResult.Outside
                : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);

            if (shouldDelete) { this.DeleteCircle(circle, result); return; }
            if (containment != ContainmentResult.Intersects) { result.KeptCount++; return; }

            this.SplitCircleAndKeep(circle, boundary, keepInside, ts, result);
        }

        /// <summary>
        ///     圆与边界求精确交点 — 弧段采样为弦段序列，对每条弦调用 boundary.FindLineIntersections().
        ///     当边界为 Circle/Ellipse 时使用解析解.
        /// </summary>
        private void SplitCircleAndKeep(
            Circle circle, ICropBoundary boundary, bool keepInside,
            ITransactionService ts, CropCircleResult result)
        {
            try
            {
                var centerX = circle.Center.X;
                var centerY = circle.Center.Y;
                var radius = circle.Radius;
                var normal = circle.Normal;

                // ★ 圆采样为弦段序列，对每条弦调用 boundary.FindLineIntersections()
                const int circleSamples = 64;
                var allIx = new List<CorePoint2D>();
                for (int i = 0; i < circleSamples; i++)
                {
                    double a1 = 2.0 * Math.PI * i / circleSamples;
                    double a2 = 2.0 * Math.PI * (i + 1) / circleSamples;
                    var p1 = new CorePoint2D(centerX + radius * Math.Cos(a1), centerY + radius * Math.Sin(a1));
                    var p2 = new CorePoint2D(centerX + radius * Math.Cos(a2), centerY + radius * Math.Sin(a2));

                    var intersections = boundary.FindLineIntersections(p1, p2);
                    allIx.AddRange(intersections);
                }

                // 脱重（距离 < 1e-6 的点合并为一个）
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

                // 交点转为角度 [0, 2π)
                var intersectionAngles = new List<double>();
                foreach (var pt in deduped)
                {
                    var angle = Math.Atan2(pt.Y - centerY, pt.X - centerX);
                    if (angle < 0) angle += 2.0 * Math.PI;
                    intersectionAngles.Add(angle);
                }
                intersectionAngles.Sort();

                // 构造节点序列: 0 → 交点 → 2π
                var angleNodes = new List<double> { 0.0 };
                angleNodes.AddRange(intersectionAngles);
                angleNodes.Add(2.0 * Math.PI);

                // 逐弧段中点判断保留/删除
                var arcsToKeep = new List<Tuple<double, double>>();
                for (var i = 0; i < angleNodes.Count - 1; i++)
                {
                    var a = angleNodes[i];
                    var b = angleNodes[i + 1];
                    if (Math.Abs(b - a) < 1e-9) continue;

                    var midAngle = (a + b) / 2.0;
                    var midX = centerX + radius * Math.Cos(midAngle);
                    var midY = centerY + radius * Math.Sin(midAngle);
                    // ★ 用 ICropBoundary.IsPointInside() 替代 CropGeometryService.IsPointInPolygon()
                    var inside = boundary.IsPointInside(new CorePoint2D(midX, midY));

                    if ((keepInside && inside) || (!keepInside && !inside))
                        arcsToKeep.Add(Tuple.Create(a, b));
                }

                if (arcsToKeep.Count == 0) { this.DeleteCircle(circle, result); return; }
                if (intersectionAngles.Count == 0 && arcsToKeep.Count == 1) { result.KeptCount++; return; }

                if (!circle.IsWriteEnabled) circle.UpgradeOpen();
                circle.Erase();

                foreach (var seg in arcsToKeep)
                {
                    var startA = seg.Item1;
                    var endA = seg.Item2;
                    if (Math.Abs(endA - startA) < 1e-9) continue;

                    var newArc = new Arc(circle.Center, normal, radius, startA, endA);
                    newArc.Layer = circle.Layer;
                    newArc.Color = circle.Color;
                    newArc.Linetype = circle.Linetype;
                    newArc.LineWeight = circle.LineWeight;
                    ts.AppendEntityToCurrentSpace(newArc);
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
