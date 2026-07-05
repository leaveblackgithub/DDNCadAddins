using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Services;

namespace ServiceACAD
{
    /// <summary>
    ///     批量爆炸结果，包含统计信息和每个块的爆炸详情.
    /// </summary>
    public class ExplodeMultipleResult
    {
        /// <summary>成功爆炸的块数量.</summary>
        public int SuccessCount { get; set; }

        /// <summary>爆炸生成的实体总数.</summary>
        public int TotalExploded { get; set; }

        /// <summary>每个块的爆炸详情（块名 + 统计）.</summary>
        public List<ExplodeDetail> Details { get; set; } = new List<ExplodeDetail>();

        /// <summary>失败的块列表.</summary>
        public List<string> FailedBlocks { get; set; } = new List<string>();
    }

    /// <summary>
    ///     单个块爆炸详情.
    /// </summary>
    public class ExplodeDetail
    {
        /// <summary>块名称.</summary>
        public string BlockName { get; set; }

        /// <summary>爆炸结果统计.</summary>
        public ExplodeAsShownResult Stats { get; set; }
    }

    /// <summary>
    ///     图块爆炸器 — 将块参照爆炸为基本实体，将属性引用转换为文本。
    ///     从 BlockService 提取，独立负责爆炸及后处理职责（SRP）。
    /// </summary>
    public class BlockExploder
    {
        private readonly ITransactionService _serviceTrans;

        public BlockExploder(ITransactionService serviceTrans)
        {
            this._serviceTrans = serviceTrans ?? throw new ArgumentNullException(nameof(serviceTrans));
        }

        /// <summary>
        ///     批量爆炸多个块参照 — 静态方法，供命令层直接调用.
        /// </summary>
        /// <param name="blockRefIds">块参照 ObjectId 列表.</param>
        /// <param name="ts">事务服务.</param>
        /// <param name="cancellation">取消检测.</param>
        /// <returns>批量爆炸结果.</returns>
        public static OpResult<ExplodeMultipleResult> ExplodeMultiple(
            List<ObjectId> blockRefIds,
            ITransactionService ts,
            ICommandCancellation cancellation)
        {
            try
            {
                if (blockRefIds == null || blockRefIds.Count == 0)
                    return OpResult<ExplodeMultipleResult>.Fail("未选择图块");

                var result = new ExplodeMultipleResult();

                foreach (var blockRefId in blockRefIds)
                {
                    if (cancellation != null && cancellation.IsCancellationRequested)
                        break;

                    if (!blockRefId.IsValid || blockRefId.IsErased)
                    {
                        result.FailedBlocks.Add($"无效图块: {blockRefId}");
                        continue;
                    }

                    var blockService = ts.Block.GetBlockService(blockRefId);
                    if (blockService == null)
                    {
                        result.FailedBlocks.Add($"无法获取图块服务: {blockRefId}");
                        continue;
                    }

                    var blockName = blockService.Name;
                    var explodeResult = blockService.ExplodeAsShown();
                    if (!explodeResult.IsSuccess)
                    {
                        result.FailedBlocks.Add($"爆炸图块 {blockName} 失败: {explodeResult.Message}");
                        continue;
                    }

                    result.SuccessCount++;
                    result.TotalExploded += explodeResult.Data?.EntityIds?.Count ?? 0;
                    result.Details.Add(new ExplodeDetail
                    {
                        BlockName = blockName,
                        Stats = explodeResult.Data,
                    });
                }

                return OpResult<ExplodeMultipleResult>.Success(result);
            }
            catch (Exception ex)
            {
                Logger._.Error($"批量爆炸块参照失败: {ex.Message}");
                return OpResult<ExplodeMultipleResult>.Fail($"批量爆炸块参照失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     爆炸块参照并将其属性转换为文本。
        /// </summary>
        /// <param name="blkRef">块参照</param>
        /// <returns>爆炸结果及统计信息</returns>
        public OpResult<ExplodeAsShownResult> Explode(BlockReference blkRef)
        {
            if (blkRef == null)
                return OpResult<ExplodeAsShownResult>.Fail("CadBlkRef is null");

            try
            {
                if (!blkRef.IsWriteEnabled)
                    blkRef.UpgradeOpen();

                var stats = new ExplodeAsShownResult();
                var entitiesToAdd = new List<Entity>();

                // 处理属性引用 → 文本
                var textList = ProcessAttributeReferences(blkRef);
                foreach (var text in textList)
                {
                    if (text == null) continue;
                    stats.AttributeTextCount++;
                    var adjustStats = SetChildPropsAsBlk(text, blkRef);
                    if (adjustStats.LayerAdjusted) stats.LayerAdjustedCount++;
                    if (adjustStats.ColorAdjusted) stats.ColorAdjustedCount++;
                    entitiesToAdd.Add(text);
                }

                // 执行爆炸
                var explodedResult = ProcessExplodedEntities(blkRef);
                if (!explodedResult.IsSuccess)
                    return OpResult<ExplodeAsShownResult>.Fail(explodedResult.Message);

                stats.LayerAdjustedCount += explodedResult.Data.LayerAdjustedCount;
                stats.ColorAdjustedCount += explodedResult.Data.ColorAdjustedCount;
                entitiesToAdd.AddRange(explodedResult.Data.Entities);

                if (entitiesToAdd.Count == 0)
                    return OpResult<ExplodeAsShownResult>.Fail("无法获取爆炸后实体");

                var addedEntities = _serviceTrans.AppendEntitiesToCurrentSpace(entitiesToAdd);
                if (addedEntities.Count == 0)
                    return OpResult<ExplodeAsShownResult>.Fail("未能将实体添加到当前空间");

                stats.EntityIds = addedEntities;

                // 删除原块参照
                blkRef.Erase();

                return OpResult<ExplodeAsShownResult>.Success(stats);
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸块参照失败: {ex.Message}");
                return OpResult<ExplodeAsShownResult>.Fail(FormatExplodeErrorMessage(ex.Message));
            }
        }

        /// <summary>
        ///     爆炸后实体批次结果
        /// </summary>
        private class ExplodedEntitiesBatch
        {
            public List<Entity> Entities { get; } = new List<Entity>();
            public int LayerAdjustedCount { get; set; }
            public int ColorAdjustedCount { get; set; }
        }

        /// <summary>
        ///     处理爆炸后的实体，将非属性定义的实体添加到实体列表
        /// </summary>
        private OpResult<ExplodedEntitiesBatch> ProcessExplodedEntities(BlockReference blockRef)
        {
            try
            {
                if (!blockRef.ObjectId.IsValid)
                    return OpResult<ExplodedEntitiesBatch>.Fail("块参照无效");

                if (!blockRef.IsWriteEnabled)
                {
                    blockRef.UpgradeOpen();
                    if (!blockRef.IsWriteEnabled)
                        return OpResult<ExplodedEntitiesBatch>.Fail("无法以写方式打开块参照");
                }

                var blockDef = _serviceTrans.GetObject<BlockTableRecord>(blockRef.BlockTableRecord);
                if (blockDef == null || !blockRef.BlockTableRecord.IsValid)
                    return OpResult<ExplodedEntitiesBatch>.Fail("块定义无效");

                var layer = _serviceTrans.GetObject<LayerTableRecord>(blockRef.LayerId);
                if (layer != null && layer.IsLocked)
                    return OpResult<ExplodedEntitiesBatch>.Fail($"图块所在图层已锁定: {layer.Name}");

                var entityIds = _serviceTrans.GetChildObjects<DBObject>(blockDef);
                if (entityIds.Count == 0)
                    return OpResult<ExplodedEntitiesBatch>.Fail("块定义不含实体");

                if (blockRef.ScaleFactors.X == 0 || blockRef.ScaleFactors.Y == 0 || blockRef.ScaleFactors.Z == 0)
                    return OpResult<ExplodedEntitiesBatch>.Fail("块参照的缩放比例无效");

                if (double.IsNaN(blockRef.Rotation))
                    return OpResult<ExplodedEntitiesBatch>.Fail("块参照的旋转角度无效");

                var batch = new ExplodedEntitiesBatch();

                foreach (ObjectId entityId in entityIds)
                {
                    if (!entityId.IsValid) continue;

                    DBObject obj = _serviceTrans.GetObject<DBObject>(entityId);
                    if (obj == null) continue;

                    if (obj is AttributeDefinition)
                    {
                        obj.Dispose();
                        continue;
                    }

                    Entity entity;
                    if (obj is BlockReference nestedBlockRef)
                    {
                        var transformedPosition = nestedBlockRef.Position.TransformBy(blockRef.BlockTransform);
                        var newBlockRef = new BlockReference(transformedPosition, nestedBlockRef.BlockTableRecord)
                        {
                            ScaleFactors = new Scale3d(
                                nestedBlockRef.ScaleFactors.X * blockRef.ScaleFactors.X,
                                nestedBlockRef.ScaleFactors.Y * blockRef.ScaleFactors.Y,
                                nestedBlockRef.ScaleFactors.Z * blockRef.ScaleFactors.Z),
                            Rotation = nestedBlockRef.Rotation + blockRef.Rotation,
                            Layer = nestedBlockRef.Layer,
                            Color = nestedBlockRef.Color,
                            Linetype = nestedBlockRef.Linetype,
                        };
                        CopyXclipState(nestedBlockRef, newBlockRef);
                        entity = newBlockRef;
                    }
                    else if (obj is Entity sourceEntity)
                    {
                        entity = sourceEntity.Clone() as Entity;
                        if (entity != null)
                            entity.TransformBy(blockRef.BlockTransform);
                    }
                    else
                    {
                        Logger._.Warn($"遇到未处理的对象类型: {obj.GetType().Name}");
                        obj.Dispose();
                        continue;
                    }

                    if (entity != null)
                    {
                        var adjustStats = SetChildPropsAsBlk(entity, blockRef);
                        if (adjustStats.LayerAdjusted) batch.LayerAdjustedCount++;
                        if (adjustStats.ColorAdjusted) batch.ColorAdjustedCount++;
                        batch.Entities.Add(entity);
                    }

                    obj.Dispose();
                }

                if (batch.Entities.Count == 0)
                    return OpResult<ExplodedEntitiesBatch>.Fail("爆炸后实体数量为0");

                return OpResult<ExplodedEntitiesBatch>.Success(batch);
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸块参照时发生异常: {ex.Message}");
                return OpResult<ExplodedEntitiesBatch>.Fail(FormatExplodeErrorMessage(ex.Message));
            }
        }

        public void CopyXclipState(BlockReference source, BlockReference target)
        {
            try
            {
                const string filterDictName = "ACAD_FILTER";
                const string spatialName = "SPATIAL";

                var sourceExtDictId = source.ExtensionDictionary;
                if (!sourceExtDictId.IsValid) return;

                var sourceExtDict = _serviceTrans.GetObject<DBDictionary>(sourceExtDictId);
                if (sourceExtDict == null || !sourceExtDict.Contains(filterDictName)) return;

                var filterDictId = sourceExtDict.GetAt(filterDictName);
                var filterDict = _serviceTrans.GetObject<DBDictionary>(filterDictId);
                if (filterDict == null || !filterDict.Contains(spatialName)) return;

                var spatialFilterId = filterDict.GetAt(spatialName);
                var spatialFilter = _serviceTrans.GetObject<SpatialFilter>(spatialFilterId);
                if (spatialFilter == null) return;

                var targetInDatabase = target.ObjectId.IsValid;
                if (targetInDatabase && !target.IsWriteEnabled)
                    target.UpgradeOpen();

                if (!target.ExtensionDictionary.IsValid)
                    target.CreateExtensionDictionary();

                var targetExtDictId = target.ExtensionDictionary;
                if (!targetExtDictId.IsValid) return;

                var targetExtDict = GetDictionaryObject(targetExtDictId, OpenMode.ForWrite);
                if (targetExtDict == null) return;

                if (!targetInDatabase)
                    _serviceTrans.AddNewlyCreatedDBObject(targetExtDict, true);

                DBDictionary targetFilterDict;
                if (targetExtDict.Contains(filterDictName))
                    targetFilterDict = GetDictionaryObject(targetExtDict.GetAt(filterDictName), OpenMode.ForWrite);
                else
                {
                    targetFilterDict = new DBDictionary();
                    targetExtDict.SetAt(filterDictName, targetFilterDict);
                    _serviceTrans.AddNewlyCreatedDBObject(targetFilterDict, true);
                }

                if (targetFilterDict == null) return;

                var clonedFilter = new SpatialFilter { Definition = spatialFilter.Definition };

                if (targetFilterDict.Contains(spatialName))
                    targetFilterDict.Remove(spatialName);

                targetFilterDict.SetAt(spatialName, clonedFilter);
                _serviceTrans.AddNewlyCreatedDBObject(clonedFilter, true);
            }
            catch (Exception ex)
            {
                Logger._.Error($"复制XCLIP状态时发生异常: {ex.Message}", ex);
            }
        }

        private DBDictionary GetDictionaryObject(ObjectId dictionaryId, OpenMode openMode = OpenMode.ForRead)
        {
            if (!dictionaryId.IsValid) return null;

            try
            {
                if (dictionaryId.ObjectClass.Name == "AcDbDictionary" && dictionaryId.IsValid)
                {
                    var directDictionary = dictionaryId.GetObject(openMode) as DBDictionary;
                    if (directDictionary != null) return directDictionary;
                }
            }
            catch { /* fall through */ }

            return _serviceTrans.GetObject<DBDictionary>(dictionaryId, openMode);
        }

        // ── 属性转换 ──

        private List<DBText> ProcessAttributeReferences(BlockReference blockRef)
        {
            var textList = new List<DBText>();
            if (blockRef.AttributeCollection.Count == 0) return textList;

            foreach (ObjectId attId in blockRef.AttributeCollection)
            {
                try
                {
                    if (attId == ObjectId.Null || !attId.IsValid) continue;

                    var attRef = _serviceTrans.GetObject<AttributeReference>(attId);
                    if (attRef == null || attRef.Invisible) continue;

                    var text = ConvertAttributeToText(attRef);
                    if (text != null) textList.Add(text);
                }
                catch (Exception ex)
                {
                    Logger._.Warn($"处理属性引用失败: {ex.Message}");
                }
            }

            return textList;
        }

        private static DBText ConvertAttributeToText(AttributeReference attRef)
        {
            if (attRef == null)
            {
                Logger._.Warn("属性引用为空");
                return null;
            }

            var text = new DBText();
            try
            {
                PropertyUtils.MatchPropValues(text, attRef);
                return text;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"转换属性到文本时发生异常: {ex.Message}");
                text.Dispose();
                return null;
            }
        }

        // ── 属性继承 ──

        private struct ChildPropAdjustStats
        {
            public bool LayerAdjusted;
            public bool ColorAdjusted;
        }

        private ChildPropAdjustStats SetChildPropsAsBlk(Entity entChild, Entity entBlk)
        {
            var stats = new ChildPropAdjustStats();
            if (entChild == null || entBlk == null) return stats;

            try
            {
                if (MatchProp(entChild, entBlk, CadServiceManager.PropNames.Layer, CadServiceManager.Layers.Default).IsSuccess)
                    stats.LayerAdjusted = true;

                if (MatchProp(entChild, entBlk, CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByBlock).IsSuccess)
                    stats.ColorAdjusted = true;

                MatchProp(entChild, entBlk, CadServiceManager.PropNames.Linetype, CadServiceManager.Linetypes.ByBlock);
                MatchProp(entChild, entBlk, CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"处理实体属性时发生异常: {ex.Message}");
            }

            return stats;
        }

        private static OpResult<object> MatchProp(Entity entTo, Entity entFr, string propName, object valueToFix)
        {
            var result = PropertyUtils.MatchPropValue(entTo, entFr, propName, (entT, entF) =>
            {
                var getValueTo = PropertyUtils.GetPropertyValue(entT, propName);
                if (!getValueTo.IsSuccess) return false;
                return PropertyComparisonUtils.ValueEquals(getValueTo.Data, valueToFix);
            });
            return new OpResult<object>(result.IsSuccess, result.Message, result.Data);
        }

        // ── 错误格式化 ──

        private static string FormatExplodeErrorMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "爆炸图块时发生未知错误";

            if (message.IndexOf("eOnLockedLayer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "图块所在图层已锁定";

            if (message.IndexOf("eWasErased", StringComparison.OrdinalIgnoreCase) >= 0)
                return "图块已被删除";

            if (message.IndexOf("eNotInDatabase", StringComparison.OrdinalIgnoreCase) >= 0)
                return "图块不在当前图形数据库中";

            return message;
        }
    }
}
