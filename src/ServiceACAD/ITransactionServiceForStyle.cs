using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务样式部分接口，提供图层和线型管理功能
    /// </summary>
    public interface ITransactionServiceForStyle
    {
        /// <summary>
        ///     获取图层表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>图层表</returns>
        LayerTable GetLayerTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     创建新线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的线型对象，如果创建失败则返回null</returns>
        LinetypeTableRecord GetOrCreateLineType(string lineTypeName);

        /// <summary>
        ///     创建新线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的线型对象，如果创建失败则返回null</returns>
        LinetypeTableRecord CreateLineType(string lineTypeName);

        /// <summary>
        ///     获取图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>图层对象，如果不存在则返回null</returns>
        LayerTableRecord GetLayer(string layerName, OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取线型表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>线型表</returns>
        LinetypeTable GetLineTypeTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取有效的图层名称
        /// </summary>
        /// <param name="layerName">原始图层名称</param>
        /// <returns>有效的图层名称</returns>
        string GetValidLayerName(string layerName);

        ObjectId GetValidLineTypeId(string lineTypeName);

        /// <summary>
        ///     获取有效的线型名称
        /// </summary>
        /// <param name="linetypeName">原始线型名称</param>
        /// <returns>有效的线型名称</returns>
        string GetValidLineTypeName(string linetypeName);

        /// <summary>
        ///     获取有效的颜色索引
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色索引</returns>
        short GetValidColorIndex(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite);

        /// <summary>
        ///     获取有效的颜色
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色</returns>
        Color GetValidColor(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite);

        /// <summary>
        ///     获取当前图层名称
        /// </summary>
        /// <returns>当前图层名称</returns>
        string GetCurrentLayerName();

        /// <summary>
        ///     创建新图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="lineTypeName"></param>
        /// <returns>创建的图层对象，如果创建失败则返回null</returns>
        LayerTableRecord GetOrCreateLayer(string layerName = "",
            short colorIndex = CadServiceManager.ColorIndexWhite,
            string lineTypeName = CadServiceManager.LineTypeContinuous);

        /// <summary>
        ///     创建新图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="lineTypeName"></param>
        /// <returns>创建的图层对象，如果创建失败则返回null</returns>
        LayerTableRecord CreateLayer(string layerName = "",
            short colorIndex = CadServiceManager.ColorIndexWhite,
            string lineTypeName = CadServiceManager.LineTypeContinuous);

        /// <summary>
        ///     获取线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>线型对象，如果不存在则返回null</returns>
        LinetypeTableRecord GetLineType(string lineTypeName, OpenMode openMode = OpenMode.ForRead);
    }
}
