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
        ///     Executes a list of delegate actions
        /// </summary>
        /// <param name="drawingFile">Path to the test drawing file.</param>
        /// <param name="testActions">Test actions to execute.</param>
        public static void ExcecuteInCl(string drawingFile = "",
            params Action<Database, Transaction>[] testActions)
        {
            bool defaultDrawing;
            if (string.IsNullOrEmpty(drawingFile))
            {
                defaultDrawing = true;
                // Should this be executing assembly path instead?
                drawingFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDrawing.dwg");
            }
            else
            {
                defaultDrawing = false;
                if (!File.Exists(drawingFile))
                {
                    Assert.Fail($"Drawing file {drawingFile} does not exist.");
                }
            }

            var document = Application.DocumentManager.MdiActiveDocument;

            // Lock the document and execute the test actions.
            using (document.LockDocument())
            using (var db = new Database(defaultDrawing, false))
            {
                if (!string.IsNullOrEmpty(drawingFile))
                {
                    db.ReadDwgFile(drawingFile, FileOpenMode.OpenForReadAndWriteNoShare, true, null);
                }

                var oldDb = HostApplicationServices.WorkingDatabase;
                HostApplicationServices.WorkingDatabase = db; // change to the current database.


                ExecuteActions(testActions, db);

                // Change the database back to the original.
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        /// <summary>
        ///     执行测试动作 - 根据编译环境自动选择合适的执行方法
        /// </summary>
        /// <param name="testActions">要执行的测试动作数组</param>
        /// <param name="drawingFile">可选的图形文件路径，仅用于AcCoreConsole环境</param>
        public static void ExecuteDbActions(string drawingFile = "", params Action<Database, Transaction>[] testActions)
        {
#if IN_ACCORE
            // 在AcCoreConsole环境下运行
            ExcecuteInCl(drawingFile, testActions);
#else
            ExecuteInApp(testActions);
#endif
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
