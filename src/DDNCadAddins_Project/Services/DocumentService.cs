using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;

// 使用别名解决命名冲突
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     文档服务实现类 - 负责与AutoCAD文档交互的所有操作.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentService"/> class.
        ///     构造函数.
        /// </summary>
        /// <param name="logger">日志记录接口.</param>
        public DocumentService(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     获取当前活动文档对象.
        /// </summary>
        /// <returns>当前活动文档对象，如果没有打开的文档则返回null.</returns>
        public Document GetMdiActiveDocument()
        {
            try
            {
                return Application.DocumentManager.MdiActiveDocument;
            }
            catch (SystemException ex)
            {
                this.logger.LogError($"获取当前文档失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        ///     显示警告对话框.
        /// </summary>
        /// <param name="message">显示的消息.</param>
        public void ShowAlertDialog(string message)
        {
            try
            {
                Application.ShowAlertDialog(message);
            }
            catch (SystemException ex)
            {
                this.logger.LogError($"显示警告对话框失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     获取当前活动文档.
        /// </summary>
        /// <returns>当前文档是否可用.</returns>
        public bool GetActiveDocument(out Database database, out Editor editor)
        {
            database = null;
            editor = null;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return false;
            }

            database = doc.Database;
            editor = doc.Editor;
            return true;
        }

        /// <summary>
        ///     写入消息到命令行.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void WriteMessage(string message)
        {
            try
            {
                Document doc = this.GetMdiActiveDocument();
                if (doc != null && doc.Editor != null)
                {
                    doc.Editor.WriteMessage("\n" + message);
                }
            }
            catch (SystemException ex)
            {
                this.logger.LogError($"写入消息到命令行失败: {ex.Message}", ex);
            }
        }
    }
}
