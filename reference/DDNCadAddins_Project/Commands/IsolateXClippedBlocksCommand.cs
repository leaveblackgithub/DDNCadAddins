using System;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    ///     隔离XClip块命令 - 专用于隔离显示XClip图块.
    /// </summary>
    public class IsolateXClippedBlocksCommand : CommandBase
    {
        private readonly IXClipBlockService xclipService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IsolateXClippedBlocksCommand"/> class.
        ///     构造函数 - 使用依赖注入.
        /// </summary>
        /// <param name="logger">日志记录器.</param>
        /// <param name="messageService">消息服务.</param>
        /// <param name="acadService">AutoCAD服务.</param>
        /// <param name="uiService">用户界面服务.</param>
        /// <param name="xclipService">XClip服务.</param>
        public IsolateXClippedBlocksCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService,
            IXClipBlockService xclipService)
            : base(logger, messageService, acadService, uiService)
        {
            this.xclipService = xclipService ?? throw new ArgumentNullException(nameof(xclipService));
            this.CommandName = "IsolateXClippedBlocks";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IsolateXClippedBlocksCommand"/> class.
        ///     无参构造函数 - 创建默认依赖项（用于向后兼容）.
        /// </summary>
        public IsolateXClippedBlocksCommand()
        {
            this.xclipService = new XClipBlockService(this.AcadService, this.Logger);
            this.CommandName = "IsolateXClippedBlocks";
        }

        /// <summary>
        ///     命令入口点.
        /// </summary>
        [CommandMethod("IsolateXClippedBlocks")]
        public void IsolateXClippedBlocks()
        {
            this.Execute();
        }

        /// <summary>
        ///     执行命令的具体实现.
        /// </summary>
        protected override void ExecuteCommand()
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
    }
}
