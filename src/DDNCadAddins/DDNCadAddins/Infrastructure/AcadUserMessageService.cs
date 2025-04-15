using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    /// AutoCAD用户消息服务实现
    /// </summary>
    public class AcadUserMessageService : IUserMessageService
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        public AcadUserMessageService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 获取当前编辑器
        /// </summary>
        private Editor GetCurrentEditor()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            return doc?.Editor;
        }
        
        /// <summary>
        /// 显示普通信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public void ShowMessage(string message)
        {
            Editor ed = GetCurrentEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n{message}");
            }
            
            _logger.Log(message, false); // 记录到日志但不重复输出到命令行
        }
        
        /// <summary>
        /// 显示成功信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public void ShowSuccess(string message)
        {
            Editor ed = GetCurrentEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n✓ {message}");
            }
            
            _logger.Log($"成功: {message}", false);
        }
        
        /// <summary>
        /// 显示警告信息
        /// </summary>
        /// <param name="message">消息内容</param>
        public void ShowWarning(string message)
        {
            Editor ed = GetCurrentEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n⚠ {message}");
            }
            
            _logger.Log($"警告: {message}", false);
        }
        
        /// <summary>
        /// 显示错误信息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="ex">可选的异常对象</param>
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
                _logger.LogError(message, ex);
            }
            else
            {
                _logger.Log($"错误: {message}", false);
            }
        }
        
        /// <summary>
        /// 显示提示对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        public void ShowAlert(string message)
        {
            AcadApp.ShowAlertDialog(message);
            _logger.Log($"提示对话框: {message}", false);
        }
    }
} 