using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务实体部分，提供实体创建和属性管理功能
    /// </summary>
    public class TransactionServiceForEntity : ITransactionServiceForEntity
    {
        private readonly Dictionary<string, List<string>> _specialConstructor = new Dictionary<string, List<string>>
        {
            {
                CadServiceManager.StrLine,
                new List<string> { CadServiceManager.StrStartPoint, CadServiceManager.StrEndPoint }
            },
            {
                CadServiceManager.StrCircle,
                new List<string>
                    { CadServiceManager.StrCenter, CadServiceManager.StrNormal, CadServiceManager.StrRadius }
            },
            {
                CadServiceManager.StrAttributeDefinition,
                new List<string>
                {
                    CadServiceManager.StrPosition, CadServiceManager.StrTextString, CadServiceManager.StrTag,
                    CadServiceManager.StrPrompt, CadServiceManager.StrTextStyleId
                }
            }
        };

        private readonly List<string> _stylesToCheck = new List<string>
        {
            CadServiceManager.StrLayer, CadServiceManager.StrColorIndex, CadServiceManager.StrLinetype
        };

        private readonly TransactionService _transactionService;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        public TransactionServiceForEntity(TransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        ///     为实体添加自定义标识
        /// </summary>
        /// <param name="entity">要添加标识的实体</param>
        /// <param name="identityKey">标识键名</param>
        /// <param name="identityValue">标识值</param>
        /// <returns>是否添加成功</returns>
        public bool AddCustomIdentity(Entity entity, string identityKey, string identityValue)
        {
            try
            {
                if (entity == null || string.IsNullOrEmpty(identityKey))
                {
                    return false;
                }

                entity.XData = new ResultBuffer(
                    new TypedValue((int)DxfCode.ExtendedDataRegAppName, identityKey),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString, identityValue));

                return true;
            }
            catch (Exception ex)
            {
                Logger._.Error($"添加自定义标识失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     获取实体的自定义标识
        /// </summary>
        /// <param name="entity">要获取标识的实体</param>
        /// <param name="identityKey">标识键名</param>
        /// <returns>标识值，如不存在则返回null</returns>
        public string GetCustomIdentity(Entity entity, string identityKey)
        {
            try
            {
                if (entity == null || string.IsNullOrEmpty(identityKey))
                {
                    return null;
                }

                var rb = entity.XData;
                if (rb == null)
                {
                    return null;
                }

                var values = rb.AsArray();
                if (values == null || values.Length < 2)
                {
                    return null;
                }

                if (values[0].TypeCode == (int)DxfCode.ExtendedDataRegAppName &&
                    values[0].Value.ToString() == identityKey &&
                    values[1].TypeCode == (int)DxfCode.ExtendedDataAsciiString)
                {
                    return values[1].Value.ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取自定义标识失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     根据类型名称和属性字典创建实体
        /// </summary>
        /// <param name="typeName">实体类型名称</param>
        /// <param name="properties">属性字典</param>
        /// <returns>创建的实体对象，如果创建失败则返回null</returns>
        public Entity CreateEntityByTypeAndProperties(string typeName, Dictionary<string, object> properties)
        {
            try
            {
                // 在AutoCAD数据库服务命名空间中查找类型
                Type entityType = null;

                // 尝试在Autodesk.AutoCAD.DatabaseServices命名空间中查找
                entityType = typeof(Entity).Assembly.GetType($"Autodesk.AutoCAD.DatabaseServices.{typeName}");

                if (entityType == null)
                {
                    Logger._.Error($"找不到实体类型: {typeName}");
                    return null;
                }

                // 特殊处理几种需要特定构造参数的类型
                Entity entity = null;
                if (!_specialConstructor.TryGetValue(typeName, out var specialParams))
                {
                    entity = (Entity)Activator.CreateInstance(entityType);
                }
                else
                {
                    var paramValues = new List<object>();
                    foreach (var specialParam in specialParams)
                    {
                        if (!properties.TryGetValue(specialParam, out var paramValue))
                        {
                            Logger._.Error($"创建实体失败: {typeName} 缺少必要的参数{specialParam}");
                            return null;
                        }

                        paramValues.Add(paramValue);
                    }

                    entity = (Entity)ConstructorUtils.CreateWithParameters(entityType, paramValues);
                }

                if (entity == null)
                {
                    Logger._.Error($"无法创建实体类型: {typeName}");
                    return null;
                }

                // 使用反射设置属性
                foreach (var prop in properties)
                {
                    var propertyName = prop.Key;
                    var propertyValue = prop.Value;

                    // 跳过用于创建对象的特殊属性
                    if (propertyName == CadServiceManager.StrTypeName ||
                        (_specialConstructor.TryGetValue(typeName, out var constructorParams) &&
                         constructorParams.Contains(propertyName)))
                    {
                        continue;
                    }


                    switch (propertyName)
                    {
                        case CadServiceManager.StrLayer:
                            propertyValue = _transactionService.Style.GetValidLayerName((string)propertyValue);
                            break;
                        case CadServiceManager.StrColorIndex:
                            propertyValue = _transactionService.Style.GetValidColorIndex((short)propertyValue);
                            break;
                        case CadServiceManager.StrLinetype:
                            propertyValue = _transactionService.Style.GetValidLineTypeName((string)propertyValue);
                            break;
                    }

                    PropertyUtils.SetPropertyValue(entity, propertyName, propertyValue);
                }


                return entity;
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建实体失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     转换属性值为目标类型
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换后的值</returns>
        private object ConvertPropertyValue(object value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            if (targetType == typeof(short) || targetType == typeof(short))
            {
                return Convert.ToInt16(value);
            }

            if (targetType == typeof(int) || targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value);
            }

            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }

            if (targetType.IsEnum && value is int)
            {
                return Enum.ToObject(targetType, value);
            }

            if (targetType == typeof(LineWeight) && value is int)
            {
                return (LineWeight)(int)value;
            }

            // 其他类型转换...

            Logger._.Warn($"无法将 {value.GetType().Name} 转换为 {targetType.Name}");
            return null;
        }
    }
}
