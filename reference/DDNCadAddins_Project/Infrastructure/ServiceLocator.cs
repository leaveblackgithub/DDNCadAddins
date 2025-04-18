using System;
using System.Collections.Generic;
using DDNCadAddins.Services;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    ///     服务定位器 - 提供依赖注入功能的轻量级容器.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, Func<object>> ServiceFactories = new Dictionary<Type, Func<object>>();
        private static readonly Dictionary<Type, object> SingletonInstances = new Dictionary<Type, object>();
        private static bool isInitialized;
        private static readonly object LockObject = new object();

        /// <summary>
        ///     初始化服务定位器.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            lock (LockObject)
            {
                if (isInitialized)
                {
                    return;
                }

                RegisterServices();
                isInitialized = true;
            }
        }

        /// <summary>
        ///     注册所有默认服务.
        /// </summary>
        private static void RegisterServices()
        {
            // 注册日志服务
            RegisterSingleton<ILogger, FileLogger>();

            // 注册消息服务
            RegisterSingleton<IUserMessageService>(() => new AcadUserMessageService(GetService<ILogger>()));

            // 注册各种AutoCAD服务
            RegisterSingleton<IDocumentService>(() => new DocumentService(GetService<ILogger>()));
            RegisterSingleton<ITransactionService>(() => new TransactionService(GetService<ILogger>()));
            RegisterSingleton<IBlockReferenceService>(() =>
                new BlockReferenceService(GetService<ILogger>(), GetService<ITransactionService>()));

            // 注册AcadService
            RegisterSingleton<IAcadService>(() => new AcadService(
                GetService<ILogger>(),
                GetService<IDocumentService>(),
                GetService<ITransactionService>(),
                GetService<IBlockReferenceService>()));

            // 注册其他服务
            RegisterSingleton<IXClipBlockService>(() =>
                new XClipBlockService(GetService<IAcadService>(), GetService<ILogger>()));

            RegisterSingleton<IViewService>(() =>
                new AcadViewService(GetService<IUserMessageService>(), GetService<ILogger>()));
        }

        /// <summary>
        ///     注册单例服务.
        /// </summary>
        /// <typeparam name="TService">服务接口类型.</typeparam>
        /// <typeparam name="TImplementation">服务实现类型.</typeparam>
        public static void RegisterSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService, new()
        {
            RegisterSingleton<TService>(() => new TImplementation());
        }

        /// <summary>
        ///     使用工厂函数注册单例服务.
        /// </summary>
        /// <typeparam name="TService">服务接口类型.</typeparam>
        /// <param name="factory">创建服务实例的工厂函数.</param>
        public static void RegisterSingleton<TService>(Func<TService> factory)
            where TService : class
        {
            ServiceFactories[typeof(TService)] = () =>
            {
                Type serviceType = typeof(TService);
                if (!SingletonInstances.TryGetValue(serviceType, out object instance))
                {
                    instance = factory();
                    SingletonInstances[serviceType] = instance;
                }

                return instance;
            };
        }

        /// <summary>
        ///     注册瞬态服务.
        /// </summary>
        /// <typeparam name="TService">服务接口类型.</typeparam>
        /// <typeparam name="TImplementation">服务实现类型.</typeparam>
        public static void RegisterTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService, new()
        {
            RegisterTransient<TService>(() => new TImplementation());
        }

        /// <summary>
        ///     使用工厂函数注册瞬态服务.
        /// </summary>
        /// <typeparam name="TService">服务接口类型.</typeparam>
        /// <param name="factory">创建服务实例的工厂函数.</param>
        public static void RegisterTransient<TService>(Func<TService> factory)
            where TService : class
        {
            ServiceFactories[typeof(TService)] = () => factory();
        }

        /// <summary>
        ///     获取服务实例.
        /// </summary>
        /// <typeparam name="T">服务类型.</typeparam>
        /// <returns>服务实例.</returns>
        public static T GetService<T>()
            where T : class
        {
            if (!isInitialized)
            {
                Initialize();
            }

            Type serviceType = typeof(T);
            return ServiceFactories.TryGetValue(serviceType, out Func<object> factory)
                ? (T)factory()
                : throw new InvalidOperationException($"未找到类型为 {serviceType.Name} 的服务");
        }

        /// <summary>
        ///     创建命令实例.
        /// </summary>
        /// <typeparam name="T">命令类型.</typeparam>
        /// <returns>命令实例.</returns>
        public static T CreateCommand<T>()
            where T : class
        {
            // 通过反射创建实例并注入依赖
            Type type = typeof(T);
            System.Reflection.ConstructorInfo[] constructors = type.GetConstructors();

            foreach (System.Reflection.ConstructorInfo constructor in constructors)
            {
                // 尝试找到接受服务参数的构造函数
                System.Reflection.ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length > 0)
                {
                    object[] paramValues = new object[parameters.Length];
                    bool canResolve = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Type paramType = parameters[i].ParameterType;
                        try
                        {
                            // 使用反射调用泛型GetService
                            System.Reflection.MethodInfo method = typeof(ServiceLocator).GetMethod("GetService")
                                .MakeGenericMethod(paramType);
                            paramValues[i] = method.Invoke(null, null);
                        }
                        catch
                        {
                            canResolve = false;
                            break;
                        }
                    }

                    if (canResolve)
                    {
                        return (T)constructor.Invoke(paramValues);
                    }
                }
            }

            // 如果没有找到合适的构造函数，尝试使用无参构造函数
            return Activator.CreateInstance<T>();
        }

        /// <summary>
        ///     清除所有注册的服务和实例（主要用于测试）.
        /// </summary>
        public static void Reset()
        {
            lock (LockObject)
            {
                ServiceFactories.Clear();
                SingletonInstances.Clear();
                isInitialized = false;
            }
        }
    }
}
