using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     属性操作工具类 - 提供反射相关的属性操作方法
    /// </summary>
    public static class PropertyUtils
    {
        /// <summary>
        ///     检查对象是否具有指定属性
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果对象具有该属性返回true，否则返回false</returns>
        public static bool HasProperty(object obj, string propertyName)
        {
            // 参数有效性检查
            if (obj == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            try
            {
                // 使用反射获取属性
                var property = obj.GetType().GetProperty(propertyName);
                if (property == null || property.GetValue(obj) == null)
                {
                    return false;
                }

                return property != null;
            }
            catch (Exception ex)
            {
                Logger._.Error($"\n警告: 检查对象属性时发生异常: {ex.Message}");
                // 捕获任何异常，确保方法不会抛出异常
                return false;
            }
        }

        /// <summary>
        ///     获取对象的属性值
        /// </summary>
        /// <param name="obj">要获取属性的对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值的操作结果</returns>
        public static OpResult<object> GetPropertyValue(object obj, string propertyName)
        {
            // 参数有效性检查
            if (obj == null)
            {
                return OpResult<object>.Fail("对象不能为空");
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                return OpResult<object>.Fail("属性名称不能为空");
            }

            try
            {
                // 检查属性是否存在
                var property = obj.GetType().GetProperty(propertyName);
                if (property == null)
                {
                    return OpResult<object>.Fail($"对象 {obj.GetType().Name} 不包含属性 {propertyName}");
                }

                // 获取属性值
                var value = property.GetValue(obj);
                return OpResult<object>.Success(value);
            }
            catch (Exception ex)
            {
                return OpResult<object>.Fail($"获取属性 {propertyName} 值失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     设置对象的属性值
        /// </summary>
        /// <param name="obj">要设置属性的对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <param name="value">要设置的值</param>
        /// <returns>设置结果</returns>
        public static OpResult<object> SetPropertyValue(object obj, string propertyName, object value)
        {
            // 参数有效性检查
            if (obj == null)
            {
                return OpResult<object>.Fail("对象不能为空");
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                return OpResult<object>.Fail("属性名称不能为空");
            }

            try
            {
                // 检查属性是否存在
                var property = obj.GetType().GetProperty(propertyName);
                if (property == null)
                {
                    return OpResult<object>.Fail($"对象 {obj.GetType().Name} 不包含属性 {propertyName}");
                }

                // 检查属性是否可写
                if (!property.CanWrite)
                {
                    return OpResult<object>.Fail($"属性 {propertyName} 不可写");
                }

                // 检查值类型是否兼容
                if (value != null && !property.PropertyType.IsAssignableFrom(value.GetType()))
                {
                    try
                    {
                        // 尝试进行类型转换
                        value = Convert.ChangeType(value, property.PropertyType);
                    }
                    catch
                    {
                        return OpResult<object>.Fail(
                            $"值类型 {value.GetType().Name} 不能转换为属性类型 {property.PropertyType.Name}");
                    }
                }

                // 设置属性值
                property.SetValue(obj, value);
                return OpResult<object>.Success(value);
            }
            catch (Exception ex)
            {
                return OpResult<object>.Fail($"设置属性 {propertyName} 值失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     匹配两个实体的属性值
        /// </summary>
        /// <param name="objTo"></param>
        /// <param name="objFr"></param>
        /// <param name="propertyName">属性名称</param>
        /// <param name="entToFilter"></param>
        /// <param name="valueToFix">需要匹配的默认值</param>
        /// <returns>匹配结果</returns>
        public static OpResult<object> MatchPropValues(object objTo, object objFr, string propertyName,
            Func<object, object, bool> entToFilter = null)
        {
            // 参数有效性检查
            if (objFr == null)
            {
                return OpResult<object>.Fail("目标实体不能为空");
            }

            if (objTo == null)
            {
                return OpResult<object>.Fail("源实体不能为空");
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                return OpResult<object>.Fail("属性名称不能为空");
            }

            try
            {
                if (entToFilter != null || entToFilter(objTo,objFr))
                {
                    return OpResult<object>.Fail("不需要修改属性值");
                }

                var valueFrResult = GetPropertyValue(objFr, propertyName);
                if (!valueFrResult.IsSuccess)
                {
                    return valueFrResult;
                }

                var valueToResult = GetPropertyValue(objTo, propertyName);
                if (!valueToResult.IsSuccess)
                {
                    return valueToResult;
                }

                if (valueFrResult.Data.GetType() != valueToResult.Data.GetType())
                {
                    return OpResult<object>.Fail("属性值类型不一致");
                }

                return SetPropertyValue(objTo, propertyName, valueToResult.Data);
            }

            catch (Exception ex)
            {
                return OpResult<object>.Fail($"匹配属性 {propertyName} 失败: {ex.Message}");
            }
        }
    }
}
