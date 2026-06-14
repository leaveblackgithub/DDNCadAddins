using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace ServiceACAD.Adapters
{
    /// <summary>
    ///     AutoCAD 图块仓储适配器 - 负责 CAD 类型与 Core POCO 的转换
    /// </summary>
    public class AutoCadBlockRepository : IBlockRepository
    {
        private readonly ITransactionService _transactionService;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        public AutoCadBlockRepository(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<IReadOnlyList<BlockInfo>> IBlockRepository.GetAllBlocksInCurrentSpace()
        {
            try
            {
                var blockRefIds = _transactionService.GetChildObjectsFromCurrentSpace<BlockReference>();
                var blocks = new List<BlockInfo>();

                foreach (var blockRefId in blockRefIds)
                {
                    try
                    {
                        if (!blockRefId.IsValid || blockRefId.IsErased)
                        {
                            continue;
                        }

                        var blockService = _transactionService.Block.GetBlockService(blockRefId);
                        if (blockService == null)
                        {
                            continue;
                        }

                        blocks.Add(new BlockInfo
                        {
                            Id = blockRefId.Handle.Value.ToString(),
                            Name = blockService.Name,
                            IsXclipped = blockService.IsXclipped()
                        });
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        Logger._.Warn($"跳过无效图块 (ObjectId={blockRefId}): {ex.ErrorStatus}");
                    }
                }

                return DDNCadAddins.Core.Models.OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly());
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取当前空间图块异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<IReadOnlyList<BlockInfo>>.Fail($"获取当前空间图块失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<bool> IBlockRepository.IsBlockXclipped(string blockId)
        {
            try
            {
                if (!TryResolveBlockId(blockId, out var objectId))
                {
                    return DDNCadAddins.Core.Models.OpResult<bool>.Success(false);
                }

                var blockService = _transactionService.Block.GetBlockService(objectId);
                if (blockService == null)
                {
                    return DDNCadAddins.Core.Models.OpResult<bool>.Success(false);
                }

                return DDNCadAddins.Core.Models.OpResult<bool>.Success(blockService.IsXclipped());
            }
            catch (Exception ex)
            {
                Logger._.Error($"检查图块 XCLIP 状态失败: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<bool>.Success(false);
            }
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<BlockExplodeResult> IBlockRepository.ExplodeBlock(string blockId)
        {
            try
            {
                if (!TryResolveBlockId(blockId, out var objectId))
                {
                    return DDNCadAddins.Core.Models.OpResult<BlockExplodeResult>.Fail("图块不存在");
                }

                var blockService = _transactionService.Block.GetBlockService(objectId);
                if (blockService == null)
                {
                    return DDNCadAddins.Core.Models.OpResult<BlockExplodeResult>.Fail("无法获取图块服务");
                }

                var explodeResult = blockService.ExplodeAsShown();
                if (!explodeResult.IsSuccess)
                {
                    return DDNCadAddins.Core.Models.OpResult<BlockExplodeResult>.Fail(explodeResult.Message);
                }

                var stats = explodeResult.Data;
                var entityCount = stats?.EntityIds == null ? 0 : stats.EntityIds.Count;
                return DDNCadAddins.Core.Models.OpResult<BlockExplodeResult>.Success(new BlockExplodeResult
                {
                    EntityCount = entityCount,
                    AttributeTextCount = stats?.AttributeTextCount ?? 0,
                    LayerAdjustedCount = stats?.LayerAdjustedCount ?? 0,
                    ColorAdjustedCount = stats?.ColorAdjustedCount ?? 0
                });
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸图块异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<BlockExplodeResult>.Fail($"爆炸图块失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<bool> IBlockRepository.EraseEmptyBlock(string blockId)
        {
            try
            {
                if (!TryResolveBlockId(blockId, out var objectId))
                {
                    return DDNCadAddins.Core.Models.OpResult<bool>.Fail("图块不存在");
                }

                var blockService = _transactionService.Block.GetBlockService(objectId);
                if (blockService == null)
                {
                    return DDNCadAddins.Core.Models.OpResult<bool>.Fail("无法获取图块服务");
                }

                var eraseResult = blockService.EraseIfEmptyDefinition();
                if (!eraseResult.IsSuccess)
                {
                    return DDNCadAddins.Core.Models.OpResult<bool>.Fail(eraseResult.Message);
                }

                return DDNCadAddins.Core.Models.OpResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Logger._.Error($"删除空定义图块异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<bool>.Fail($"删除空定义图块失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     通过 Handle 字符串解析图块 ObjectId
        /// </summary>
        /// <param name="blockId">图块 Handle 字符串</param>
        /// <param name="objectId">解析后的 ObjectId</param>
        /// <returns>是否解析成功</returns>
        private bool TryResolveBlockId(string blockId, out ObjectId objectId)
        {
            objectId = ObjectId.Null;

            if (string.IsNullOrEmpty(blockId) || !ulong.TryParse(blockId, out var handleValue))
            {
                return false;
            }

            return CadServiceManager._.CadDb.TryGetObjectId(new Handle((long)handleValue), out objectId)
                && objectId.IsValid
                && !objectId.IsErased;
        }
    }
}
