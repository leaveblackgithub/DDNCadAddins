namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     图块信息 POCO 模型，无 CAD 依赖
    /// </summary>
    public class BlockInfo
    {
        /// <summary>
        ///     图块唯一标识（CAD Handle 字符串）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     图块名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     是否被 XCLIP 裁剪
        /// </summary>
        public bool IsXclipped { get; set; }
    }
}
