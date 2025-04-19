using System.Reflection;
using AddinsACAD;
using Autodesk.AutoCAD.Runtime;
using TestRunnerACAD;
using Autodesk.AutoCAD.ApplicationServices.Core;

[assembly: CommandClass(typeof(RunTestsCommand))]

namespace AddinsACAD
{
    public class RunTestsCommand
    {
        [CommandMethod("RunTests", CommandFlags.Session)]
        public void RunTests()
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // 合并的代码：根据运行环境决定是否排除测试
            if (Application.DocumentManager.MdiActiveDocument == null)
            {
                // 在Console环境中，排除App环境专用测试
                TestUtils.Run(assembly, "AddinsACAD.TestInApp");
            }
            else
            {
                // 在App环境中，运行所有测试
                TestUtils.Run(assembly);
            }
        }
    }
}
