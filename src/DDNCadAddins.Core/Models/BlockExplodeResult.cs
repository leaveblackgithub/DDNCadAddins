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

        /// <summary>
        ///     将另一个结果累加到此结果
        /// </summary>
        /// <param name="other">要累加的结果</param>
        public void Add(BlockExplodeResult other)
        {
            if (other == null) return;
            EntityCount += other.EntityCount;
            AttributeTextCount += other.AttributeTextCount;
            LayerAdjustedCount += other.LayerAdjustedCount;
            ColorAdjustedCount += other.ColorAdjustedCount;
        }

        /// <summary>
        ///     创建累加后的新实例
        /// </summary>
        /// <param name="results">要累加的结果列表</param>
        /// <returns>累加后的新实例</returns>
        public static BlockExplodeResult Aggregate(params BlockExplodeResult[] results)
        {
            var total = new BlockExplodeResult();
            foreach (var result in results)
            {
                total.Add(result);
            }
            return total;
        }
    }
}
