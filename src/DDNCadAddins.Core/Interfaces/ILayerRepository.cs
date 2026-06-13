using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     图层仓储接口 - 抽象图层数据访问，由 CAD 适配器或测试 Fake 实现
    /// </summary>
    public interface ILayerRepository
    {
        /// <summary>
        ///     按名称获取图层信息
        /// </summary>
        /// <param name="name">图层名称</param>
        /// <returns>图层信息</returns>
        OpResult<LayerInfo> GetLayer(string name);

        /// <summary>
        ///     获取所有图层信息
        /// </summary>
        /// <returns>图层列表</returns>
        OpResult<IReadOnlyList<LayerInfo>> GetAllLayers();

        /// <summary>
        ///     更新图层信息
        /// </summary>
        /// <param name="layer">待更新的图层信息</param>
        /// <returns>操作结果</returns>
        OpResult UpdateLayer(LayerInfo layer);

        /// <summary>
        ///     获取当前图层名称
        /// </summary>
        /// <returns>当前图层名称</returns>
        OpResult<string> GetCurrentLayerName();
    }
}
