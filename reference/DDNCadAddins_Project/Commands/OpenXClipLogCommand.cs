using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    ///     打开XClip日志命令 - 专用于打开日志文件目录.
    /// </summary>
    public class OpenXClipLogCommand : CommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenXClipLogCommand"/> class.
        ///     构造函数 - 使用依赖注入.
        /// </summary>
        /// <param name="logger">日志记录器.</param>
        /// <param name="messageService">消息服务.</param>
        /// <param name="acadService">AutoCAD服务.</param>
        /// <param name="uiService">用户界面服务.</param>
        public OpenXClipLogCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService)
            : base(logger, messageService, acadService, uiService)
        {
            this.CommandName = "OpenXClipLog";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenXClipLogCommand"/> class.
        ///     无参构造函数 - 创建默认依赖项（用于向后兼容）.
        /// </summary>
        public OpenXClipLogCommand()
        {
            this.CommandName = "OpenXClipLog";
        }

        /// <summary>
        ///     命令入口点.
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenXClipLog()
        {
            this.Execute();
        }

        /// <summary>
        ///     执行命令的具体实现.
        /// </summary>
        protected override void ExecuteCommand()
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
    }
}
