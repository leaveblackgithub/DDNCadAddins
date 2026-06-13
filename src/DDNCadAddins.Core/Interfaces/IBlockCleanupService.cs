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
        /// <param name="options">可选的进度回调与取消检测</param>
        /// <returns>清理结果</returns>
        OpResult<BlockCleanupResult> CleanupNonXclippedBlocks(BlockCleanupOptions options = null);
    }
}
