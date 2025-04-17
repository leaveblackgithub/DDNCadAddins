namespace DDNCadAddins.Services
{
    /// <summary>
    ///     XClip块服务接口 - 门面模式(Facade)
    ///     组合了三个子接口：IXClipBlockFinder、IXClipBlockCreator和IXClipBlockManager
    ///     提供XClip块操作的完整功能集.
    /// </summary>
    public interface IXClipBlockService : IXClipBlockFinder, IXClipBlockCreator, IXClipBlockManager
    {
        // 所有方法均继承自子接口，无需额外定义
    }
}
