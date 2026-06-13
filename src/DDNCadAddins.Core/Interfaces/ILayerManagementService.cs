using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     图层状态管理业务服务接口
    /// </summary>
    public interface ILayerManagementService
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
