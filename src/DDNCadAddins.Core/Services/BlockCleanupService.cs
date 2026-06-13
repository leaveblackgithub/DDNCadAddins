using System;
using System.Collections.Generic;
using System.Linq;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     图块清理业务服务 - 纯业务逻辑，无 CAD 依赖
    /// </summary>
    public class BlockCleanupService : IBlockCleanupService
    {
        /// <summary>
        ///     块定义不含实体时的错误消息（与 CAD 适配器保持一致）
        /// </summary>
        public const string EmptyDefinitionMessage = "块定义不含实体";

        private readonly IBlockRepository _blockRepository;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="blockRepository">图块仓储</param>
        public BlockCleanupService(IBlockRepository blockRepository)
        {
            _blockRepository = blockRepository;
        }

        /// <inheritdoc />
        public OpResult<BlockCleanupResult> CleanupNonXclippedBlocks(BlockCleanupOptions options = null)
        {
            try
            {
                if (IsCancellationRequested(options))
                {
                    return OpResult<BlockCleanupResult>.Fail(BlockCleanupOptions.CancelledMessage);
                }

                var cleanupResult = new BlockCleanupResult();
                var skippedBlockIds = new HashSet<string>();
                var hasMoreBlocks = true;

                while (hasMoreBlocks)
                {
                    if (IsCancellationRequested(options))
                    {
                        return OpResult<BlockCleanupResult>.Fail(BlockCleanupOptions.CancelledMessage);
                    }

                    cleanupResult.IterationCount++;
                    var roundResult = new BlockCleanupRoundResult
                    {
                        Iteration = cleanupResult.IterationCount
                    };
                    options?.OnRoundStarted?.Invoke(roundResult.Iteration);

                    var blocksResult = GetNonXclippedBlocks(skippedBlockIds);
                    if (!blocksResult.IsSuccess)
                    {
                        return OpResult<BlockCleanupResult>.Fail(blocksResult.Message);
                    }

                    var blocks = blocksResult.Data;
                    if (blocks.Count == 0)
                    {
                        cleanupResult.Rounds.Add(roundResult);
                        hasMoreBlocks = false;
                        continue;
                    }

                    var roundExploded = 0;

                    for (var index = 0; index < blocks.Count; index++)
                    {
                        if (IsCancellationRequested(options))
                        {
                            return OpResult<BlockCleanupResult>.Fail(BlockCleanupOptions.CancelledMessage);
                        }

                        var block = blocks[index];
                        if (block == null || string.IsNullOrEmpty(block.Id))
                        {
                            continue;
                        }

                        if (skippedBlockIds.Contains(block.Id))
                        {
                            continue;
                        }

                        roundResult.AttemptedCount++;
                        var explodeResult = _blockRepository.ExplodeBlock(block.Id);
                        if (!explodeResult.IsSuccess)
                        {
                            if (explodeResult.Message == EmptyDefinitionMessage)
                            {
                                var eraseResult = _blockRepository.EraseEmptyBlock(block.Id);
                                if (eraseResult.IsSuccess)
                                {
                                    cleanupResult.TotalErasedEmptyBlockCount++;
                                    continue;
                                }

                                RecordFailure(eraseResult.Message, roundResult.FailureCounts, cleanupResult.FailureCounts);
                            }
                            else
                            {
                                RecordFailure(explodeResult.Message, roundResult.FailureCounts, cleanupResult.FailureCounts);
                            }

                            skippedBlockIds.Add(block.Id);
                            continue;
                        }

                        var explodeStats = explodeResult.Data;
                        roundExploded += explodeStats.EntityCount;
                        var report = new BlockExplodeReport
                        {
                            BlockName = block.Name,
                            Stats = explodeStats
                        };
                        roundResult.ExplodeReports.Add(report);
                        options?.OnBlockExploded?.Invoke(report);

                        if (IsCancellationRequested(options))
                        {
                            return OpResult<BlockCleanupResult>.Fail(BlockCleanupOptions.CancelledMessage);
                        }
                    }

                    roundResult.ExplodedEntityCount = roundExploded;
                    cleanupResult.TotalExplodedEntityCount += roundExploded;
                    cleanupResult.Rounds.Add(roundResult);

                    var remainingResult = GetNonXclippedBlocks(skippedBlockIds);
                    if (!remainingResult.IsSuccess)
                    {
                        return OpResult<BlockCleanupResult>.Fail(remainingResult.Message);
                    }

                    if (remainingResult.Data.Count == 0 || roundExploded == 0)
                    {
                        hasMoreBlocks = false;
                    }
                }

                return OpResult<BlockCleanupResult>.Success(cleanupResult);
            }
            catch (Exception ex)
            {
                return OpResult<BlockCleanupResult>.Fail($"图块清理失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     获取当前空间中未跳过且非 XCLIP 的图块
        /// </summary>
        /// <param name="skippedBlockIds">已跳过图块 ID 集合</param>
        /// <returns>图块列表</returns>
        private OpResult<IReadOnlyList<BlockInfo>> GetNonXclippedBlocks(HashSet<string> skippedBlockIds)
        {
            var allBlocksResult = _blockRepository.GetAllBlocksInCurrentSpace();
            if (!allBlocksResult.IsSuccess)
            {
                return OpResult<IReadOnlyList<BlockInfo>>.Fail(allBlocksResult.Message);
            }

            var blocks = allBlocksResult.Data
                .Where(block => block != null
                    && !string.IsNullOrEmpty(block.Id)
                    && !block.IsXclipped
                    && !skippedBlockIds.Contains(block.Id))
                .ToList();

            return OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly());
        }

        /// <summary>
        ///     记录失败原因并按原因汇总数量
        /// </summary>
        /// <param name="reason">失败原因</param>
        /// <param name="roundFailureCounts">本轮失败统计</param>
        /// <param name="totalFailureCounts">累计失败统计</param>
        private static void RecordFailure(
            string reason,
            Dictionary<string, int> roundFailureCounts,
            Dictionary<string, int> totalFailureCounts)
        {
            var message = string.IsNullOrWhiteSpace(reason) ? "未知错误" : reason;
            IncrementCount(roundFailureCounts, message);
            IncrementCount(totalFailureCounts, message);
        }

        /// <summary>
        ///     增加字典中指定键的计数
        /// </summary>
        /// <param name="counts">计数字典</param>
        /// <param name="key">键</param>
        private static void IncrementCount(Dictionary<string, int> counts, string key)
        {
            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
            else
            {
                counts[key] = 1;
            }
        }

        /// <summary>
        ///     检查是否收到取消请求
        /// </summary>
        /// <param name="options">清理选项</param>
        /// <returns>是否应取消</returns>
        private static bool IsCancellationRequested(BlockCleanupOptions options)
        {
            try
            {
                return options?.IsCancellationRequested != null && options.IsCancellationRequested();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
