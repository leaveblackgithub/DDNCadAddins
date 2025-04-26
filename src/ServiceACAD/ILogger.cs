using System;

namespace ServiceACAD
{
    /// <summary>
    ///     日志记录接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        ///     记录调试信息
        /// </summary>
        /// <param name="message">日志消息</param>
        void Debug(string message);

        /// <summary>
        ///     记录信息
        /// </summary>
        /// <param name="message">日志消息</param>
        void Info(string message);

        /// <summary>
        ///     记录警告信息
        /// </summary>
        /// <param name="message">日志消息</param>
        void Warn(string message);

        /// <summary>
        ///     记录错误信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        void Error(string message, Exception exception = null);
    }
} 