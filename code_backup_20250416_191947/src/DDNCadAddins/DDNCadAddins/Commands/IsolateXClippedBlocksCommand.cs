using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 隔离XClip块命令 - 专用于隔离显示XClip图块
    /// </summary>
    public class IsolateXClippedBlocksCommand : CommandBase
    {
        private readonly IXClipBlockService _xclipService;
        
        /// <summary>
        /// 构造函数 - 使用依赖注入
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="messageService">消息服务</param>
        /// <param name="acadService">AutoCAD服务</param>
        /// <param name="uiService">用户界面服务</param>
        /// <param name="xclipService">XClip服务</param>
        public IsolateXClippedBlocksCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService,
            IXClipBlockService xclipService)
            : base(logger, messageService, acadService, uiService)
        {
            _xclipService = xclipService ?? throw new ArgumentNullException(nameof(xclipService));
            CommandName = "IsolateXClippedBlocks";
        }
        
        /// <summary>
        /// 无参构造函数 - 创建默认依赖项（用于向后兼容）
        /// </summary>
        public IsolateXClippedBlocksCommand() : base()
        {
            _xclipService = new XClipBlockService(AcadService, Logger);
            CommandName = "IsolateXClippedBlocks";
        }
        
        /// <summary>
        /// 命令入口点
        /// </summary>
        [CommandMethod("IsolateXClippedBlocks")]
        public void IsolateXClippedBlocks()
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
            
            if (result.Data.Count == 0)
            {
                MessageService.ShowWarning("未找到任何被XClip的图块");
                return;
            }
            
            // 处理：将找到的XClip图块移到顶层并隔离
            MessageService.ShowMessage($"找到 {result.Data.Count} 个被XClip的图块，正在处理...");
            
            // 明确指定类型，避免编译器混淆
            List<XClippedBlockInfo> xclippedBlocks = result.Data;
            var isolateResult = _xclipService.IsolateXClippedBlocks(doc.Database, xclippedBlocks);
            
            if (isolateResult.Success)
            {
                MessageService.ShowSuccess(isolateResult.Message);
            }
            else
            {
                MessageService.ShowError($"隔离XClip图块失败: {isolateResult.ErrorMessage}");
            }
        }
    }
} 