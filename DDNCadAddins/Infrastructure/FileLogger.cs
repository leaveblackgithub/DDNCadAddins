using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    /// 文件日志实现类 - 将日志写入文件
    /// </summary>
    public class FileLogger : ILogger
    {
        private StreamWriter _logWriter;
        private StringBuilder _logBuffer = new StringBuilder();
        
        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; private set; }
        
        /// <summary>
        /// 初始化日志文件
        /// </summary>
        /// <param name="operationName">操作名称</param>
        public void Initialize(string operationName)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDir = Path.Combine(appDataPath, "DDNCadAddins", "Logs");
                
                // 确保日志目录存在
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                
                // 创建带有时间戳的日志文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                LogFilePath = Path.Combine(logDir, $"XClipCommand_{operationName}_{timestamp}.log");
                
                // 创建日志文件流
                _logWriter = new StreamWriter(LogFilePath, false, Encoding.UTF8);
                _logWriter.AutoFlush = true;
                
                // 写入日志头信息
                if (operationName != "CreateXClippedBlock")
                {
                    Log("===== DDNCadAddins XClip命令执行日志 =====");
                    Log($"操作: {operationName}");
                    Log($"开始时间: {DateTime.Now}");
                    Log($"版本: {GetAssemblyVersion()}");
                    Log("=====================================");
                }
            }
            catch (System.Exception ex)
            {
                // 如果初始化日志失败，输出到AutoCAD命令行
                Document doc = Application.DocumentManager.MdiActiveDocument;
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
                // 写入到日志文件
                if (_logWriter != null)
                {
                    _logWriter.WriteLine(message);
                }
                
                // 添加到缓冲区
                _logBuffer.AppendLine(message);
                
                // 写入到命令行
                if (writeToCommand)
                {
                    Document doc = Application.DocumentManager.MdiActiveDocument;
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
        public void LogError(string message, System.Exception ex)
        {
            Log($"错误: {message}");
            if (ex != null)
            {
                Log($"异常类型: {ex.GetType().Name}");
                Log($"异常消息: {ex.Message}");
                Log($"堆栈跟踪: {ex.StackTrace}");
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
                    Log($"结束时间: {DateTime.Now}");
                    Log("===== 日志结束 =====");
                    
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
        /// <param name="fileNamePrefix">文件名前缀</param>
        /// <returns>日志文件路径，如果不存在则返回null</returns>
        public static string GetLatestLogFile(string fileNamePrefix)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDir = Path.Combine(appDataPath, "DDNCadAddins", "Logs");
                
                if (Directory.Exists(logDir))
                {
                    // 查找最新的日志文件
                    var logFiles = new DirectoryInfo(logDir).GetFiles($"{fileNamePrefix}_*.log")
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