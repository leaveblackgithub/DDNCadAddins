namespace ServiceACAD
{
    /// <summary>
    ///     长耗时命令的用户取消检测
    /// </summary>
    public interface ICommandCancellation
    {
        /// <summary>
        ///     用户是否已请求取消（如按下 ESC）
        /// </summary>
        bool IsCancellationRequested { get; }
    }
}
