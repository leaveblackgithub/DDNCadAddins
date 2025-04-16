using System;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 打开XClip日志命令 - 专用于打开日志文件目录
    /// </summary>
    public class OpenXClipLogCommand : CommandBase
    {
        /// <summary>
        /// 构造函数 - 使用依赖注入
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="messageService">消息服务</param>
        /// <param name="acadService">AutoCAD服务</param>
        /// <param name="uiService">用户界面服务</param>
        public OpenXClipLogCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService)
            : base(logger, messageService, acadService, uiService)
        {
            CommandName = "OpenXClipLog";
        }
        
        /// <summary>
        /// 无参构造函数 - 创建默认依赖项（用于向后兼容）
        /// </summary>
        public OpenXClipLogCommand() : base()
        {
            CommandName = "OpenXClipLog";
        }
        
        /// <summary>
        /// 命令入口点
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenXClipLog()
        {
            Execute();
        }
        
        /// <summary>
        /// 执行命令的具体实现
        /// </summary>
        protected override void ExecuteCommand()
        {
            string logDirectory = FileLogger.LogDirectory;
            
            if (Directory.Exists(logDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                MessageService.ShowMessage($"已打开日志文件所在目录: {logDirectory}");
            }
            else
            {
                MessageService.ShowAlert($"日志目录不存在: {logDirectory}");
            }
        }
    }
} 