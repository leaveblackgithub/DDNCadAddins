using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;

namespace DDNCadAddins.Infrastructure
{
    // 使用别名解决命名冲突

    /// <summary>
    ///     AutoCAD用户消息服务实现.
    /// </summary>
    public class AcadUserMessageService : IUserMessageService
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AcadUserMessageService"/> class.
        ///     构造函数.
        /// </summary>
        /// <param name="logger">日志服务.</param>
        public AcadUserMessageService(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     显示普通信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void ShowMessage(string message)
        {
            Editor ed = GetCurrentEditor();
            ed?.WriteMessage($"\n{message}");

            this.logger.Log(message, false); // 记录到日志但不重复输出到命令行
        }

        /// <summary>
        ///     显示成功信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void ShowSuccess(string message)
        {
            Editor ed = GetCurrentEditor();
            ed?.WriteMessage($"\n✓ {message}");

            this.logger.Log($"成功: {message}", false);
        }

        /// <summary>
        ///     显示警告信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void ShowWarning(string message)
        {
            Editor ed = GetCurrentEditor();
            ed?.WriteMessage($"\n⚠ {message}");

            this.logger.Log($"警告: {message}", false);
        }

        /// <summary>
        ///     显示错误信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        /// <param name="ex">可选的异常对象.</param>
        public void ShowError(string message, Exception ex = null)
        {
            Editor ed = GetCurrentEditor();
            if (ed != null)
            {
                if (ex != null)
                {
                    ed.WriteMessage($"\n❌ {message}: {ex.Message}");
                }
                else
                {
                    ed.WriteMessage($"\n❌ {message}");
                }
            }

            if (ex != null)
            {
                this.logger.LogError(message, ex);
            }
            else
            {
                this.logger.Log($"错误: {message}", false);
            }
        }

        /// <summary>
        ///     显示提示对话框.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void ShowAlert(string message)
        {
            Application.ShowAlertDialog(message);
            this.logger.Log($"提示对话框: {message}", false);
        }

        /// <summary>
        ///     获取当前编辑器.
        /// </summary>
        private static Editor GetCurrentEditor()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc = Application.DocumentManager.MdiActiveDocument;
            return doc?.Editor;
        }
    }
}
