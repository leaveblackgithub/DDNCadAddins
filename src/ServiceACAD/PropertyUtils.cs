using System;
using System.ComponentModel;
using System.Linq;

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
        /// <param name="propertyPropertyType">目标属性的类型</param>
        /// <param name="getType">源数据的类型</param>
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
        public static bool CanBeConvertedFrom(Type propertyPropertyType, Type getType)
        {
            if (propertyPropertyType == null || getType == null)
            {
                return false;
            }

            // 相同类型可直接赋值
            if (propertyPropertyType == getType)
            {
                return true;
            }

            // 检查null值 - 引用类型属性可以接受null
            if (getType == typeof(DBNull) ||
                (getType.IsValueType == false && propertyPropertyType.IsValueType == false))
            {
                return true;
            }

            // 检查数值类型的隐式转换
            if (IsNumericType(propertyPropertyType) && IsNumericType(getType))
            {
                // 数值类型的扩展转换规则
                // 小范围类型可以隐式转换为大范围类型
                if (GetNumericTypeRank(propertyPropertyType) >= GetNumericTypeRank(getType))
                {
                    return true;
                }
            }

            // 检查可空类型
            if (propertyPropertyType.IsGenericType &&
                propertyPropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // 可空类型的底层类型
                var underlyingType = Nullable.GetUnderlyingType(propertyPropertyType);
                // 如果值类型与可空类型的底层类型匹配，可以赋值
                if (underlyingType == getType)
                {
                    return true;
                }

                // 检查可空数值类型的隐式转换
                if (IsNumericType(underlyingType) && IsNumericType(getType))
                {
                    if (GetNumericTypeRank(underlyingType) >= GetNumericTypeRank(getType))
                    {
                        return true;
                    }
                }
            }

            // 检查继承关系（子类可以赋值给父类）
            if (propertyPropertyType.IsAssignableFrom(getType))
            {
                return true;
            }

            // 检查接口实现
            if (propertyPropertyType.IsInterface && getType.GetInterfaces().Contains(propertyPropertyType))
            {
                return true;
            }

            // 检查是否存在类型转换器或隐式转换操作符
            try
            {
                var converter = TypeDescriptor.GetConverter(getType);
                if (converter.CanConvertTo(propertyPropertyType))
                {
                    return true;
                }

                converter = TypeDescriptor.GetConverter(propertyPropertyType);
                if (converter.CanConvertFrom(getType))
                {
                    return true;
                }
            }
            catch
            {
                // 转换器检查失败，忽略异常
            }

            // 检查常见的特殊转换情况
            // string -> Guid
            if (propertyPropertyType == typeof(Guid) && getType == typeof(string))
            {
                return true;
            }

            // string -> enum
            if (propertyPropertyType.IsEnum && getType == typeof(string))
            {
                return true;
            }

            // int -> enum
            if (propertyPropertyType.IsEnum && getType == typeof(int))
            {
                return true;
            }

            // string -> 数值类型
            if (IsNumericType(propertyPropertyType) && getType == typeof(string))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     检查类型是否为数值类型
        /// </summary>
        /// <param name="type">要检查的类型</param>
        /// <returns>如果是数值类型返回true，否则返回false</returns>
        private static bool IsNumericType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            // 处理可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return IsNumericType(Nullable.GetUnderlyingType(type));
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     获取数值类型的排序级别，用于判断隐式转换的可行性
        /// </summary>
        /// <param name="type">数值类型</param>
        /// <returns>排序级别，数值越大表示范围越大</returns>
        private static int GetNumericTypeRank(Type type)
        {
            // 处理可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return GetNumericTypeRank(Nullable.GetUnderlyingType(type));
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: return 1;
                case TypeCode.SByte: return 2;
                case TypeCode.Int16: return 3;
                case TypeCode.UInt16: return 4;
                case TypeCode.Int32: return 5;
                case TypeCode.UInt32: return 6;
                case TypeCode.Int64: return 7;
                case TypeCode.UInt64: return 8;
                case TypeCode.Single: return 9;
                case TypeCode.Double: return 10;
                case TypeCode.Decimal: return 11;
                default: return 0;
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
                if (entToFilter != null || entToFilter(objTo, objFr))
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
