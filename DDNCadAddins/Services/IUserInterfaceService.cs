using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// 用户界面服务接口
    /// </summary>
    public interface IUserInterfaceService
    {
        /// <summary>
        /// 获取用户选择的图块对象ID集合
        /// </summary>
        /// <returns>图块对象ID集合，如果用户取消则返回null</returns>
        IEnumerable<ObjectId> GetSelectedBlocks();
        
        /// <summary>
        /// 获取用户选择的CSV文件保存路径
        /// </summary>
        /// <returns>CSV文件路径，如果用户取消则返回null</returns>
        string GetCsvSavePath();
        
        /// <summary>
        /// 输出结果消息
        /// </summary>
        /// <param name="message">消息内容</param>
        void ShowResultMessage(string message);
        
        /// <summary>
        /// 询问用户是否打开生成的CSV文件
        /// </summary>
        /// <returns>如果用户选择打开，则返回true</returns>
        bool AskToOpenCsvFile();
        
        /// <summary>
        /// 打开指定路径的文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        void OpenFile(string filePath);
        
        /// <summary>
        /// 显示错误消息
        /// </summary>
        /// <param name="message">错误消息</param>
        void ShowErrorMessage(string message);
    }
} 