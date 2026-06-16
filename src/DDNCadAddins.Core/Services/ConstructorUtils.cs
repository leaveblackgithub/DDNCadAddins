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
                        var paramValue = paramValues[i];

                        if (paramValue == null)
                        {
                            var canAcceptNull = !paramType.IsValueType ||
                                (paramType.IsGenericType &&
                                 paramType.GetGenericTypeDefinition() == typeof(Nullable<>));
                            if (!canAcceptNull)
                            {
                                typeMatch = false;
                                break;
                            }

                            continue;
                        }

                        var valueType = paramValue.GetType();
                        if (!PropertyUtils.CanBeConvertedFrom(paramType, valueType))
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
                    return null;
                }

                var obj = Activator.CreateInstance(objType, paramValues.ToArray());
                return obj;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
