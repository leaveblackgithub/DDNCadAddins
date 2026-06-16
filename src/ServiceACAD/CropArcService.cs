using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropArcResult = ServiceACAD.OpResult<ServiceACAD.CropArcResult>;

namespace ServiceACAD
{
    /// <summary>
    ///     圆弧裁剪结果.
    /// </summary>
    public class CropArcResult
    {
        /// <summary>
        ///     被删除的圆弧数量.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        ///     被拆分的圆弧数量.
        /// </summary>
        public int SplitCount { get; set; }

        /// <summary>
        ///     保留的圆弧数量（完全在目标侧无需处理）.
        /// </summary>
        public int KeptCount { get; set; }

        /// <summary>
        ///     跳过的圆弧数量（无效或错误）.
        /// </summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     圆弧裁剪服务 - 专门处理 Arc 类型的裁剪操作.
    ///     支持保留边界内部或外部的圆弧，自动将跨越边界的圆弧拆分为多段弧.
    /// </summary>
    public class CropArcService
    {
        private readonly ICropGeometryService _cropGeometry;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="cropGeometry">几何计算服务，为空时使用默认实现.</param>
        public CropArcService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        // ---- 公共接口 ----

        /// <summary>
        ///     裁剪圆弧：保留边界内部的圆弧段.
        /// </summary>
        public OpResultOfCropArcResult CropArcsInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> arcIds,
            ITransactionService transactionService)
        {
            return this.CropArcs(boundaryPoints, arcIds, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪圆弧：保留边界外部的圆弧段.
        /// </summary>
        public OpResultOfCropArcResult CropArcsOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> arcIds,
            ITransactionService transactionService)
        {
            return this.CropArcs(boundaryPoints, arcIds, transactionService, keepInside: false);
        }

        /// <summary>
        ///     裁剪所有圆弧：保留边界内部，自动选择图纸中所有 Arc 对象.
        /// </summary>
        public OpResultOfCropArcResult CropAllArcsInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllArcs(boundaryPoints, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪所有圆弧：保留边界外部，自动选择图纸中所有 Arc 对象.
        /// </summary>
        public OpResultOfCropArcResult CropAllArcsOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllArcs(boundaryPoints, transactionService, keepInside: false);
        }

        // ---- 私有实现 ----

        private OpResultOfCropArcResult CropAllArcs(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropArcResult.Fail("裁剪边界顶点不足（至少需要3个点）");

                if (transactionService == null)
                    return OpResultOfCropArcResult.Fail("事务服务引用为空");

                var allArcIds = transactionService.GetChildObjectsFromModelspace<Arc>();
                if (allArcIds == null || allArcIds.Count == 0)
                    return OpResultOfCropArcResult.Fail("图纸中没有找到任何圆弧");

                return this.CropArcs(boundaryPoints, allArcIds, transactionService, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllArcs 操作失败: {ex.Message}", ex);
                return OpResultOfCropArcResult.Fail($"自动裁剪圆弧失败: {ex.Message}");
            }
        }

        private OpResultOfCropArcResult CropArcs(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> arcIds,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropArcResult.Fail("裁剪边界顶点不足（至少需要3个点）");

                if (arcIds == null || arcIds.Count == 0)
                    return OpResultOfCropArcResult.Fail("待裁剪的圆弧列表为空");

                if (transactionService == null)
                    return OpResultOfCropArcResult.Fail("事务服务引用为空");

                var result = new CropArcResult();

                foreach (var arcId in arcIds)
                {
                    try
                    {
                        if (!arcId.IsValid || arcId.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        var entity = transactionService.GetObject<Entity>(arcId);
                        if (entity == null || entity.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        if (!(entity is Arc arc))
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        this.ProcessArc(arc, boundaryPoints, keepInside, transactionService, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理圆弧 {arcId} 时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                if (result.DeletedCount == 0 && result.SplitCount == 0 && result.KeptCount == 0)
                    return OpResultOfCropArcResult.Fail("没有圆弧被处理");

                return OpResultOfCropArcResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropArcs 操作失败: {ex.Message}", ex);
                return OpResultOfCropArcResult.Fail($"圆弧裁剪失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理单条圆弧的裁剪：先判断包围盒分类，需要拆分时采样求交再拆分.
        /// </summary>
        private void ProcessArc(
            Arc arc,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropArcResult result)
        {
            // 先判断圆弧整体与边界的关系
            var extents = arc.GeometricExtents;
            var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
            var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
            var containment = this._cropGeometry.ClassifyBoundingBox(minPt, maxPt, boundaryPoints);

            bool shouldDelete = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);

            if (shouldDelete)
            {
                this.DeleteArc(arc, result);
                return;
            }

            // 不交叉则直接保留
            if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects)
            {
                result.KeptCount++;
                return;
            }

            // 需要拆分：采样圆弧为多段线段求交点
            this.SplitArcAndKeep(arc, boundaryPoints, keepInside, transactionService, result);
        }

        /// <summary>
        ///     按交点拆分圆弧，逐弧段中点判断保留/删除，重组保留段.
        /// </summary>
        private void SplitArcAndKeep(
            Arc arc,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropArcResult result)
        {
            try
            {
                const int sampleCount = 64;
                var startAngle = arc.StartAngle;
                var endAngle = arc.EndAngle;
                var totalAngle = endAngle - startAngle;

                // 收集所有沿圆弧的交点参数（角度）
                var intersectionAngles = new List<double>();

                Point2d prevPt = new Point2d(
                    arc.Center.X + arc.Radius * Math.Cos(startAngle),
                    arc.Center.Y + arc.Radius * Math.Sin(startAngle));

                for (var i = 1; i <= sampleCount; i++)
                {
                    var t = (double)i / sampleCount;
                    var angle = startAngle + totalAngle * t;
                    var currPt = new Point2d(
                        arc.Center.X + arc.Radius * Math.Cos(angle),
                        arc.Center.Y + arc.Radius * Math.Sin(angle));

                    var segStart = new CorePoint2D(prevPt.X, prevPt.Y);
                    var segEnd = new CorePoint2D(currPt.X, currPt.Y);

                    var intersections = this._cropGeometry.FindLineSegmentIntersections(
                        segStart, segEnd, boundaryPoints);

                    foreach (var ix in intersections)
                    {
                        // 将交点位置转为角度参数
                        var dx = ix.X - arc.Center.X;
                        var dy = ix.Y - arc.Center.Y;
                        var intersectionAngle = Math.Atan2(dy, dx);
                        // 归一化到 [startAngle, endAngle] 范围
                        intersectionAngle = NormalizeAngleToRange(intersectionAngle, startAngle, endAngle);
                        intersectionAngles.Add(intersectionAngle);
                    }

                    prevPt = currPt;
                }

                // 排序交点
                intersectionAngles.Sort();

                // 构造节点序列：起点 + 交点角度 + 终点
                var angleNodes = new List<double> { startAngle };
                angleNodes.AddRange(intersectionAngles);
                angleNodes.Add(endAngle);

                // 逐弧段用中点判断保留/删除
                var segmentsToKeep = new List<Tuple<double, double>>();
                for (var i = 0; i < angleNodes.Count - 1; i++)
                {
                    var a = angleNodes[i];
                    var b = angleNodes[i + 1];

                    if (Math.Abs(b - a) < 1e-9)
                        continue;

                    var midAngle = (a + b) / 2.0;
                    var midX = arc.Center.X + arc.Radius * Math.Cos(midAngle);
                    var midY = arc.Center.Y + arc.Radius * Math.Sin(midAngle);
                    var midPt = new CorePoint2D(midX, midY);
                    var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);

                    if ((keepInside && isInside) || (!keepInside && !isInside))
                    {
                        segmentsToKeep.Add(Tuple.Create(a, b));
                    }
                }

                if (segmentsToKeep.Count == 0)
                {
                    this.DeleteArc(arc, result);
                    return;
                }

                // 如果没有交点（整条弧都在目标侧），保留原弧
                if (intersectionAngles.Count == 0 && segmentsToKeep.Count == 1)
                {
                    result.KeptCount++;
                    return;
                }

                // 创建新的弧段
                if (!arc.IsWriteEnabled)
                    arc.UpgradeOpen();

                arc.Erase();

                foreach (var seg in segmentsToKeep)
                {
                    var newArc = new Arc(
                        arc.Center,
                        arc.Normal,
                        arc.Radius,
                        seg.Item1,
                        seg.Item2);
                    newArc.Layer = arc.Layer;
                    newArc.Color = arc.Color;
                    newArc.Linetype = arc.Linetype;
                    newArc.LineWeight = arc.LineWeight;

                    transactionService.AppendEntityToCurrentSpace(newArc);
                }

                result.SplitCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"拆分圆弧失败 (ID={arc.ObjectId}): {ex.Message}");
                this.DeleteArc(arc, result);
            }
        }

        /// <summary>
        ///     将角度归一化到 [start, end] 范围内（start < end）.
        /// </summary>
        private static double NormalizeAngleToRange(double angle, double start, double end)
        {
            var range = end - start;
            // 将 angle 调整到与 start 相同的周期
            while (angle < start)
                angle += 2.0 * Math.PI;
            while (angle > start + 2.0 * Math.PI)
                angle -= 2.0 * Math.PI;
            // 确保在范围内
            if (angle > end)
                angle -= 2.0 * Math.PI;
            if (angle < start)
                angle += 2.0 * Math.PI;
            return Math.Max(start, Math.Min(end, angle));
        }

        /// <summary>
        ///     删除圆弧并更新统计.
        /// </summary>
        private void DeleteArc(Arc arc, CropArcResult result)
        {
            try
            {
                if (!arc.IsWriteEnabled)
                    arc.UpgradeOpen();

                arc.Erase();
                result.DeletedCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"删除圆弧失败 (ID={arc.ObjectId}): {ex.Message}");
                result.SkippedCount++;
            }
        }
    }
}