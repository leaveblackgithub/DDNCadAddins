using System;

namespace ServiceACAD
{
    /// <summary>
    ///     日志记录器实现
    /// </summary>
    public class Logger : ILogger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());

        private Logger()
        {
        }

        public static Logger _ => _instance.Value;

        /// <summary>
        ///     记录调试信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Debug(string message) =>
            System.Diagnostics.Debug.WriteLine($"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");

        /// <summary>
        ///     记录信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Info(string message) =>
            System.Diagnostics.Debug.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");

        /// <summary>
        ///     记录警告信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Warn(string message) =>
            System.Diagnostics.Debug.WriteLine($"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");

        /// <summary>
        ///     记录错误信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        public void Error(string message, Exception exception = null)
        {
            var errorMessage = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            if (exception != null)
            {
                errorMessage += $"\nException: {exception.Message}\nStack Trace: {exception.StackTrace}";
            }

            System.Diagnostics.Debug.WriteLine(errorMessage);
        }
    }
}
