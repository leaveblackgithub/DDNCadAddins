using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     图块清理业务服务接口
    /// </summary>
    public interface IBlockCleanupService
    {
        /// <summary>
        ///     多轮清理当前空间中所有非 XCLIP 图块
        /// </summary>
        /// <returns>清理结果</returns>
        OpResult<BlockCleanupResult> CleanupNonXclippedBlocks();
    }
}
