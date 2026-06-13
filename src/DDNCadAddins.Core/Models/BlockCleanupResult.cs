using System.Collections.Generic;

namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     图块清理结果
    /// </summary>
    public class BlockCleanupResult
    {
        /// <summary>
        ///     执行的清理轮数
        /// </summary>
        public int IterationCount { get; set; }

        /// <summary>
        ///     爆炸生成的实体总数
        /// </summary>
        public int TotalExplodedEntityCount { get; set; }

        /// <summary>
        ///     删除的空定义图块总数
        /// </summary>
        public int TotalErasedEmptyBlockCount { get; set; }

        /// <summary>
        ///     累计失败原因统计
        /// </summary>
        public Dictionary<string, int> FailureCounts { get; } = new Dictionary<string, int>();

        /// <summary>
        ///     各轮清理明细
        /// </summary>
        public List<BlockCleanupRoundResult> Rounds { get; } = new List<BlockCleanupRoundResult>();
    }

    /// <summary>
    ///     单轮图块清理结果
    /// </summary>
    public class BlockCleanupRoundResult
    {
        /// <summary>
        ///     轮次编号（从 1 开始）
        /// </summary>
        public int Iteration { get; set; }

        /// <summary>
        ///     本轮尝试爆炸的图块数
        /// </summary>
        public int AttemptedCount { get; set; }

        /// <summary>
        ///     本轮爆炸生成的实体数
        /// </summary>
        public int ExplodedEntityCount { get; set; }

        /// <summary>
        ///     本轮失败原因统计
        /// </summary>
        public Dictionary<string, int> FailureCounts { get; } = new Dictionary<string, int>();
    }
}
