namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     图层信息 POCO 模型，无 CAD 依赖
    /// </summary>
    public class LayerInfo
    {
        /// <summary>
        ///     图层名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     是否锁定
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        ///     是否冻结
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        ///     颜色索引（ACI）
        /// </summary>
        public short ColorIndex { get; set; }

        /// <summary>
        ///     线型名称
        /// </summary>
        public string LinetypeName { get; set; }
    }
}
