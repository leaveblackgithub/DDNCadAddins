using System;
using System.ComponentModel;
using System.Linq;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;

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
            catch (Exception)
            {
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
                if (!CanBeConvertedFrom(property.PropertyType, value.GetType()))
                {
                    return OpResult<object>.Fail(
                        $"值类型 {value.GetType().Name} 不能转换为属性类型 {property.PropertyType.Name}");
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
        ///     检查前者类型的属性是否能接受后者类型的赋值
        /// </summary>
        /// <param name="targetType">目标属性的类型</param>
        /// <param name="sourceType">源数据的类型</param>
        /// <returns>如果可以转换/赋值返回true，否则返回false</returns>
        /// <remarks>
        ///     此方法综合检查以下情况：
        ///     1. 类型相同或直接兼容的情况
        ///     2. 数值类型的隐式转换（小范围到大范围）
        ///     3. 可空类型的赋值规则
        ///     4. 继承关系和接口实现
        ///     5. 类型转换器支持的转换
        ///     6. 特殊类型转换（如字符串到枚举、Guid等）
        /// </remarks>
        public static bool CanBeConvertedFrom(Type targetType, Type sourceType) =>
            PropertyConversionUtils.CanBeConvertedFrom(targetType, sourceType);

        /// <summary>
        ///     匹配两个实体的属性值
        /// </summary>
        /// <param name="objTo"></param>
        /// <param name="objFr"></param>
        /// <param name="propertyName">属性名称</param>
        /// <param name="filter"></param>
        /// <param name="valueToFix">需要匹配的默认值</param>
        /// <returns>匹配结果</returns>
        public static OpResult<object> MatchPropValue(object objTo, object objFr, string propertyName,
            Func<object, object, bool> filter = null)
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
                if (filter != null && !filter(objTo, objFr))
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

                return SetPropertyValue(objTo, propertyName, valueFrResult.Data);
            }

            catch (Exception ex)
            {
                return OpResult<object>.Fail($"匹配属性 {propertyName} 失败: {ex.Message}");
            }
        }

        public static OpResult MatchPropValues(object objTo, object objFr,
            params string[] propertyNamesToIgnore)

        {
            if (objTo == null)
            {
                return OpResult.Fail("目标实体不能为空");
            }

            if (objFr == null)
            {
                return OpResult.Fail("源实体不能为空");
            }

            try
            {
                var objToType = objTo.GetType();
                foreach (var propertyInfo in objToType.GetProperties())
                {
                    var propertyName = propertyInfo.Name;
                    if (propertyNamesToIgnore.Contains(propertyName) || !HasProperty(objFr, propertyName))
                    {
                        continue;
                    }

                    MatchPropValue(objTo, objFr, propertyName);
                }

                return OpResult.Success();
            }
            catch (Exception e)
            {
                return OpResult.Fail(e.Message);
            }
        }
    }
}
