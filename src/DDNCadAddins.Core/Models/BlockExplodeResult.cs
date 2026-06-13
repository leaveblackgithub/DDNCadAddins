namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     图块爆炸结果统计（与 CAD 实现无关）
    /// </summary>
    public class BlockExplodeResult
    {
        /// <summary>
        ///     爆炸后生成的实体数量
        /// </summary>
        public int EntityCount { get; set; }

        /// <summary>
        ///     由属性转换而来的文字数量
        /// </summary>
        public int AttributeTextCount { get; set; }

        /// <summary>
        ///     继承块参照图层的子实体数量
        /// </summary>
        public int LayerAdjustedCount { get; set; }

        /// <summary>
        ///     继承块参照颜色的子实体数量
        /// </summary>
        public int ColorAdjustedCount { get; set; }
    }
}
