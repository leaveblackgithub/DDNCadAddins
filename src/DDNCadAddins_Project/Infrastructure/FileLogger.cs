using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace DDNCadAddins.Infrastructure
{
    // 使用别名解决命名冲突

    /// <summary>
    ///     文件日志实现类 - 将日志写入文件.
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private readonly StringBuilder logBuffer = new StringBuilder();
        private StreamWriter logWriter;
        private string operationName;
        private DateTime startTime;

        /// <summary>
        ///     Gets 日志文件路径.
        /// </summary>
        public string LogFilePath { get; private set; }

        /// <summary>
        ///     Gets 日志目录.
        /// </summary>
        public static string LogDirectory =>

            // 使用指定的固定路径
            Path.Combine(
                Path.GetDirectoryName(Path.GetDirectoryName(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))),
                "logs");

        /// <summary>
        ///     初始化日志文件.
        /// </summary>
        /// <param name="operationName">操作名称.</param>
        public void Initialize(string operationName)
        {
            try
            {
                this.operationName = operationName;
                this.startTime = DateTime.Now;

                // 确保日志目录存在
                if (!Directory.Exists(LogDirectory))
                {
                    _ = Directory.CreateDirectory(LogDirectory);
                }

                // 创建带有时间戳的日志文件名
                string timestamp = this.startTime.ToString("yyyyMMdd_HHmmss");
                this.LogFilePath = Path.Combine(LogDirectory, $"{operationName}_{timestamp}.log");

                // 创建日志文件流
                this.logWriter = new StreamWriter(this.LogFilePath, false, Encoding.UTF8)
                {
                    AutoFlush = true,
                };

                // 写入日志头信息
                this.Log($"===== DDNCadAddins {operationName}命令执行日志 =====", false);
                this.Log($"开始时间: {this.startTime}", false);
                this.Log($"版本: {GetAssemblyVersion()}", false);
                this.Log("=====================================", false);
            }
            catch (Exception ex)
            {
                // 如果初始化日志失败，输出到AutoCAD命令行
                Autodesk.AutoCAD.ApplicationServices.Document doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage($"\n日志初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     写入日志信息.
        /// </summary>
        /// <param name="message">日志消息.</param>
        /// <param name="writeToCommand">是否同时写入到命令行.</param>
        public void Log(string message, bool writeToCommand = true)
        {
            try
            {
                // 添加时间戳
                string timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

                // 写入到日志文件
                this.logWriter?.WriteLine(timestampedMessage);

                // 添加到缓冲区
                _ = this.logBuffer.AppendLine(timestampedMessage);

                // 写入到命令行（仅在需要时）
                if (writeToCommand)
                {
                    Autodesk.AutoCAD.ApplicationServices.Document doc = Application.DocumentManager.MdiActiveDocument;
                    doc?.Editor.WriteMessage($"\n{message}");
                }
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        /// <summary>
        ///     记录错误信息.
        /// </summary>
        /// <param name="message">错误消息.</param>
        /// <param name="ex">异常对象.</param>
        public void LogError(string message, Exception ex)
        {
            this.Log($"错误: {message}", false);
            if (ex != null)
            {
                this.Log($"异常类型: {ex.GetType().Name}", false);
                this.Log($"异常消息: {ex.Message}", false);
                this.Log($"堆栈跟踪: {ex.StackTrace}", false);
            }
        }

        /// <summary>
        ///     关闭日志.
        /// </summary>
        public void Close()
        {
            try
            {
                if (this.logWriter != null)
                {
                    TimeSpan duration = DateTime.Now - this.startTime;
                    this.Log($"执行时间: {duration.TotalSeconds:F2}秒", false);
                    this.Log($"结束时间: {DateTime.Now}", false);
                    this.Log($"===== {this.operationName}命令日志结束 =====", false);

                    this.logWriter.Flush();
                    this.logWriter.Close();
                    this.logWriter.Dispose();
                    this.logWriter = null;
                }
            }
            catch
            {
                // 忽略关闭日志错误
            }
        }

        public void LogInfo(string statusMessage)
        {
            this.Log($"{statusMessage}", false);
        }

        /// <summary>
        ///     获取程序集版本号.
        /// </summary>
        /// <returns>版本号字符串.</returns>
        private static string GetAssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        /// <summary>
        ///     获取最新日志文件路径.
        /// </summary>
        /// <param name="commandName">命令名称.</param>
        /// <returns>日志文件路径，如果不存在则返回null.</returns>
        public static string GetLatestLogFile(string commandName)
        {
            try
            {
                if (Directory.Exists(LogDirectory))
                {
                    // 查找最新的日志文件
                    System.Collections.Generic.List<FileInfo> logFiles = new DirectoryInfo(LogDirectory).GetFiles($"{commandName}_*.log")
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();

                    if (logFiles.Count > 0)
                    {
                        return logFiles[0].FullName;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
