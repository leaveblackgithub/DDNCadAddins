using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using NUnitLite;
using ServiceACAD;
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

        public static void ExecuteInTransactions(string drawingTitle="",params Action<IDocumentService, Transaction>[] testActions)
        {
            //var cadDoc = Application.DocumentManager.MdiActiveDocument;
            var serviceDoc = CadServiceManager._.ActiveServiceDoc;//new DocumentService(cadDoc);
                serviceDoc.ExecuteInTransactions(drawingTitle,testActions);
        }

        public static void ExecuteInDoc(Action<IDocumentService> testAction, string drawingTitle = "")
        {
            // var cadDoc = Application.DocumentManager.MdiActiveDocument;

            var serviceDoc = CadServiceManager._.ActiveServiceDoc;//new DocumentService(cadDoc);
                serviceDoc.ExecuteInDoc(testAction,drawingTitle);
            
        }
        // public static void ExecuteInApp(Action<Database, Transaction> testAction1,
        //     params Action<Database, Transaction>[] OtherTestActions)
        // {
        //     var testActions = new List<Action<Database, Transaction>> { testAction1 };
        //     if (OtherTestActions.Length > 0)
        //     {
        //         testActions.AddRange(OtherTestActions);
        //     }
        //
        //     var document = Application.DocumentManager.MdiActiveDocument;
        //
        //     // Lock the document and execute the test actions.
        //     using (document.LockDocument())
        //     using (var db = document.Database)
        //     {
        //         foreach (var testAction in (ICollection<Action<Database, Transaction>>)testActions)
        //         {
        //             using (var tr = db.TransactionManager.StartTransaction())
        //             {
        //                 try
        //                 {
        //                     // Execute the test action.
        //                     testAction(db, tr);
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     tr.Commit();
        //                     MessageBox.Show(e.ToString());
        //                     break;
        //                 }
        //
        //                 tr.Commit();
        //             }
        //         }
        //     }
        // }
    }
}
