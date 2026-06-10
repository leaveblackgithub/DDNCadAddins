using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace TestRunnerACAD
{
    /// <summary>
    ///     配置文件读取器，负责从配置文件中读取设置
    /// </summary>
    public class ConfigReader
    {
        // 配置文件名
        private const string CONFIG_FILE_NAME = "paths.config";
        private const string HOTLOAD_PLUGIN_DIR_KEY = "HOTLOAD_PLUGIN_DIR";
        private const string HOTLOAD_ASSEMBLY_PATHS_KEY = "HOTLOAD_ASSEMBLY_PATHS";

        /// <summary>
        ///     从配置文件读取输出路径
        /// </summary>
        /// <returns>配置的输出路径，如果读取失败则返回插件程序集所在目录</returns>
        public static string GetOutputPath()
        {
            try
            {
                foreach (var searchDir in GetConfigSearchDirectories())
                {
                    var configPath = Path.Combine(searchDir, CONFIG_FILE_NAME);
                    if (!File.Exists(configPath))
                    {
                        continue;
                    }

                    try
                    {
                        var configFileMap = new ExeConfigurationFileMap { ExeConfigFilename = configPath };
                        var config =
                            ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);

                        if (config.AppSettings.Settings["OutputPath"] != null)
                        {
                            var configuredPath = config.AppSettings.Settings["OutputPath"].Value;
                            if (!string.IsNullOrWhiteSpace(configuredPath))
                            {
                                return configuredPath;
                            }
                        }
                    }
                    catch
                    {
                        /* 忽略配置文件读取错误 */
                    }
                }

                // 尝试加载应用程序配置
                try
                {
                    var appSettings = ConfigurationManager.AppSettings;
                    if (appSettings["OutputPath"] != null)
                    {
                        var configuredPath = appSettings["OutputPath"];
                        if (!string.IsNullOrWhiteSpace(configuredPath))
                        {
                            return configuredPath;
                        }
                    }
                }
                catch
                {
                    /* 忽略错误 */
                }

                return GetDefaultOutputPath();
            }
            catch (Exception)
            {
                return GetDefaultOutputPath();
            }
        }

        /// <summary>
        ///     获取默认输出路径（当前插件程序集所在目录）
        /// </summary>
        /// <returns>插件程序集目录，无法解析时返回应用程序基目录</returns>
        private static string GetDefaultOutputPath()
        {
            try
            {
                var assemblyDir = GetAssemblyDirectory(typeof(ConfigReader));
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    return assemblyDir;
                }
            }
            catch
            {
                /* 忽略错误 */
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        ///     获取配置文件搜索目录列表
        /// </summary>
        /// <returns>按优先级排序的目录列表</returns>
        private static string[] GetConfigSearchDirectories()
        {
            var assemblyDir = GetAssemblyDirectory(typeof(ConfigReader));
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            if (string.IsNullOrEmpty(assemblyDir))
            {
                return new[] { baseDir };
            }

            if (string.Equals(assemblyDir.TrimEnd('\\'), baseDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                return new[] { baseDir };
            }

            return new[] { assemblyDir, baseDir };
        }

        /// <summary>
        ///     获取指定类型所在程序集的目录
        /// </summary>
        /// <param name="typeInAssembly">程序集中的类型</param>
        /// <returns>程序集目录，无法解析时返回 null</returns>
        private static string GetAssemblyDirectory(Type typeInAssembly)
        {
            try
            {
                var paths = AppDomain.CurrentDomain.GetData(HOTLOAD_ASSEMBLY_PATHS_KEY) as Dictionary<string, string>;
                if (paths != null &&
                    paths.TryGetValue(typeInAssembly.Assembly.FullName, out var dllPath) &&
                    !string.IsNullOrEmpty(dllPath))
                {
                    return Path.GetDirectoryName(dllPath);
                }

                var pluginDir = AppDomain.CurrentDomain.GetData(HOTLOAD_PLUGIN_DIR_KEY) as string;
                if (!string.IsNullOrEmpty(pluginDir))
                {
                    return pluginDir;
                }

                var location = typeInAssembly.Assembly.Location;
                if (string.IsNullOrEmpty(location))
                {
                    return null;
                }

                return Path.GetDirectoryName(location);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     读取任意配置键的值
        /// </summary>
        /// <param name="key">配置键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值或默认值</returns>
        public static string GetConfigValue(string key, string defaultValue)
        {
            try
            {
                foreach (var searchDir in GetConfigSearchDirectories())
                {
                    var configPath = Path.Combine(searchDir, CONFIG_FILE_NAME);
                    if (!File.Exists(configPath))
                    {
                        continue;
                    }

                    try
                    {
                        var configFileMap = new ExeConfigurationFileMap { ExeConfigFilename = configPath };
                        var config =
                            ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);

                        if (config.AppSettings.Settings[key] != null)
                        {
                            return config.AppSettings.Settings[key].Value;
                        }
                    }
                    catch
                    {
                        /* 忽略配置文件读取错误 */
                    }
                }

                // 尝试应用程序配置
                try
                {
                    var appSettings = ConfigurationManager.AppSettings;
                    if (appSettings[key] != null)
                    {
                        return appSettings[key];
                    }
                }
                catch
                {
                    /* 忽略错误 */
                }
            }
            catch
            {
                /* 忽略所有错误 */
            }

            // 返回默认值
            return defaultValue;
        }
    }
}
