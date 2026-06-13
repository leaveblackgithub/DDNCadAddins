using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     图块仓储接口 - 抽象图块数据访问，由 CAD 适配器或测试 Fake 实现
    /// </summary>
    public interface IBlockRepository
    {
        /// <summary>
        ///     获取当前空间中所有图块信息
        /// </summary>
        /// <returns>图块列表</returns>
        OpResult<IReadOnlyList<BlockInfo>> GetAllBlocksInCurrentSpace();

        /// <summary>
        ///     爆炸指定图块
        /// </summary>
        /// <param name="blockId">图块标识</param>
        /// <returns>爆炸结果统计</returns>
        OpResult<BlockExplodeResult> ExplodeBlock(string blockId);

        /// <summary>
        ///     删除空定义图块参照
        /// </summary>
        /// <param name="blockId">图块标识</param>
        /// <returns>操作结果</returns>
        OpResult<bool> EraseEmptyBlock(string blockId);
    }
}
