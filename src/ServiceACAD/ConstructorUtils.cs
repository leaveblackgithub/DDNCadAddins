using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceACAD
{
    public static class ConstructorUtils
    {
        public static object CreateWithParameters(Type objType, IList<object> paramValues)
        {
            try
            {
                var typeName = objType.Name;

                // 验证构造函数参数类型和数量
                var constructors = objType.GetConstructors();
                var validConstructor = false;
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    if (parameters.Length != paramValues.Count)
                    {
                        continue;
                    }

                    var typeMatch = true;
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        var valueType = paramValues[i]?.GetType();

                        if (valueType == null || !PropertyUtils.CanBeConvertedFrom(paramType, valueType))
                        {
                            typeMatch = false;
                            break;
                        }
                    }

                    if (typeMatch)
                    {
                        validConstructor = true;
                        break;
                    }
                }

                if (!validConstructor)
                {
                    Logger._.Error($"创建实例失败: {typeName} 找不到匹配的构造函数，参数类型不兼容");
                    return null;
                }

                var obj = Activator.CreateInstance(objType, paramValues.ToArray());
                if (obj != null)
                {
                    return obj;
                }

                Logger._.Error($"无法创建实例: {typeName}");
                return null;
            }
            catch (Exception e)
            {
                Logger._.Error($"创建实例失败: {e.Message}");
                return null;
            }
        }
    }
}
