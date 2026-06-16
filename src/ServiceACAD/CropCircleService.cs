using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
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
        ///     被删除的圆数量.
        /// </summary>
        public int DeletedCount { get; set; }

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
    ///     圆是闭合曲线，无法拆分，因此只做保留/删除判断.
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

        /// <summary>
        ///     裁剪圆：保留边界内部的圆.
        /// </summary>
        public OpResultOfCropCircleResult CropCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> circleIds,
            ITransactionService transactionService)
        {
            return this.CropCircles(boundaryPoints, circleIds, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪圆：保留边界外部的圆.
        /// </summary>
        public OpResultOfCropCircleResult CropCirclesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> circleIds,
            ITransactionService transactionService)
        {
            return this.CropCircles(boundaryPoints, circleIds, transactionService, keepInside: false);
        }

        /// <summary>
        ///     裁剪所有圆：保留边界内部.
        /// </summary>
        public OpResultOfCropCircleResult CropAllCirclesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllCircles(boundaryPoints, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪所有圆：保留边界外部.
        /// </summary>
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

                        this.ProcessCircle(circle, boundaryPoints, keepInside, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理圆 {circleId} 时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                if (result.DeletedCount == 0 && result.KeptCount == 0)
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
        ///     处理单个圆：通过圆心点判断保留或删除.
        /// </summary>
        private void ProcessCircle(
            Circle circle,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            CropCircleResult result)
        {
            var centerPt = new CorePoint2D(circle.Center.X, circle.Center.Y);
            var isInside = this._cropGeometry.IsPointInPolygon(centerPt, boundaryPoints);

            bool shouldKeep = (keepInside && isInside) || (!keepInside && !isInside);

            if (shouldKeep)
            {
                result.KeptCount++;
            }
            else
            {
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