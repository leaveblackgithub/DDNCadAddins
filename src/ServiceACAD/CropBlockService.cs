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
        /// <summary>被删除的块数量.</summary>
        public int DeletedCount { get; set; }

        /// <summary>保留的块数量.</summary>
        public int KeptCount { get; set; }

        /// <summary>跳过的块数量.</summary>
        public int SkippedCount { get; set; }

        /// <summary>被爆炸的块数量.</summary>
        public int ExplodedCount { get; set; }

        /// <summary>嵌套块因 Intersects 跳过裁剪的数量.</summary>
        public int NestedBlockSkippedCount { get; set; }

        /// <summary>嵌套块因 Inside 保留的数量.</summary>
        public int NestedBlockKeptCount { get; set; }

        /// <summary>嵌套块因 Outside 删除的数量.</summary>
        public int NestedBlockDeletedCount { get; set; }
    }

    /// <summary>
    ///     BlockReference 裁剪服务 — 包围盒分类 + 爆炸裁剪（仅最外层）.
    ///     <para>嵌套块采用包围盒粗筛：Inside→保留 / Outside→删除 / Intersects→跳过.</para>
    /// </summary>
    public class CropBlockService
    {
        private readonly ICropGeometryService _geometry;
        private readonly ICropService _cropService;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="geometry">几何服务.</param>
        /// <param name="cropService">裁剪服务（用于裁剪爆炸后的子实体）.</param>
        public CropBlockService(ICropGeometryService geometry, ICropService cropService)
        {
            this._geometry = geometry ?? new CropGeometryService();
            this._cropService = cropService ?? throw new ArgumentNullException(nameof(cropService));
        }

        // ── 公开入口 ──

        /// <summary>
        ///     裁剪块参照（保留边界内部）— 使用多边形边界.
        ///     <para>向后兼容：无 ICropBoundary 时退化为包围盒粗筛（不爆炸）.</para>
        /// </summary>
        public OpResult<CropBlockResult> CropBlocksInside(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts)
            => this.CropBoundingBoxOnly(bp, ids, true, ts);

        /// <summary>
        ///     裁剪块参照（保留边界外部）— 使用多边形边界.
        ///     <para>向后兼容：无 ICropBoundary 时退化为包围盒粗筛（不爆炸）.</para>
        /// </summary>
        public OpResult<CropBlockResult> CropBlocksOutside(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts)
            => this.CropBoundingBoxOnly(bp, ids, false, ts);

        /// <summary>
        ///     裁剪块参照（保留边界内部）— 使用精确边界，支持爆炸裁剪.
        /// </summary>
        /// <param name="boundary">精确裁剪边界.</param>
        /// <param name="polygon">边界的近似多边形顶点（用于嵌套块包围盒分类）.</param>
        /// <param name="ids">块参照 ObjectId 列表.</param>
        /// <param name="ts">事务服务.</param>
        /// <returns>裁剪结果.</returns>
        public OpResult<CropBlockResult> CropBlocksInside(
            ICropBoundary boundary,
            IReadOnlyList<CorePoint2D> polygon,
            List<ObjectId> ids,
            ITransactionService ts)
            => this.CropCore(boundary, polygon, ids, true, ts);

        /// <summary>
        ///     裁剪块参照（保留边界外部）— 使用精确边界，支持爆炸裁剪.
        /// </summary>
        /// <param name="boundary">精确裁剪边界.</param>
        /// <param name="polygon">边界的近似多边形顶点（用于嵌套块包围盒分类）.</param>
        /// <param name="ids">块参照 ObjectId 列表.</param>
        /// <param name="ts">事务服务.</param>
        /// <returns>裁剪结果.</returns>
        public OpResult<CropBlockResult> CropBlocksOutside(
            ICropBoundary boundary,
            IReadOnlyList<CorePoint2D> polygon,
            List<ObjectId> ids,
            ITransactionService ts)
            => this.CropCore(boundary, polygon, ids, false, ts);

        // ── 核心逻辑 ──

        /// <summary>
        ///     核心方法：包围盒分类 → Intersects 时爆炸裁剪 / Inside 保留 / Outside 删除.
        /// </summary>
        private OpResult<CropBlockResult> CropCore(
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
                            // 删除
                            if (!blockRef.IsWriteEnabled)
                                blockRef.UpgradeOpen();
                            blockRef.Erase();
                            result.DeletedCount++;
                            continue;
                        }

                        if (containment == ContainmentResult.Intersects)
                        {
                            // 爆炸 → 裁剪子实体
                            var explodeCropResult = this.ExplodeAndCrop(
                                blockRef, boundary, polygon, keepInside, ts);

                            if (!explodeCropResult.IsSuccess)
                            {
                                Logger._.Warn($"爆炸裁剪块参照失败，跳过: {explodeCropResult.Message}");
                                result.SkippedCount++;
                                continue;
                            }

                            result.ExplodedCount++;
                            result.DeletedCount++; // 原块参照已被 Explode() 擦除
                            result.NestedBlockKeptCount += explodeCropResult.Data.NestedKept;
                            result.NestedBlockDeletedCount += explodeCropResult.Data.NestedDeleted;
                            result.NestedBlockSkippedCount += explodeCropResult.Data.NestedSkipped;
                            continue;
                        }

                        // Inside 且 keepInside（或 Outside 且 !keepInside）→ 保留
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
        ///     爆炸块参照并对子实体执行裁剪.
        /// </summary>
        private OpResult<NestedBlockStats> ExplodeAndCrop(
            BlockReference blockRef,
            ICropBoundary boundary,
            IReadOnlyList<CorePoint2D> polygon,
            bool keepInside,
            ITransactionService ts)
        {
            try
            {
                // ── 1. 爆炸 ──
                var exploder = new BlockExploder(ts);
                var explodeResult = exploder.Explode(blockRef);
                if (!explodeResult.IsSuccess)
                    return OpResult<NestedBlockStats>.Fail(explodeResult.Message);

                var childIds = explodeResult.Data.EntityIds;
                if (childIds == null || childIds.Count == 0)
                    return OpResult<NestedBlockStats>.Fail("爆炸后未生成任何实体");

                // ── 2. 分离嵌套块与其他实体 ──
                var nestedBlockIds = new List<ObjectId>();
                var otherIds = new List<ObjectId>();

                foreach (var childId in childIds)
                {
                    if (!childId.IsValid || childId.IsErased)
                        continue;

                    // 检查实体类型（不在事务中打开，仅通过 ObjectId 的 ObjectClass 判断）
                    if (childId.ObjectClass != null &&
                        childId.ObjectClass.Name == "AcDbBlockReference")
                    {
                        nestedBlockIds.Add(childId);
                    }
                    else
                    {
                        otherIds.Add(childId);
                    }
                }

                // ── 3. 裁剪非嵌套实体 via ICropService ──
                if (otherIds.Count > 0)
                {
                    var input = new CropInput
                    {
                        Boundary = boundary,
                        EntityIds = otherIds,
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

                // ── 4. 嵌套块包围盒粗筛 ──
                var nestedStats = this.ClassifyNestedBlocks(polygon, nestedBlockIds, keepInside, ts);

                return OpResult<NestedBlockStats>.Success(nestedStats);
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸裁剪块参照失败: {ex.Message}", ex);
                return OpResult<NestedBlockStats>.Fail($"爆炸裁剪失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     嵌套块包围盒粗筛统计.
        /// </summary>
        private class NestedBlockStats
        {
            public int NestedKept { get; set; }
            public int NestedDeleted { get; set; }
            public int NestedSkipped { get; set; }
        }

        /// <summary>
        ///     对嵌套块执行包围盒粗筛（不递归爆炸/裁剪）.
        /// </summary>
        private NestedBlockStats ClassifyNestedBlocks(
            IReadOnlyList<CorePoint2D> polygon,
            List<ObjectId> nestedBlockIds,
            bool keepInside,
            ITransactionService ts)
        {
            var stats = new NestedBlockStats();

            foreach (var id in nestedBlockIds)
            {
                try
                {
                    if (!id.IsValid || id.IsErased)
                    {
                        stats.NestedSkipped++;
                        continue;
                    }

                    var nestedBlock = ts.GetObject<BlockReference>(id);
                    if (nestedBlock == null || nestedBlock.IsErased)
                    {
                        stats.NestedSkipped++;
                        continue;
                    }

                    // 包围盒分类
                    var extents = nestedBlock.GeometricExtents;
                    if (extents.MinPoint.DistanceTo(extents.MaxPoint) < 1e-9)
                    {
                        stats.NestedKept++;
                        continue;
                    }

                    var minPt = new CorePoint2D(extents.MinPoint.X, extents.MinPoint.Y);
                    var maxPt = new CorePoint2D(extents.MaxPoint.X, extents.MaxPoint.Y);
                    var containment = this._geometry.ClassifyBoundingBox(minPt, maxPt, polygon);

                    bool shouldDelete = keepInside
                        ? containment == ContainmentResult.Outside
                        : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);

                    if (shouldDelete)
                    {
                        if (!nestedBlock.IsWriteEnabled)
                            nestedBlock.UpgradeOpen();
                        nestedBlock.Erase();
                        stats.NestedDeleted++;
                    }
                    else if (containment == ContainmentResult.Intersects)
                    {
                        // Intersects → 跳过（保留原样）
                        stats.NestedSkipped++;
                    }
                    else
                    {
                        // Inside + keepInside 或 Outside + !keepInside → 保留
                        stats.NestedKept++;
                    }
                }
                catch (Exception ex)
                {
                    Logger._.Warn($"处理嵌套块时发生异常: {ex.Message}");
                    stats.NestedSkipped++;
                }
            }

            return stats;
        }

        /// <summary>
        ///     纯包围盒粗筛（向后兼容，不爆炸）.
        /// </summary>
        private OpResult<CropBlockResult> CropBoundingBoxOnly(
            IReadOnlyList<CorePoint2D> polygon,
            List<ObjectId> blockRefIds,
            bool keepInside,
            ITransactionService ts)
        {
            try
            {
                if (blockRefIds == null || blockRefIds.Count == 0)
                    return OpResult<CropBlockResult>.Fail("块参照列表为空");

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

                        var cr = CropUtils.ProcessNonCurve(blockRef, polygon, keepInside, this._geometry);
                        result.DeletedCount += cr.DeletedCount;
                        result.KeptCount += cr.KeptCount;
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"包围盒分类块参照时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                return OpResult<CropBlockResult>.Success(result);
            }
            catch (Exception ex)
            {
                Logger._.Error($"CropBlock(包围盒) 操作失败: {ex.Message}", ex);
                return OpResult<CropBlockResult>.Fail($"裁剪图块失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     使用 ICropBoundary 对块参照进行包围盒分类.
        /// </summary>
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
