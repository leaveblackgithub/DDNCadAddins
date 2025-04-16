using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    /// 文件日志实现类 - 将日志写入文件
    /// </summary>
    public class FileLogger : ILogger
    {
        private StreamWriter _logWriter;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private string _operationName;
        private DateTime _startTime;
        
        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; private set; }
        
        /// <summary>
        /// 日志目录
        /// </summary>
        public static string LogDirectory
        {
            get
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataPath, "DDNCadAddins", "Logs");
            }
        }
        
        /// <summary>
        /// 初始化日志文件
        /// </summary>
        /// <param name="operationName">操作名称</param>
        public void Initialize(string operationName)
        {
            try
            {
                _operationName = operationName;
                _startTime = DateTime.Now;
                
                // 确保日志目录存在
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
                
                // 创建带有时间戳的日志文件名
                string timestamp = _startTime.ToString("yyyyMMdd_HHmmss");
                LogFilePath = Path.Combine(LogDirectory, $"{operationName}_{timestamp}.log");
                
                // 创建日志文件流
                _logWriter = new StreamWriter(LogFilePath, false, Encoding.UTF8);
                _logWriter.AutoFlush = true;
                
                // 写入日志头信息
                Log($"===== DDNCadAddins {operationName}命令执行日志 =====", false);
                Log($"开始时间: {_startTime}", false);
                Log($"版本: {GetAssemblyVersion()}", false);
                Log("=====================================", false);
            }
            catch (Exception ex)
            {
                // 如果初始化日志失败，输出到AutoCAD命令行
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n日志初始化失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 写入日志信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="writeToCommand">是否同时写入到命令行</param>
        public void Log(string message, bool writeToCommand = true)
        {
            try
            {
                // 添加时间戳
                string timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                
                // 写入到日志文件
                if (_logWriter != null)
                {
                    _logWriter.WriteLine(timestampedMessage);
                }
                
                // 添加到缓冲区
                _logBuffer.AppendLine(timestampedMessage);
                
                // 写入到命令行（仅在需要时）
                if (writeToCommand)
                {
                    Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        doc.Editor.WriteMessage($"\n{message}");
                    }
                }
            }
            catch
            {
                // 忽略日志写入错误
            }
        }
        
        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="ex">异常对象</param>
        public void LogError(string message, Exception ex)
        {
            Log($"错误: {message}", false);
            if (ex != null)
            {
                Log($"异常类型: {ex.GetType().Name}", false);
                Log($"异常消息: {ex.Message}", false);
                Log($"堆栈跟踪: {ex.StackTrace}", false);
            }
        }
        
        /// <summary>
        /// 关闭日志
        /// </summary>
        public void Close()
        {
            try
            {
                if (_logWriter != null)
                {
                    TimeSpan duration = DateTime.Now - _startTime;
                    Log($"执行时间: {duration.TotalSeconds:F2}秒", false);
                    Log($"结束时间: {DateTime.Now}", false);
                    Log($"===== {_operationName}命令日志结束 =====", false);
                    
                    _logWriter.Flush();
                    _logWriter.Close();
                    _logWriter.Dispose();
                    _logWriter = null;
                }
            }
            catch
            {
                // 忽略关闭日志错误
            }
        }
        
        /// <summary>
        /// 获取程序集版本号
        /// </summary>
        /// <returns>版本号字符串</returns>
        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        
        /// <summary>
        /// 获取最新日志文件路径
        /// </summary>
        /// <param name="commandName">命令名称</param>
        /// <returns>日志文件路径，如果不存在则返回null</returns>
        public static string GetLatestLogFile(string commandName)
        {
            try
            {
                if (Directory.Exists(LogDirectory))
                {
                    // 查找最新的日志文件
                    var logFiles = new DirectoryInfo(LogDirectory).GetFiles($"{commandName}_*.log")
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
    }
} 