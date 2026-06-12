using System;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Services;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.HelloWorldCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     HelloWorld 命令 - 验证 Core 层可从 CAD 命令中调用
    ///     命令名: HELLO
    /// </summary>
    public class HelloWorldCommand
    {
        /// <summary>
        ///     CAD 命令入口，调用 Core 层纯逻辑服务
        /// </summary>
        [CommandMethod("HELLO", CommandFlags.Modal)]
        public void HelloWorld()
        {
            try
            {
                // Core 层服务实例化（无 CAD 依赖）
                var calculator = new CalculatorService();

                // 调用纯业务逻辑
                var addResult = calculator.Add(10.0, 20.0);
                var subResult = calculator.Subtract(100.0, 37.5);

                var ed = CadServiceManager.ServiceEd;
                ed.WriteMessage("\n=== HelloWorld: Core 层验证 ===");

                if (addResult.IsSuccess)
                    ed.WriteMessage($"\n{addResult.Message}");
                else
                    ed.WriteMessage($"\n加法失败: {addResult.Message}");

                if (subResult.IsSuccess)
                    ed.WriteMessage($"\n{subResult.Message}");
                else
                    ed.WriteMessage($"\n减法失败: {subResult.Message}");

                ed.WriteMessage("\n Core 层调用成功，分层架构验证通过！");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"HelloWorldCommand 执行失败: {ex.Message}");
                CadServiceManager.ServiceEd.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
