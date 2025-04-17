using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     用户界面服务接口.
    /// </summary>
    public interface IUserInterfaceService
    {
        /// <summary>
        ///     获取用户选择的图块对象ID集合.
        /// </summary>
        /// <returns>图块对象ID集合，如果用户取消则返回null.</returns>
        IEnumerable<ObjectId> GetSelectedBlocks();

        /// <summary>
        ///     获取用户选择的CSV文件保存路径.
        /// </summary>
        /// <returns>CSV文件路径，如果用户取消则返回null.</returns>
        string GetCsvSavePath();

        /// <summary>
        ///     输出结果消息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        void ShowResultMessage(string message);

        /// <summary>
        ///     询问用户是否打开生成的CSV文件.
        /// </summary>
        /// <returns>如果用户选择打开，则返回true.</returns>
        bool AskToOpenCsvFile();

        /// <summary>
        ///     打开指定路径的文件.
        /// </summary>
        /// <param name="filePath">文件路径.</param>
        void OpenFile(string filePath);

        /// <summary>
        ///     显示错误消息.
        /// </summary>
        /// <param name="message">错误消息.</param>
        void ShowErrorMessage(string message);

        /// <summary>
        ///     询问用户是否要缩放到第一个XClip图块.
        /// </summary>
        /// <returns>如果用户选择是，则返回true.</returns>
        bool AskToZoomToFirstBlock();

        /// <summary>
        ///     获取当前活动文档.
        /// </summary>
        /// <returns>当前活动文档，如果没有则返回null.</returns>
        Document GetActiveDocument();

        /// <summary>
        ///     验证当前活动文档是否存在.
        /// </summary>
        /// <returns>如果当前有打开的文档则返回true.</returns>
        bool ValidateActiveDocument();
    }
}
