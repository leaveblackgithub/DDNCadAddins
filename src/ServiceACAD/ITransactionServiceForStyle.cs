namespace ServiceACAD
{
    /// <summary>
    ///     事务服务样式部分接口，组合图层、线型、颜色和状态管理功能
    /// </summary>
    public interface ITransactionServiceForStyle :
        ILayerService,
        ILinetypeService,
        IColorService,
        ILayerStateService
    {
    }
}
