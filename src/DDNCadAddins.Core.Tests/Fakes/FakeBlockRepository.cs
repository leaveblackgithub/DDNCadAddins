using System.Collections.Generic;
using System.Linq;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;

namespace DDNCadAddins.Core.Tests.Fakes
{
    /// <summary>
    ///     图块仓储 Fake 实现，用于纯单元测试
    /// </summary>
    public class FakeBlockRepository : IBlockRepository
    {
        /// <summary>
        ///     内存中的图块列表
        /// </summary>
        public List<BlockInfo> Blocks { get; set; } = new List<BlockInfo>();

        /// <summary>
        ///     模拟 GetAllBlocksInCurrentSpace 失败
        /// </summary>
        public bool ShouldFailGetAll { get; set; }

        /// <summary>
        ///     模拟 ExplodeBlock 失败
        /// </summary>
        public HashSet<string> ExplodeFailBlockIds { get; } = new HashSet<string>();

        /// <summary>
        ///     模拟 ExplodeBlock 返回空定义错误
        /// </summary>
        public HashSet<string> EmptyDefinitionBlockIds { get; } = new HashSet<string>();

        /// <summary>
        ///     模拟 EraseEmptyBlock 失败
        /// </summary>
        public HashSet<string> EraseFailBlockIds { get; } = new HashSet<string>();

        /// <summary>
        ///     记录已爆炸的图块 ID
        /// </summary>
        public List<string> ExplodedBlockIds { get; } = new List<string>();

        /// <summary>
        ///     记录已删除的空定义图块 ID
        /// </summary>
        public List<string> ErasedBlockIds { get; } = new List<string>();

        /// <summary>
        ///     每个图块爆炸后生成的实体数
        /// </summary>
        public Dictionary<string, int> ExplodeEntityCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        ///     图块爆炸后在当前空间出现的后续图块
        /// </summary>
        public Dictionary<string, BlockInfo> FollowUpBlocksAfterExplode { get; } = new Dictionary<string, BlockInfo>();

        /// <inheritdoc />
        public OpResult<IReadOnlyList<BlockInfo>> GetAllBlocksInCurrentSpace()
        {
            if (ShouldFailGetAll)
            {
                return OpResult<IReadOnlyList<BlockInfo>>.Fail("模拟获取图块失败");
            }

            var blocks = Blocks
                .Where(block => block != null && !string.IsNullOrEmpty(block.Id))
                .Select(CloneBlock)
                .ToList();

            return OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly());
        }

        /// <inheritdoc />
        public OpResult<bool> IsBlockXclipped(string blockId)
        {
            if (string.IsNullOrEmpty(blockId))
            {
                return OpResult<bool>.Success(false);
            }

            var block = Blocks.FirstOrDefault(b => b.Id == blockId);
            return OpResult<bool>.Success(block?.IsXclipped ?? false);
        }

        /// <inheritdoc />
        public OpResult<BlockExplodeResult> ExplodeBlock(string blockId)
        {
            if (string.IsNullOrEmpty(blockId) || Blocks.All(b => b.Id != blockId))
            {
                return OpResult<BlockExplodeResult>.Fail("图块不存在");
            }

            var targetBlock = Blocks.FirstOrDefault(b => b.Id == blockId);
            if (targetBlock != null && targetBlock.IsXclipped)
            {
                return OpResult<BlockExplodeResult>.Fail("XCLIP 图块不应被爆炸，需后续处理");
            }

            if (EmptyDefinitionBlockIds.Contains(blockId))
            {
                return OpResult<BlockExplodeResult>.Fail(BlockCleanupService.EmptyDefinitionMessage);
            }

            if (ExplodeFailBlockIds.Contains(blockId))
            {
                return OpResult<BlockExplodeResult>.Fail("模拟爆炸失败");
            }

            ExplodedBlockIds.Add(blockId);
            Blocks.RemoveAll(b => b.Id == blockId);

            if (FollowUpBlocksAfterExplode.ContainsKey(blockId))
            {
                Blocks.Add(CloneBlock(FollowUpBlocksAfterExplode[blockId]));
            }

            var entityCount = ExplodeEntityCounts.ContainsKey(blockId) ? ExplodeEntityCounts[blockId] : 1;
            return OpResult<BlockExplodeResult>.Success(new BlockExplodeResult
            {
                EntityCount = entityCount
            });
        }

        /// <inheritdoc />
        public OpResult<bool> EraseEmptyBlock(string blockId)
        {
            if (string.IsNullOrEmpty(blockId) || Blocks.All(block => block.Id != blockId))
            {
                return OpResult<bool>.Fail("图块不存在");
            }

            if (EraseFailBlockIds.Contains(blockId))
            {
                return OpResult<bool>.Fail("模拟删除空定义图块失败");
            }

            ErasedBlockIds.Add(blockId);
            Blocks.RemoveAll(block => block.Id == blockId);
            return OpResult<bool>.Success(true);
        }

        /// <summary>
        ///     克隆图块信息，避免测试间共享引用
        /// </summary>
        /// <param name="block">源图块</param>
        /// <returns>克隆后的图块</returns>
        private static BlockInfo CloneBlock(BlockInfo block)
        {
            return new BlockInfo
            {
                Id = block.Id,
                Name = block.Name,
                IsXclipped = block.IsXclipped
            };
        }
    }
}
