using System.Diagnostics;
using System.IO;

namespace TestRunnerACAD
{
    /// <summary>
    ///     Utility class for generating and opening HTML reports from NUnit XML results
    /// </summary>
    public class ReportGenerator
    {
        private readonly PathManager _pathManager;

        /// <summary>
        ///     Initializes a new instance of the ReportGenerator class
        /// </summary>
        public ReportGenerator()
        {
            _pathManager = new PathManager();
        }

        public string GeneratorPath => _pathManager.GetReportGeneratorPath();
        public string ReportDir => _pathManager.GetReportDirectory();
        public string HtmlReportPath => _pathManager.GetHtmlReportPath();
        public string NunitXmlPath => _pathManager.GetNUnitXmlReportPath();

        /// <summary>
        ///     Opens a HTML report with the default viewer.
        /// </summary>
        public void OpenHtmlReport()
        {
            if (!File.Exists(HtmlReportPath))
            {
                return;
            }

            using (var process = new Process())
            {
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.FileName = HtmlReportPath;
                process.Start();
            }
        }

        /// <summary>
        ///     Creates a HTML report based on the NUnit XML report.
        /// </summary>
        public void CreateHtmlReport()
        {
            if (!File.Exists(NunitXmlPath))
            {
                return;
            }

            InitReportDir();

            CleanupHtmlReport();

            using (var process = new Process())
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = GeneratorPath;
                //extent -i results/nunit.xml -o results/ -r v3html
                process.StartInfo.Arguments = $" \"-i\" \"{NunitXmlPath}\" \"-o\" \"{ReportDir}\" \"-r\" \"v3html\"";

                process.Start();
                process.WaitForExit();
            }
        }

        public void CleanupHtmlReport()
        {
            if (File.Exists(HtmlReportPath))
            {
                File.Delete(HtmlReportPath);
            }
        }

        public void InitReportDir()
        {
            if (!Directory.Exists(ReportDir))
            {
                Directory.CreateDirectory(ReportDir);
            }
        }

        public void CleanNunitXml()
        {
            // 删除现有的测试报告文件
            if (File.Exists(NunitXmlPath))
            {
                File.Delete(NunitXmlPath);
            }
        }
    }
}
