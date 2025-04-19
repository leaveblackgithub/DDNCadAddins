using System.Reflection;
using AddinsACAD;
using Autodesk.AutoCAD.Runtime;
using TestRunnerACAD;

[assembly: CommandClass(typeof(RunTestsCommand))]

namespace AddinsACAD
{
    public class RunTestsCommand
    {
        [CommandMethod("RunTests", CommandFlags.Session)]
        public void RunTests()
        {
            var assembly = Assembly.GetExecutingAssembly();
#if IN_ACCORE
            TestUtils.Run(assembly, "AddinsACAD.TestInApp");
#else
            TestUtils.Run(assembly);
#endif
        }
    }
}
