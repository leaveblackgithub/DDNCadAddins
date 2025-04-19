using System;
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

        // 用于回退的默认路径
        private const string DEFAULT_OUTPUT_PATH = @"D:\leaveblackgithub\AutoCAD_UnitTest\bin\Debug";

        /// <summary>
        ///     从配置文件读取输出路径
        /// </summary>
        /// <returns>配置的输出路径，如果读取失败则返回默认路径</returns>
        public static string GetOutputPath()
        {
            try
            {
                // 配置文件路径 - 使用相对于程序运行目录的路径
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE_NAME);

                // 如果配置文件存在，直接读取
                if (File.Exists(configPath))
                {
                    try
                    {
                        // 创建配置映射
                        var configFileMap = new ExeConfigurationFileMap { ExeConfigFilename = configPath };
                        var config =
                            ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);

                        if (config.AppSettings.Settings["OutputPath"] != null)
                        {
                            return config.AppSettings.Settings["OutputPath"].Value;
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
                        return appSettings["OutputPath"];
                    }
                }
                catch
                {
                    /* 忽略错误 */
                }

                // 如果无法读取配置，返回默认路径
                return DEFAULT_OUTPUT_PATH;
            }
            catch (Exception)
            {
                // 出现异常时返回默认路径
                return DEFAULT_OUTPUT_PATH;
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
                // 配置文件路径
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE_NAME);

                // 如果配置文件存在，读取
                if (File.Exists(configPath))
                {
                    try
                    {
                        // 创建配置映射
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
