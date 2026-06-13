using System;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     颜色/线型验证业务服务 - 纯逻辑，无 CAD 依赖
    /// </summary>
    public class StyleValidationService : IStyleValidationService
    {
        /// <inheritdoc />
        public short GetValidColorIndex(short colorIndex, short defaultColorIndex = CadStyleConstants.Colors.White)
        {
            try
            {
                if (IsValidAciColorIndex(colorIndex))
                {
                    return colorIndex;
                }

                return IsValidAciColorIndex(defaultColorIndex)
                    ? defaultColorIndex
                    : CadStyleConstants.Colors.White;
            }
            catch (Exception)
            {
                return CadStyleConstants.Colors.White;
            }
        }

        /// <inheritdoc />
        public bool IsValidAciColorIndex(short colorIndex) =>
            colorIndex >= CadStyleConstants.Colors.MinAciIndex
            && colorIndex <= CadStyleConstants.Colors.MaxAciIndex;

        /// <inheritdoc />
        public string NormalizeLineTypeName(string lineTypeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(lineTypeName))
                {
                    return CadStyleConstants.Linetypes.Continuous;
                }

                return lineTypeName.Trim();
            }
            catch (Exception)
            {
                return CadStyleConstants.Linetypes.Continuous;
            }
        }
    }
}
