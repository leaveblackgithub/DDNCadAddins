using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     图层管理接口 - 图层的增删改查操作
    /// </summary>
    public interface ILayerService
    {
        /// <summary>
        ///     获取图层表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>图层表</returns>
        LayerTable GetLayerTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>图层对象，如果不存在则返回null</returns>
        LayerTableRecord GetLayer(string layerName, OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取或创建图层，已存在则返回现有图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>图层对象，如果操作失败则返回null</returns>
        LayerTableRecord GetOrCreateLayer(string layerName = "",
            short colorIndex = CadServiceManager.Colors.White,
            string lineTypeName = CadServiceManager.Linetypes.Continuous);

        /// <summary>
        ///     创建新图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的图层对象，如果创建失败则返回null</returns>
        LayerTableRecord CreateLayer(string layerName = "",
            short colorIndex = CadServiceManager.Colors.White,
            string lineTypeName = CadServiceManager.Linetypes.Continuous);

        /// <summary>
        ///     获取有效的图层名称
        /// </summary>
        /// <param name="layerName">原始图层名称</param>
        /// <returns>有效的图层名称</returns>
        string GetValidLayerName(string layerName);

        /// <summary>
        ///     获取当前图层名称
        /// </summary>
        /// <returns>当前图层名称</returns>
        string GetCurrentLayerName();
    }
}
