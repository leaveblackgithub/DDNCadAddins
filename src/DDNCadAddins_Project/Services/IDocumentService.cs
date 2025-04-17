using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     文档服务接口 - 负责与AutoCAD文档交互的所有操作.
    /// </summary>
    public interface IDocumentService
    {
        /// <summary>
        ///     获取当前活动文档.
        /// </summary>
        /// <returns>当前文档是否可用.</returns>
        bool GetActiveDocument(out Database database, out Editor editor);

        /// <summary>
        ///     获取当前活动文档对象.
        /// </summary>
        /// <returns>当前活动文档对象，如果没有打开的文档则返回null.</returns>
        Document GetMdiActiveDocument();

        /// <summary>
        ///     显示警告对话框.
        /// </summary>
        /// <param name="message">显示的消息.</param>
        void ShowAlertDialog(string message);

        /// <summary>
        ///     写入消息到命令行.
        /// </summary>
        /// <param name="message">消息内容.</param>
        void WriteMessage(string message);
    }
}
