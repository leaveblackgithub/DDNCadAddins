using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    // 使用别名解决命名冲突

    /// <summary>
    ///     命令基类 - 提供所有命令通用功能.
    /// </summary>
    public abstract class CommandBase
    {
        /// <summary>
        ///     AutoCAD服务.
        /// </summary>
        protected readonly IAcadService AcadService;

        /// <summary>
        ///     日志服务.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        ///     用户信息服务.
        /// </summary>
        protected readonly IUserMessageService MessageService;

        /// <summary>
        ///     用户界面服务.
        /// </summary>
        protected readonly IUserInterfaceService UiService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandBase"/> class.
        ///     构造函数 - 使用依赖注入.
        /// </summary>
        /// <param name="logger">日志记录器接口.</param>
        /// <param name="messageService">用户消息服务.</param>
        /// <param name="acadService">AutoCAD服务.</param>
        /// <param name="uiService">用户界面服务.</param>
        protected CommandBase(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService)
        {
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.MessageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            this.AcadService = acadService ?? throw new ArgumentNullException(nameof(acadService));
            this.UiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandBase"/> class.
        ///     无参构造函数 - 创建默认依赖项（用于向后兼容）.
        /// </summary>
        protected CommandBase()
            : this(
                new FileLogger(),
                new AcadUserMessageService(new FileLogger()),
                new AcadService(new FileLogger()),
                new AcadUserInterfaceService(new FileLogger(), new AcadUserMessageService(new FileLogger())))
        {
            // 注意：这种方式不是理想的依赖注入方式，仅用于过渡
            // 最好使用依赖注入容器进行管理
        }

        /// <summary>
        ///     Gets or sets 当前命令名称.
        /// </summary>
        protected string CommandName { get; set; }

        /// <summary>
        ///     执行命令.
        /// </summary>
        public void Execute()
        {
            try
            {
                // 初始化日志
                this.Logger.Initialize(this.CommandName);
                this.Logger.Log($"开始执行{this.CommandName}命令");

                // 执行命令
                this.ExecuteCommand();
            }
            catch (Exception ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                this.Logger.LogError(errorMessage, ex);
                this.UiService.ShowErrorMessage(errorMessage);
            }
            finally
            {
                this.Logger.Log("命令执行完成");
                this.Logger.Close();
            }
        }

        /// <summary>
        ///     执行命令的具体实现 - 子类必须重写此方法.
        /// </summary>
        protected abstract void ExecuteCommand();

        /// <summary>
        ///     获取当前文档.
        /// </summary>
        /// <returns>当前文档，如果未打开文档返回null.</returns>
        protected Document GetDocument()
        {
            return this.AcadService.GetMdiActiveDocument();
        }

        /// <summary>
        ///     获取当前数据库.
        /// </summary>
        /// <returns>当前数据库，如果未打开文档返回null.</returns>
        protected Database GetDatabase()
        {
            Document doc = this.GetDocument();
            return doc?.Database;
        }

        /// <summary>
        ///     获取当前编辑器.
        /// </summary>
        /// <returns>当前编辑器，如果未打开文档返回null.</returns>
        protected Editor GetEditor()
        {
            Document doc = this.GetDocument();
            return doc?.Editor;
        }

        /// <summary>
        ///     写入消息到命令行.
        /// </summary>
        /// <param name="message">消息内容.</param>
        protected void WriteMessage(string message)
        {
            this.AcadService.WriteMessage(message);
        }
    }
}
