using System;

namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     图块清理可选行为（进度回调与取消检测）
    /// </summary>
    public class BlockCleanupOptions
    {
        /// <summary>
        ///     用户取消时的标准消息
        /// </summary>
        public const string CancelledMessage = "用户已取消操作。";

        /// <summary>
        ///     返回 true 时中止清理并回滚
        /// </summary>
        public Func<bool> IsCancellationRequested { get; set; }

        /// <summary>
        ///     新一轮清理开始时回调（轮次从 1 开始）
        /// </summary>
        public Action<int> OnRoundStarted { get; set; }

        /// <summary>
        ///     单个图块爆炸成功后回调
        /// </summary>
        public Action<BlockExplodeReport> OnBlockExploded { get; set; }
    }
}
