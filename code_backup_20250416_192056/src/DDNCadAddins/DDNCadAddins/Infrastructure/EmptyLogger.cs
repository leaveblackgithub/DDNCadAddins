using System;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    /// 空日志记录器 - 不执行任何操作
    /// </summary>
    public class EmptyLogger : ILogger
    {
        /// <summary>
        /// 初始化日志记录器
        /// </summary>
        /// <param name="operationName">操作名称</param>
        public void Initialize(string operationName)
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 记录日志信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="writeToCommand">是否同时写入到命令行</param>
        public void Log(string message, bool writeToCommand = true)
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 关闭日志记录器
        /// </summary>
        public void Close()
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="ex">异常对象</param>
        public void LogError(string message, Exception ex)
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 记录信息 - 扩展方法（非ILogger接口要求）
        /// </summary>
        /// <param name="message">信息消息</param>
        public void LogInfo(string message)
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 记录警告 - 扩展方法（非ILogger接口要求）
        /// </summary>
        /// <param name="message">警告消息</param>
        public void LogWarning(string message)
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 记录调试信息 - 扩展方法（非ILogger接口要求）
        /// </summary>
        /// <param name="message">调试消息</param>
        public void LogDebug(string message)
        {
            // 空实现，不做任何操作
        }
        
        /// <summary>
        /// 记录错误（简化版本）- 扩展方法（非ILogger接口要求）
        /// </summary>
        /// <param name="message">错误消息</param>
        public void Error(string message)
        {
            // 空实现，不做任何操作
        }
    }
} 