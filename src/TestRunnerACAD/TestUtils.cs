using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using NUnitLite;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using NUnit.Framework;

namespace TestRunnerACAD
{
    public static class TestUtils
    {
        public static void Run(Assembly testAssembly, string excludeNameSpace = "")
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

            // 运行测试
            if (!string.IsNullOrEmpty(excludeNameSpace))
            {
                nunitArgs.Add($"--where=namespace!~'{excludeNameSpace}'");
            }

            new AutoRun(testAssembly).Execute(nunitArgs.ToArray());


            // 生成HTML测试报告
            //The extentreports-dotnet-cli deprecates ReportUnit. Can only define output folder and export to default index.html
            reportGenerator.CreateHtmlReport();
            reportGenerator.OpenHtmlReport();
        }

        
          

        /// <summary>
        ///     执行测试动作 - 自动处理所有环境
        /// </summary>
        /// <param name="testActions">要执行的测试动作数组</param>
        /// <param name="drawingFile">可选的图形文件路径</param>
        public static void ExecuteDbActions(string drawingFile = "", params Action<Database, Transaction>[] testActions)
        {
                // 在App环境中运行
                ExecuteInApp(testActions);
        }

        public static void ExecuteInApp(Action<Database, Transaction>[] testActions)
        {
            var document = Application.DocumentManager.MdiActiveDocument;

            // Lock the document and execute the test actions.
            using (document.LockDocument())
            using (var db = document.Database)
            {
                ExecuteActions(testActions, db);
            }
        }

        private static void ExecuteActions(Action<Database, Transaction>[] testActions, Database db)
        {
            foreach (var testAction in testActions)
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Execute the test action.
                        testAction(db, tr);
                    }
                    catch (Exception e)
                    {
                        tr.Commit();
                        MessageBox.Show(e.ToString());
                        break;
                    }

                    tr.Commit();
                }
            }
        }
    }
}
