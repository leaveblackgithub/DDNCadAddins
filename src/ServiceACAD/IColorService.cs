using Autodesk.AutoCAD.Colors;

namespace ServiceACAD
{
    /// <summary>
    ///     颜色工具接口 - 颜色索引验证和转换
    /// </summary>
    public interface IColorService
    {
        /// <summary>
        ///     获取有效的颜色索引，超出范围时返回默认值
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色索引</returns>
        short GetValidColorIndex(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite);

        /// <summary>
        ///     获取有效的颜色对象，超出范围时返回默认颜色
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色对象</returns>
        Color GetValidColor(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite);
    }
}
