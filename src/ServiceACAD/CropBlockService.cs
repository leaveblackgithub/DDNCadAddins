using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     BlockReference 裁剪结果统计.
    /// </summary>
    public class CropBlockResult
    {
        /// <summary>被删除的块数量（包围盒完全在保留区域外侧）.</summary>
        public int DeletedCount { get; set; }

        /// <summary>保留的块数量（包围盒完全在保留区域内侧）.</summary>
        public int KeptCount { get; set; }

        /// <summary>跳过的块数量（无效/异常）.</summary>
        public int SkippedCount { get; set; }

        /// <summary>被 ExplodeAsShown 炸开的块数量（包围盒与边界相交）.</summary>
        public int ExplodedCount { get; set; }
    }

    /// <summary>
    ///     BlockReference 裁剪服务 — 包围盒分类 + ExplodeAsShown 炸开 → 裁剪子实体.
    ///     <para>流程：获取输入 → 包围盒分类 → Inside 保留 / Outside 删除 / Intersects 炸开+裁剪.</para>
    ///     <para>炸开后对非 BlockReference 子实体调用 ICropService.CropInside/CropOutside 精确裁剪，嵌套块保留原样.</para>
    /// </summary>
    public class CropBlockService
    {
        private readonly ICropGeometryService _geometry;
        private readonly ICropService _cropService;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="geometry">几何服务（用于包围盒分类）.</param>
        /// <param name="cropService">裁剪服务（用于裁剪爆炸后的非块参照子实体）.</param>
        public CropBlockService(ICropGeometryService geometry, ICropService cropService)
        {
            this._geometry = geometry ?? new CropGeometryService();
            this._cropService = cropService ?? throw new ArgumentNullException(nameof(cropService));
        }

        /// <summary>
        ///     裁剪块参照 — 包围盒分类 + ExplodeAsShown 炸开 + 子实体裁剪.
        /// </summary>
        /// <param name="boundary">精确裁剪边界（ICropBoundary）.</param>
        /// <param name="polygon">边界的近似多边形顶点（用于包围盒分类）.</param>
        /// <param name="blockRefIds">块参照 ObjectId 列表.</param>
        /// <param name="keepInside">true=保留内部，false=保留外部.</param>
        /// <param name="ts">事务服务.</param>
        /// <returns>裁剪结果（CropBlockResult）.</returns>
        public OpResult<CropBlockResult> CropBlocks(
            ICropBoundary boundary,
            IReadOnlyList<CorePoint2D> polygon,
            List<ObjectId> blockRefIds,
            bool keepInside,
            ITransactionService ts)
        {
            try
            {
                if (blockRefIds == null || blockRefIds.Count == 0)
                    return OpResult<CropBlockResult>.Fail("块参照列表为空");

                if (boundary == null)
                    return OpResult<CropBlockResult>.Fail("裁剪边界为空");

                var result = new CropBlockResult();

                foreach (var id in blockRefIds)
                {
                    try
                    {
                        if (!id.IsValid || id.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        var blockRef = ts.GetObject<BlockReference>(id);
                        if (blockRef == null || blockRef.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        // ── 包围盒分类 ──
                        var containment = this.ClassifyBlockBoundingBox(blockRef, boundary);

                        bool shouldDelete = keepInside
                            ? containment == ContainmentResult.Outside
                            : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);

                        if (shouldDelete)
                        {
                            // 完全在保留区域外侧 → 删除
                            if (!blockRef.IsWriteEnabled)
                                blockRef.UpgradeOpen();
                            blockRef.Erase();
                            result.DeletedCount++;
                            continue;
                        }

                        if (containment == ContainmentResult.Intersects)
                        {
                            // 与边界相交 → 爆炸 + 裁剪子实体（嵌套块保留原样）
                            var explodeCropResult = this.ExplodeAndCropChildren(
                                blockRef, boundary, keepInside, ts);

                            if (!explodeCropResult.IsSuccess)
                            {
                                Logger._.Warn($"爆炸裁剪块参照失败，跳过: {explodeCropResult.Message}");
                                result.SkippedCount++;
                                continue;
                            }

                            result.ExplodedCount++;
                            continue;
                        }

                        // Inside + keepInside（或 Outside + !keepInside）→ 保留
                        result.KeptCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"处理块参照 {id} 时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                return OpResult<CropBlockResult>.Success(result);
            }
            catch (Exception ex)
            {
                Logger._.Error($"CropBlock 操作失败: {ex.Message}", ex);
                return OpResult<CropBlockResult>.Fail($"裁剪图块失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     爆炸块参照并对非 BlockReference 子实体执行裁剪（嵌套块保留原样）.
        /// </summary>
        /// <param name="blockRef">块参照.</param>
        /// <param name="boundary">裁剪边界.</param>
        /// <param name="keepInside">裁剪方向.</param>
        /// <param name="ts">事务服务.</param>
        /// <returns>操作结果.</returns>
        private OpResult<object> ExplodeAndCropChildren(
            BlockReference blockRef,
            ICropBoundary boundary,
            bool keepInside,
            ITransactionService ts)
        {
            try
            {
                // ── 1. 爆炸 ──
                var exploder = new BlockExploder(ts);
                var explodeResult = exploder.Explode(blockRef);
                if (!explodeResult.IsSuccess)
                    return OpResult<object>.Fail(explodeResult.Message);

                var childIds = explodeResult.Data.EntityIds;
                if (childIds == null || childIds.Count == 0)
                    return OpResult<object>.Fail("爆炸后未生成任何实体");

                // ── 2. 分离 BlockReference（嵌套块保留原样）和其他实体 ──
                var nonBlockIds = new List<ObjectId>();
                foreach (var childId in childIds)
                {
                    if (!childId.IsValid || childId.IsErased)
                        continue;

                    // 跳过嵌套块参照
                    if (childId.ObjectClass != null &&
                        childId.ObjectClass.Name == "AcDbBlockReference")
                    {
                        continue;
                    }

                    nonBlockIds.Add(childId);
                }

                // ── 3. 裁剪非块参照子实体 ──
                if (nonBlockIds.Count > 0)
                {
                    var input = new CropInput
                    {
                        Boundary = boundary,
                        EntityIds = nonBlockIds,
                        TransactionService = ts,
                    };

                    var cropResult = keepInside
                        ? this._cropService.CropInside(input)
                        : this._cropService.CropOutside(input);

                    if (!cropResult.IsSuccess)
                    {
                        Logger._.Warn($"裁剪爆炸后子实体时部分失败: {cropResult.Message}");
                    }
                }

                return OpResult<object>.Success(null);
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸裁剪子实体失败: {ex.Message}", ex);
                return OpResult<object>.Fail($"爆炸裁剪子实体失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     使用 ICropBoundary 对块参照进行包围盒分类.
        /// </summary>
        /// <param name="blockRef">块参照.</param>
        /// <param name="boundary">裁剪边界.</param>
        /// <returns>分类结果.</returns>
        private ContainmentResult ClassifyBlockBoundingBox(
            BlockReference blockRef, ICropBoundary boundary)
        {
            try
            {
                var extents = blockRef.GeometricExtents;
                if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9)
                    return ContainmentResult.Inside;

                var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
                var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
                return boundary.ClassifyBoundingBox(minPt, maxPt);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"块参照包围盒分类失败: {ex.Message}");
                return ContainmentResult.Intersects; // 安全起见，无法分类时走爆炸路径
            }
        }
    }
}
