namespace ServiceACAD
{
    /// <summary>
    ///     图层状态管理接口 - 图层锁定/冻结状态的捕获与恢复
    /// </summary>
    public interface ILayerStateService
    {
        /// <summary>
        ///     记录所有图层的锁定与冻结状态
        /// </summary>
        /// <returns>图层状态快照</returns>
        OpResult<LayerStateSnapshot> CaptureAllLayerStates();

        /// <summary>
        ///     解锁并解冻所有图层
        /// </summary>
        /// <returns>操作结果</returns>
        OpResult<bool> UnlockAndThawAllLayers();

        /// <summary>
        ///     根据快照恢复图层的锁定与冻结状态
        /// </summary>
        /// <param name="snapshot">图层状态快照</param>
        /// <returns>操作结果</returns>
        OpResult<bool> RestoreLayerStates(LayerStateSnapshot snapshot);
    }
}
