using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 查找被XClip的块命令 - 专用于查找已被XClip的块
    /// </summary>
    public class FindXClippedBlocksCommand : CommandBase
    {
        private readonly IXClipBlockService _xclipService;
        private readonly IViewService _viewService;
        
        /// <summary>
        /// 构造函数 - 使用依赖注入
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="messageService">消息服务</param>
        /// <param name="acadService">AutoCAD服务</param>
        /// <param name="uiService">用户界面服务</param>
        /// <param name="xclipService">XClip服务</param>
        /// <param name="viewService">视图服务</param>
        public FindXClippedBlocksCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService,
            IXClipBlockService xclipService,
            IViewService viewService)
            : base(logger, messageService, acadService, uiService)
        {
            _xclipService = xclipService ?? throw new ArgumentNullException(nameof(xclipService));
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            CommandName = "FindXClippedBlocks";
        }
        
        /// <summary>
        /// 无参构造函数 - 创建默认依赖项（用于向后兼容）
        /// </summary>
        public FindXClippedBlocksCommand() : base()
        {
            _xclipService = new XClipBlockService(AcadService, Logger);
            _viewService = new AcadViewService(MessageService, Logger);
            CommandName = "FindXClippedBlocks";
        }
        
        /// <summary>
        /// 命令入口点
        /// </summary>
        [CommandMethod("FindXClippedBlocks")]
        public void FindXClippedBlocks()
        {
            Execute();
        }
        
        /// <summary>
        /// 执行命令的具体实现
        /// </summary>
        protected override void ExecuteCommand()
        {
            var doc = GetDocument();
            
            // 处理：调用服务查找XClip图块
            MessageService.ShowMessage("正在搜索被XClip的图块，请稍等...");
            
            var result = _xclipService.FindXClippedBlocks(doc.Database, doc.Editor);
            
            if (!result.Success)
            {
                MessageService.ShowError($"查找失败: {result.ErrorMessage}");
                return;
            }
            
            // 输出：显示结果和询问用户
            _viewService.ProcessAndDisplayXClipResults(result.Data);
            
            if (result.Data.Count > 0 && UiService.AskToZoomToFirstBlock())
            {
                _viewService.ZoomToFirstXClippedBlock(doc, result.Data);
            }
        }
    }
} 