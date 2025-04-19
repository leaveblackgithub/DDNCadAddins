using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    // 使用别名解决命名冲突

    /// <summary>
    ///     XClip相关命令类 - 实现XClip相关的CAD命令.
    /// </summary>
    public class XClipCommand : CommandBase
    {
        private readonly IViewService viewService;
        private readonly IXClipBlockService xclipService;

        /// <summary>
        /// Initializes a new instance of the <see cref="XClipCommand"/> class.
        ///     构造函数.
        /// </summary>
        public XClipCommand()
        {
            // 创建XClipBlockService实例，传入依赖项
            this.xclipService = new XClipBlockService(this.AcadService, this.Logger);
            this.viewService = new AcadViewService(this.MessageService, this.Logger);
        }

        /// <summary>
        ///     查找所有被XClip的图块命令.
        /// </summary>
        [CommandMethod("FindXClippedBlocks")]
        public void FindXClippedBlocks()
        {
            this.CommandName = "FindXClippedBlocks";
            this.Execute();
        }

        /// <summary>
        ///     创建测试图块命令.
        /// </summary>
        [CommandMethod("CreateXClippedBlock")]
        public void CreateXClippedBlock()
        {
            this.CommandName = "CreateXClippedBlock";
            this.Execute();
        }

        /// <summary>
        ///     打开日志文件目录命令.
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenLogFile()
        {
            this.CommandName = "OpenXClipLog";
            this.Execute();
        }

        /// <summary>
        ///     将找到的XCLIPPEDBLOCK移到顶层并隔离显示命令.
        /// </summary>
        [CommandMethod("IsolateXClippedBlocks")]
        public void IsolateXClippedBlocks()
        {
            this.CommandName = "IsolateXClippedBlocks";
            this.Execute();
        }

        /// <summary>
        ///     命令具体实现 - 根据CommandName执行对应的命令逻辑.
        /// </summary>
        protected override void ExecuteCommand()
        {
            switch (this.CommandName)
            {
                case "FindXClippedBlocks":
                    this.ExecuteFindXClippedBlocks();
                    break;
                case "CreateXClippedBlock":
                    this.ExecuteCreateXClippedBlock();
                    break;
                case "OpenXClipLog":
                    this.ExecuteOpenLogFile();
                    break;
                case "IsolateXClippedBlocks":
                    this.ExecuteIsolateXClippedBlocks();
                    break;
                default:
                    this.MessageService.ShowError($"未知命令: {this.CommandName}");
                    break;
            }
        }

        /// <summary>
        ///     执行查找所有被XClip的图块命令.
        /// </summary>
        private void ExecuteFindXClippedBlocks()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc = this.GetDocument();

            // 处理：调用服务查找XClip图块
            this.MessageService.ShowMessage("正在搜索被XClip的图块，请稍等...");

            Models.OperationResult<System.Collections.Generic.List<Models.XClippedBlockInfo>> result = this.xclipService.FindXClippedBlocks(doc.Database, doc.Editor);

            if (!result.Success)
            {
                this.MessageService.ShowError($"查找失败: {result.ErrorMessage}");
                return;
            }

            // 输出：显示结果和询问用户
            this.viewService.ProcessAndDisplayXClipResults(result.Data);

            if (result.Data.Count > 0 && this.UiService.AskToZoomToFirstBlock())
            {
                this.viewService.ZoomToFirstXClippedBlock(doc, result.Data);
            }
        }

        /// <summary>
        ///     执行创建测试图块命令.
        /// </summary>
        private void ExecuteCreateXClippedBlock()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc = this.GetDocument();

            // 处理：创建测试块
            this.MessageService.ShowMessage("正在创建测试块...");

            Models.OperationResult<ObjectId> result = this.xclipService.CreateTestBlockWithId(doc.Database);

            // 输出：处理创建结果
            if (result.Success)
            {
                this.MessageService.ShowSuccess("测试块创建成功");

                // 执行自动XClip操作
                this.ExecuteAutoXClip(doc.Database, result.Data);
            }
            else
            {
                this.MessageService.ShowError($"创建测试块失败: {result.ErrorMessage}");
                this.MessageService.ShowMessage("可能原因:");
                this.MessageService.ShowMessage("1. 文件或目录权限问题");
                this.MessageService.ShowMessage("2. 块名冲突");
                this.MessageService.ShowMessage("3. CAD内部错误");
            }
        }

        /// <summary>
        ///     执行打开日志文件目录命令.
        /// </summary>
        private void ExecuteOpenLogFile()
        {
            string logDirectory = FileLogger.LogDirectory;

            if (Directory.Exists(logDirectory))
            {
                _ = Process.Start("explorer.exe", logDirectory);
                this.MessageService.ShowMessage($"已打开日志文件所在目录: {logDirectory}");
            }
            else
            {
                this.MessageService.ShowAlert($"日志目录不存在: {logDirectory}");
            }
        }

        /// <summary>
        ///     执行隔离XClip图块命令.
        /// </summary>
        private void ExecuteIsolateXClippedBlocks()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc = this.GetDocument();

            // 处理：调用服务查找XClip图块
            this.MessageService.ShowMessage("正在搜索被XClip的图块，请稍等...");

            Models.OperationResult<System.Collections.Generic.List<Models.XClippedBlockInfo>> result = this.xclipService.FindXClippedBlocks(doc.Database, doc.Editor);

            if (!result.Success)
            {
                this.MessageService.ShowError($"查找失败: {result.ErrorMessage}");
                return;
            }

            if (result.Data.Count == 0)
            {
                this.MessageService.ShowWarning("未找到任何被XClip的图块");
                return;
            }

            // 处理：将找到的XClip图块移到顶层并隔离
            this.MessageService.ShowMessage($"找到 {result.Data.Count} 个被XClip的图块，正在处理...");

            // 明确指定类型，避免编译器混淆
            System.Collections.Generic.List<Models.XClippedBlockInfo> xclippedBlocks = result.Data;
            Models.OperationResult isolateResult = this.xclipService.IsolateXClippedBlocks(doc.Database, xclippedBlocks);

            if (isolateResult.Success)
            {
                this.MessageService.ShowSuccess(isolateResult.Message);
            }
            else
            {
                this.MessageService.ShowError($"隔离XClip图块失败: {isolateResult.ErrorMessage}");
            }
        }

        /// <summary>
        ///     执行自动XClip操作.
        /// </summary>
        private void ExecuteAutoXClip(Database database, ObjectId blockRefId)
        {
            this.MessageService.ShowMessage("正在执行自动XClip操作...");

            Models.OperationResult xclipResult = this.xclipService.AutoXClipBlock(database, blockRefId);

            if (xclipResult.Success)
            {
                this.MessageService.ShowSuccess("自动XClip操作成功完成");
            }
            else
            {
                this.MessageService.ShowError($"自动XClip操作失败: {xclipResult.ErrorMessage}");
                this.ShowManualXClipInstructions();
            }
        }

        /// <summary>
        ///     显示手动XClip操作说明.
        /// </summary>
        private void ShowManualXClipInstructions()
        {
            this.MessageService.ShowMessage("失败后可尝试手动执行XClip操作:");
            this.MessageService.ShowMessage("1. 输入XCLIP命令并按回车");
            this.MessageService.ShowMessage("2. 选择创建的测试块并按回车");
            this.MessageService.ShowMessage("3. 输入N并按回车(表示新建裁剪边界)");
            this.MessageService.ShowMessage("4. 输入R并按回车(表示使用矩形边界)");
            this.MessageService.ShowMessage("5. 绘制矩形裁剪边界");
        }
    }
}
