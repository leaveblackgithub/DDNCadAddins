using System.Reflection;
using AddinsACAD;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using TestRunnerACAD;

[assembly: CommandClass(typeof(RunTestsCommand))]

namespace AddinsACAD
{
    public class RunTestsCommand
    {
        [CommandMethod("RunTests", CommandFlags.Session)]
        public void RunTests()
        {
            // var document = Application.DocumentManager.MdiActiveDocument;
            // var documentService = new DocumentService(document);
            // documentService.CadEd.WriteMessage("\nTest");
            var assembly = Assembly.GetExecutingAssembly();
            // 在App环境中，运行所有测试
            TestUtils.Run(assembly); //,"AddinsACAD.ServiceTests");
            CadServiceManager.instance.Dispose();
        }
    }
}
