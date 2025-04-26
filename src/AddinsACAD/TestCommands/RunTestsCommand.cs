using System.Reflection;
using AddinsACAD.TestCommands;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using TestRunnerACAD;

[assembly: CommandClass(typeof(RunTestsCommand))]

namespace AddinsACAD.TestCommands
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
            // 在App环境中，运行所有测试，排除BlockServiceTests类中的测试
            TestUtils.Run(assembly, "", "AddinsACAD.ServiceTests.BlockServiceTests"); // 排除BlockServiceTests
            CadServiceManager.instance.Dispose();
        }
    }
}
