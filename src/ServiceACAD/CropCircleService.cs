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
    /// <summary>
    ///     圆裁剪结果.
    /// </summary>
    public class CropCircleResult
    {
        /// <summary>
        ///     被删除的圆数量（含拆分后的原圆）.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        ///     被拆分的圆数量.
        /// </summary>
        public int SplitCount { get; set; }

        /// <summary>
        ///     保留的圆数量（完全在目标侧无需处理）.
        /// </summary>
        public int KeptCount { get; set; }

        /// <summary>
        ///     跳过的圆数量（无效或错误）.
        /// </summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     圆裁剪服务 - 处理 Circle 类型的裁剪操作.
    ///     采用包围盒分类 + 采样线段拆分，与 CropService 对通用曲线的处理方式一致.
    /// </summary>
    public class CropCircleService
    {
        private readonly ICropGeometryService _cropGeometry;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="cropGeometry">几何计算服务，为空时使用默认实现.</param>
        public CropCircleService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        public OpResultOfCropCircleResult CropCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> circleIds,
            ITransactionService transactionService)
        {
            return this.CropCircles(boundaryPoints, circleIds, transactionService, keepInside: true);
        }

        public OpResultOfCropCircleResult CropCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> circleIds,
            ITransactionService transactionService)
        {
            return this.CropCircles(boundaryPoints, circleIds, transactionService, keepInside: false);
        }

        public OpResultOfCropCircleResult CropAllCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllCircles(boundaryPoints, transactionService, keepInside: true);
        }

        public OpResultOfCropCircleResult CropAllCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllCircles(boundaryPoints, transactionService, keepInside: false);
        }

        private OpResultOfCropCircleResult CropAllCircles(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropCircleResult.Fail("裁剪边界顶点不足（至少需要3个点）");
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
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> circleIds,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return OpResultOfCropCircleResult.Fail("裁剪边界顶点不足（至少需要3个点）");
                if (circleIds == null || circleIds.Count == 0)
                    return OpResultOfCropCircleResult.Fail("待裁剪的圆列表为空");
                if (transactionService == null)
                    return OpResultOfCropCircleResult.Fail("事务服务引用为空");

                var result = new CropCircleResult();

                foreach (var circleId in circleIds)
                {
                    try
                    {
                        if (!circleId.IsValid || circleId.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        var entity = transactionService.GetObject<Entity>(circleId);
                        if (entity == null || entity.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        if (!(entity is Circle circle))
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        this.ProcessCircle(circle, boundaryPoints, keepInside, transactionService, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理圆 {circleId} 时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                var total = result.DeletedCount + result.SplitCount + result.KeptCount;
                if (total == 0)
                    return OpResultOfCropCircleResult.Fail("没有圆被处理");

                return OpResultOfCropCircleResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropCircles 操作失败: {ex.Message}", ex);
                return OpResultOfCropCircleResult.Fail($"圆裁剪失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理单个圆：包围盒分类 → 保留/删除/拆分.
        ///     与 CropService.CropEntity 的通用曲线处理方式一致.
        /// </summary>
        private void ProcessCircle(
            Circle circle,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropCircleResult result)
        {
            var extents = circle.GeometricExtents;
            if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9)
            {
                result.SkippedCount++;
                return;
            }

            var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
            var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
            var containment = this._cropGeometry.ClassifyBoundingBox(minPt, maxPt, boundaryPoints);

            bool shouldDelete = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);

            if (shouldDelete)
            {
                this.DeleteCircle(circle, result);
                return;
            }

            if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects)
            {
                result.KeptCount++;
                return;
            }

            // 相交：采样为线段 → 逐段判断 → 保留目标侧段
            this.SplitCircleAndKeep(circle, boundaryPoints, keepInside, transactionService, result);
        }

        /// <summary>
        ///     将圆采样为 64 段线段，逐段用中点判断保留/删除，保留段加入模型空间.
        /// </summary>
        private void SplitCircleAndKeep(
            Circle circle,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropCircleResult result)
        {
            try
            {
                const int sampleCount = 64;
                var center = circle.Center;
                var radius = circle.Radius;

                var segmentsToKeep = new List<Line>();

                for (var i = 0; i < sampleCount; i++)
                {
                    var angle1 = 2.0 * Math.PI * i / sampleCount;
                    var angle2 = 2.0 * Math.PI * (i + 1) / sampleCount;

                    var x1 = center.X + radius * Math.Cos(angle1);
                    var y1 = center.Y + radius * Math.Sin(angle1);
                    var x2 = center.X + radius * Math.Cos(angle2);
                    var y2 = center.Y + radius * Math.Sin(angle2);

                    var midPt = new CorePoint2D((x1 + x2) / 2.0, (y1 + y2) / 2.0);
                    var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);

                    if ((keepInside && isInside) || (!keepInside && !isInside))
                    {
                        var seg = new Line(
                            new Point3d(x1, y1, center.Z),
                            new Point3d(x2, y2, center.Z))
                        {
                            Layer = circle.Layer,
                            Color = circle.Color,
                            Linetype = circle.Linetype,
                            LineWeight = circle.LineWeight,
                        };
                        segmentsToKeep.Add(seg);
                    }
                }

                if (segmentsToKeep.Count == 0)
                {
                    this.DeleteCircle(circle, result);
                    return;
                }

                // 如果所有段都保留（圆完全在目标侧，但包围盒分类不准），保守保留原圆
                if (segmentsToKeep.Count == sampleCount)
                {
                    foreach (var seg in segmentsToKeep)
                        seg.Dispose();
                    result.KeptCount++;
                    return;
                }

                // 删除原圆，加入保留段
                if (!circle.IsWriteEnabled)
                    circle.UpgradeOpen();

                circle.Erase();

                foreach (var seg in segmentsToKeep)
                {
                    transactionService.AppendEntityToCurrentSpace(seg);
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
                if (!circle.IsWriteEnabled)
                    circle.UpgradeOpen();
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