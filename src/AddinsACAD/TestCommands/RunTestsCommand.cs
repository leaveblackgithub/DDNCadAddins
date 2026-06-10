using System;
using System.IO;
using System.Reflection;
using AddinsACAD.TestCommands;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using TestRunnerACAD;
using AcadException = Autodesk.AutoCAD.Runtime.Exception;

[assembly: CommandClass(typeof(RunTestsCommand))]

namespace AddinsACAD.TestCommands
{
    /// <summary>
    ///     在 AutoCAD 中运行 NUnit 测试并生成 HTML 报告
    /// </summary>
    public class RunTestsCommand
    {
        private const string RequiredDrawingTitle = "xclip";
        private const string XclipDrawingRequirements =
            "xclip.dwg 要求: 模型空间含名为 23432 的图块参照（至少 6 个），且其中至少 1 个带 XClip。";

        /// <summary>
        ///     运行插件测试套件（命令名：RUNTESTS）
        /// </summary>
        [CommandMethod("RunTests", CommandFlags.Session)]
        public void RunTests()
        {
            try
            {
                WriteUsageMessage();
                WriteDrawingHint();

                var assembly = Assembly.GetExecutingAssembly();
                // 排除 BlockServiceTests（与扩展测试重复，且同样依赖 xclip.dwg）
                TestUtils.Run(assembly, "", "AddinsACAD.ServiceTests.BlockServiceTests");

                CadServiceManager.ServiceEd.WriteMessage("\n测试完成，正在打开 HTML 报告...");
            }
            catch (AcadException acadException)
            {
                Logger._.Error("RUNTESTS AutoCAD API 错误（说明输出、测试执行或报告打开）", acadException);
                TryWriteCommandMessage($"AutoCAD 错误: {acadException.Message}");
            }
            catch (System.Exception systemException)
            {
                Logger._.Error("RUNTESTS 系统错误（NUnit、文件 IO、进程启动等）", systemException);
                TryWriteCommandMessage($"运行测试失败: {systemException.Message}");
            }
            finally
            {
                CadServiceManager.instance.Dispose();
            }
        }

        /// <summary>
        ///     输出 RUNTESTS 命令用法说明
        /// </summary>
        private static void WriteUsageMessage()
        {
            CadServiceManager.ServiceEd.WriteMessage(
                "\nRUNTESTS - 运行插件单元测试" +
                "\n用法: NETLOAD 加载 AddinsACAD.dll 后执行 RUNTESTS" +
                "\n说明:" +
                "\n  1. 大部分测试可在任意图纸下运行" +
                $"\n  2. 部分测试要求当前活动图纸文件名为 {RequiredDrawingTitle}（打开 {RequiredDrawingTitle}.dwg）" +
                "\n  3. 未满足图纸要求时，相关用例将显示为 Skipped，不会记为失败" +
                "\n  4. 完成后自动打开 bin\\Debug\\ExtentReports\\index.html");
        }

        /// <summary>
        ///     根据当前图纸提示是否会跳过 xclip 相关测试
        /// </summary>
        private static void WriteDrawingHint()
        {
            try
            {
                var docService = CadServiceManager._;
                if (docService == null)
                {
                    CadServiceManager.ServiceEd.WriteMessage(
                        $"\n提示: 当前无活动图纸，依赖 {RequiredDrawingTitle}.dwg 的测试将被跳过。");
                    return;
                }

                var drawingName = Path.GetFileNameWithoutExtension(docService.DrawingFullPath);
                if (string.Equals(drawingName, RequiredDrawingTitle, StringComparison.CurrentCultureIgnoreCase))
                {
                    CadServiceManager.ServiceEd.WriteMessage(
                        $"\n当前图纸: {drawingName}.dwg（可运行全部测试）");
                    return;
                }

                CadServiceManager.ServiceEd.WriteMessage(
                    $"\n当前图纸: {drawingName}（非 {RequiredDrawingTitle}，以下测试将跳过）:" +
                    "\n  - BlockServiceExtendedTests.TestIsXclipped_XclippedBlock_ReturnsTrue" +
                    "\n  - BlockServiceExtendedTests.TestGetBlockService_CalledTwice_ReturnsSameInstance" +
                    "\n  - TransactionServiceTest.TestGetModelSpaceChildObjs2" +
                    "\n  - TransactionServiceTest.TestGetBlockRef23432" +
                    $"\n如需完整测试，请先 OPEN {RequiredDrawingTitle}.dwg 并设为当前图纸。" +
                    $"\n{XclipDrawingRequirements}");
            }
            catch (AcadException acadException)
            {
                Logger._.Error("RUNTESTS 检查当前图纸时 AutoCAD API 错误", acadException);
            }
            catch (System.Exception systemException)
            {
                Logger._.Error("RUNTESTS 检查当前图纸时系统错误", systemException);
            }
        }

        /// <summary>
        ///     向命令行输出消息；分别捕获 AutoCAD 与系统异常，避免掩盖原始错误
        /// </summary>
        /// <param name="message">要输出的消息</param>
        private static void TryWriteCommandMessage(string message)
        {
            try
            {
                CadServiceManager.ServiceEd.WriteMessage($"\n{message}");
            }
            catch (AcadException acadException)
            {
                Logger._.Error("RUNTESTS 向命令行输出消息时 AutoCAD API 错误", acadException);
            }
            catch (System.Exception systemException)
            {
                Logger._.Error("RUNTESTS 向命令行输出消息时系统错误", systemException);
            }
        }
    }
}
