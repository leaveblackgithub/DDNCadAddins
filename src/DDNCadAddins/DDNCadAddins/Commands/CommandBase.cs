using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 命令基类 - 提供所有命令通用功能
    /// </summary>
    public abstract class CommandBase
    {
        /// <summary>
        /// 当前命令名称
        /// </summary>
        protected string CommandName { get; set; }
        
        /// <summary>
        /// 日志服务
        /// </summary>
        protected readonly ILogger Logger;
        
        /// <summary>
        /// 用户信息服务
        /// </summary>
        protected readonly IUserMessageService MessageService;
        
        /// <summary>
        /// AutoCAD服务
        /// </summary>
        protected readonly IAcadService AcadService;
        
        /// <summary>
        /// 用户界面服务
        /// </summary>
        protected readonly IUserInterfaceService UiService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        protected CommandBase()
        {
            Logger = new FileLogger();
            MessageService = new AcadUserMessageService(Logger);
            AcadService = new AcadService(Logger);
            UiService = new AcadUserInterfaceService(Logger, MessageService);
        }
        
        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute()
        {
            try
            {
                // 初始化日志
                Logger.Initialize(CommandName);
                Logger.Log($"开始执行{CommandName}命令");
                
                // 执行命令
                ExecuteCommand();
            }
            catch (System.Exception ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                Logger.LogError(errorMessage, ex);
                UiService.ShowErrorMessage(errorMessage);
            }
            finally
            {
                Logger.Log("命令执行完成");
                Logger.Close();
            }
        }
        
        /// <summary>
        /// 执行命令的具体实现 - 子类必须重写此方法
        /// </summary>
        protected abstract void ExecuteCommand();
        
        /// <summary>
        /// 获取当前文档
        /// </summary>
        /// <returns>当前文档，如果未打开文档返回null</returns>
        protected Document GetDocument()
        {
            return AcadService.GetMdiActiveDocument();
        }
        
        /// <summary>
        /// 获取当前数据库
        /// </summary>
        /// <returns>当前数据库，如果未打开文档返回null</returns>
        protected Database GetDatabase()
        {
            Document doc = GetDocument();
            return doc?.Database;
        }
        
        /// <summary>
        /// 获取当前编辑器
        /// </summary>
        /// <returns>当前编辑器，如果未打开文档返回null</returns>
        protected Editor GetEditor()
        {
            Document doc = GetDocument();
            return doc?.Editor;
        }
        
        /// <summary>
        /// 写入消息到命令行
        /// </summary>
        /// <param name="message">消息内容</param>
        protected void WriteMessage(string message)
        {
            AcadService.WriteMessage(message);
        }
    }
} 