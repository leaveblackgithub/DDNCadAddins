using System;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    /// 日志接口 - 依赖倒置原则
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 初始化日志系统
        /// </summary>
        /// <param name="operationName">操作名称</param>
        void Initialize(string operationName);
        
        /// <summary>
        /// 记录日志信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="writeToCommand">是否同时写入到命令行</param>
        void Log(string message, bool writeToCommand = true);
        
        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="ex">异常对象</param>
        void LogError(string message, System.Exception ex);
        
        /// <summary>
        /// 关闭日志
        /// </summary>
        void Close();
        void LogInfo(string statusMessage);
    }
} 