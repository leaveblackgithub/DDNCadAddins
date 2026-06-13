using System;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     属性值比较工具 - 纯逻辑，无 CAD 依赖
    /// </summary>
    public static class PropertyComparisonUtils
    {
        /// <summary>
        ///     比较两个值是否相等，支持不同类型之间的比较
        /// </summary>
        /// <param name="value1">第一个值</param>
        /// <param name="value2">第二个值</param>
        /// <returns>如果两个值相等返回 true，否则返回 false</returns>
        public static bool ValueEquals(object value1, object value2)
        {
            try
            {
                if (value1 == null && value2 == null)
                {
                    return true;
                }

                if (value1 == null || value2 == null)
                {
                    return false;
                }

                if (value1 is string strValue1 && value2 is string strValue2)
                {
                    return string.Equals(strValue1, strValue2, StringComparison.OrdinalIgnoreCase);
                }

                if (value1.GetType() == value2.GetType())
                {
                    return value1.Equals(value2);
                }

                try
                {
                    var convertedValue = Convert.ChangeType(value1, value2.GetType());
                    return convertedValue.Equals(value2);
                }
                catch
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
