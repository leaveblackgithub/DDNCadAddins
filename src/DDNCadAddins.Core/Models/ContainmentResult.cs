namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     实体与边界的包含关系枚举.
    /// </summary>
    public enum ContainmentResult
    {
        /// <summary>
        ///     实体完全在边界内部.
        /// </summary>
        Inside,

        /// <summary>
        ///     实体在边界上（退化情况）.
        /// </summary>
        OnBoundary,

        /// <summary>
        ///     实体完全在边界外部.
        /// </summary>
        Outside,

        /// <summary>
        ///     实体与边界相交（需进一步拆分处理）.
        /// </summary>
        Intersects,
    }
}