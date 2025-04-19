using System.IO;

namespace TestRunnerACAD
{
    /// <summary>
    ///     Utility class for managing file paths
    /// </summary>
    public class PathManager
    {
        // Constants for file paths
        public const string ReportToolFolderName = "ExtentReports";
        public const string ReportNunitXml = "Report-NUnit.xml";
        public const string ReportOutputHtml = "index.html";
        public const string ReportToolFileName = "ExtentReports.exe";

        /// <summary>
        ///     默认构造函数
        /// </summary>
        public PathManager()
        {
        }

        /// <summary>
        ///     获取程序集目录
        /// </summary>
        /// <returns>配置的输出路径</returns>
        public string GetAssemblyDirectory() =>
            // 使用ConfigReader获取输出路径
            ConfigReader.GetOutputPath();

        /// <summary>
        ///     获取报告目录路径
        /// </summary>
        /// <param name="createIfNotExists">如果目录不存在是否创建</param>
        /// <returns>报告目录的完整路径</returns>
        public string GetReportDirectory(bool createIfNotExists = true)
        {
            var pluginDir = GetAssemblyDirectory();
            if (string.IsNullOrEmpty(pluginDir))
            {
                return null;
            }

            var reportDir = Path.Combine(pluginDir, ReportToolFolderName);
            if (createIfNotExists && !Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            return reportDir;
        }

        /// <summary>
        ///     获取NUnit XML报告文件路径
        /// </summary>
        /// <returns>XML报告文件的完整路径</returns>
        public string GetNUnitXmlReportPath()
        {
            var reportDir = GetReportDirectory();
            if (string.IsNullOrEmpty(reportDir))
            {
                return null;
            }

            return Path.Combine(reportDir, ReportNunitXml);
        }

        /// <summary>
        ///     获取HTML报告文件路径
        /// </summary>
        /// <returns>HTML报告文件的完整路径</returns>
        public string GetHtmlReportPath()
        {
            var reportDir = GetReportDirectory();
            if (string.IsNullOrEmpty(reportDir))
            {
                return null;
            }

            return Path.Combine(reportDir, ReportOutputHtml);
        }

        /// <summary>
        ///     获取报告生成工具路径
        /// </summary>
        /// <returns>报告生成工具的完整路径</returns>
        public string GetReportGeneratorPath()
        {
            var pluginDir = GetAssemblyDirectory();
            if (string.IsNullOrEmpty(pluginDir))
            {
                return null;
            }

            return Path.Combine(pluginDir, ReportToolFolderName, ReportToolFileName);
        }
    }
}
