using System;
using System.ComponentModel;
using System.Linq;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     属性类型转换判断工具 - 纯逻辑，无 CAD 依赖
    /// </summary>
    public static class PropertyConversionUtils
    {
        /// <summary>
        ///     检查前者类型的属性是否能接受后者类型的赋值
        /// </summary>
        /// <param name="targetType">目标属性的类型</param>
        /// <param name="sourceType">源数据的类型</param>
        /// <returns>如果可以转换/赋值返回 true，否则返回 false</returns>
        public static bool CanBeConvertedFrom(Type targetType, Type sourceType)
        {
            try
            {
                if (targetType == null || sourceType == null)
                {
                    return false;
                }

                if (targetType == sourceType)
                {
                    return true;
                }

                if (sourceType == typeof(DBNull) ||
                    (sourceType.IsValueType == false && targetType.IsValueType == false))
                {
                    return true;
                }

                if (IsNumericType(targetType) && IsNumericType(sourceType))
                {
                    return GetNumericTypeRank(targetType) >= GetNumericTypeRank(sourceType);
                }

                if (targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var underlyingType = Nullable.GetUnderlyingType(targetType);
                    if (underlyingType == sourceType)
                    {
                        return true;
                    }

                    if (IsNumericType(underlyingType) && IsNumericType(sourceType))
                    {
                        return GetNumericTypeRank(underlyingType) >= GetNumericTypeRank(sourceType);
                    }
                }

                if (targetType.IsAssignableFrom(sourceType))
                {
                    return true;
                }

                if (targetType.IsInterface && sourceType.GetInterfaces().Contains(targetType))
                {
                    return true;
                }

                try
                {
                    var converter = TypeDescriptor.GetConverter(sourceType);
                    if (converter.CanConvertTo(targetType))
                    {
                        return true;
                    }

                    converter = TypeDescriptor.GetConverter(targetType);
                    if (converter.CanConvertFrom(sourceType))
                    {
                        return true;
                    }
                }
                catch
                {
                    // 转换器检查失败，忽略异常
                }

                if (targetType == typeof(Guid) && sourceType == typeof(string))
                {
                    return true;
                }

                if (targetType.IsEnum && sourceType == typeof(string))
                {
                    return true;
                }

                if (targetType.IsEnum && sourceType == typeof(int))
                {
                    return true;
                }

                if (IsNumericType(targetType) && sourceType == typeof(string))
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     检查类型是否为数值类型
        /// </summary>
        /// <param name="type">要检查的类型</param>
        /// <returns>如果是数值类型返回 true，否则返回 false</returns>
        public static bool IsNumericType(Type type)
        {
            if (type == null)
            {
                return false;
            }

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
        public static int GetNumericTypeRank(Type type)
        {
            if (type == null)
            {
                return 0;
            }

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
    }
}
