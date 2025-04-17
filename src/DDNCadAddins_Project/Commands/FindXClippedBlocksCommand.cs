using System;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    ///     查找被XClip的块命令 - 专用于查找已被XClip的块.
    /// </summary>
    public class FindXClippedBlocksCommand : CommandBase
    {
        private readonly IViewService viewService;
        private readonly IXClipBlockService xclipService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FindXClippedBlocksCommand"/> class.
        ///     构造函数 - 使用依赖注入.
        /// </summary>
        /// <param name="logger">日志记录器.</param>
        /// <param name="messageService">消息服务.</param>
        /// <param name="acadService">AutoCAD服务.</param>
        /// <param name="uiService">用户界面服务.</param>
        /// <param name="xclipService">XClip服务.</param>
        /// <param name="viewService">视图服务.</param>
        public FindXClippedBlocksCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService,
            IXClipBlockService xclipService,
            IViewService viewService)
            : base(logger, messageService, acadService, uiService)
        {
            this.xclipService = xclipService ?? throw new ArgumentNullException(nameof(xclipService));
            this.viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            this.CommandName = "FindXClippedBlocks";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FindXClippedBlocksCommand"/> class.
        ///     无参构造函数 - 创建默认依赖项（用于向后兼容）.
        /// </summary>
        public FindXClippedBlocksCommand()
        {
            this.xclipService = new XClipBlockService(this.AcadService, this.Logger);
            this.viewService = new AcadViewService(this.MessageService, this.Logger);
            this.CommandName = "FindXClippedBlocks";
        }

        /// <summary>
        ///     命令入口点.
        /// </summary>
        [CommandMethod("FindXClippedBlocks")]
        public void FindXClippedBlocks()
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

            // 输出：显示结果和询问用户
            this.viewService.ProcessAndDisplayXClipResults(result.Data);

            if (result.Data.Count > 0 && this.UiService.AskToZoomToFirstBlock())
            {
                this.viewService.ZoomToFirstXClippedBlock(doc, result.Data);
            }
        }
    }
}
