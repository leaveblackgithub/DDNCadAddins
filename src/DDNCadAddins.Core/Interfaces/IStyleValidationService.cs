using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     颜色/线型验证服务接口 - 纯逻辑，无 CAD 依赖
    /// </summary>
    public interface IStyleValidationService
    {
        /// <summary>
        ///     获取有效的 ACI 颜色索引，超出 0-255 范围时返回默认值
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色索引</returns>
        short GetValidColorIndex(short colorIndex, short defaultColorIndex = CadStyleConstants.Colors.White);

        /// <summary>
        ///     判断颜色索引是否在 ACI 有效范围内（0-255）
        /// </summary>
        /// <param name="colorIndex">颜色索引</param>
        /// <returns>是否有效</returns>
        bool IsValidAciColorIndex(short colorIndex);

        /// <summary>
        ///     规范化线型名称，空值回退为 Continuous
        /// </summary>
        /// <param name="lineTypeName">原始线型名称</param>
        /// <returns>规范化后的线型名称</returns>
        string NormalizeLineTypeName(string lineTypeName);
    }
}
