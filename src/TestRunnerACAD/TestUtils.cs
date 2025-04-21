using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using NUnit.Framework;
using NUnitLite;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TestRunnerACAD
{
    public static class TestUtils
    {
        public static void Run(Assembly testAssembly,string include="", string exclude = "")
        {
            // 创建ReportGenerator实例
            var reportGenerator = new ReportGenerator();

            var xmlReportPath = reportGenerator.NunitXmlPath;

            reportGenerator.CleanNunitXml();

            // 设置NUnit参数，包括输出XML结果
            var nunitArgs = new List<string>
            {
                "--trace=verbose", "--result=" + xmlReportPath
            };
            if(!string.IsNullOrEmpty(include))
            {
                nunitArgs.Add($"--where=namespace=='{include}'");
            }
            // 运行测试
            if (!string.IsNullOrEmpty(exclude))
            {
                nunitArgs.Add($"--where=namespace!~'{exclude}'");
            }

            new AutoRun(testAssembly).Execute(nunitArgs.ToArray());


            // 生成HTML测试报告
            //The extentreports-dotnet-cli deprecates ReportUnit. Can only define output folder and export to default index.html
            reportGenerator.CreateHtmlReport();
            reportGenerator.OpenHtmlReport();
        }
        
    }
}
