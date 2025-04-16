using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;
using DDNCadAddins.Commands;

namespace DDNCadAddins.Commands.XClipTest
{
    /// <summary>
    /// 测试命令基类 - 为所有测试命令提供共同的功能和服务
    /// </summary>
    public abstract class TestCommandBase : CommandBase
    {
        /// <summary>
        /// XClip图块服务
        /// </summary>
        protected readonly IXClipBlockService XClipBlockService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        protected TestCommandBase()
            : base()
        {
            XClipBlockService = new XClipBlockService(AcadService, Logger);
        }
        
        /// <summary>
        /// 写入测试开始信息
        /// </summary>
        /// <param name="testName">测试名称</param>
        protected void WriteTestStart(string testName)
        {
            Editor ed = GetEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n===== 开始{testName}测试 =====");
            }
        }
        
        /// <summary>
        /// 写入测试结束信息
        /// </summary>
        /// <param name="testName">测试名称</param>
        protected void WriteTestEnd(string testName)
        {
            Editor ed = GetEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n===== {testName}测试结束 =====");
            }
        }
        
        /// <summary>
        /// 写入测试成功信息
        /// </summary>
        /// <param name="message">成功信息</param>
        protected void WriteTestSuccess(string message)
        {
            Editor ed = GetEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n成功: {message}");
            }
        }
        
        /// <summary>
        /// 写入测试失败信息
        /// </summary>
        /// <param name="message">失败信息</param>
        protected void WriteTestError(string message)
        {
            Editor ed = GetEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n错误: {message}");
            }
        }
        
        /// <summary>
        /// 写入测试警告信息
        /// </summary>
        /// <param name="message">警告信息</param>
        protected void WriteTestWarning(string message)
        {
            Editor ed = GetEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n警告: {message}");
            }
        }
        
        /// <summary>
        /// 写入测试信息
        /// </summary>
        /// <param name="message">测试信息</param>
        protected void WriteTestInfo(string message)
        {
            Editor ed = GetEditor();
            if (ed != null)
            {
                ed.WriteMessage($"\n{message}");
            }
        }
        
        /// <summary>
        /// 执行测试操作并捕获异常
        /// </summary>
        /// <param name="testAction">测试操作</param>
        /// <param name="errorMessage">错误信息前缀</param>
        protected void ExecuteTestOperation(Action testAction, string errorMessage)
        {
            try
            {
                // 确保测试环境干净
                PrepareTestEnvironment();
                
                // 执行测试操作
                testAction();
            }
            catch (System.Exception ex)
            {
                WriteTestError($"{errorMessage}: {ex.Message}");
                Logger.LogError($"{errorMessage}: {ex.Message}", ex);
                
                // 出错时尝试取消可能阻塞的命令
                CleanupActiveCommands();
            }
        }
        
        /// <summary>
        /// 准备测试环境 - 确保没有激活的命令
        /// </summary>
        protected void PrepareTestEnvironment()
        {
            try
            {
                CleanupActiveCommands();
                
                // 暂停一下，确保环境准备完成
                System.Threading.Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                Logger.LogError($"准备测试环境时出错: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 取消所有可能活动的命令
        /// </summary>
        protected void CleanupActiveCommands()
        {
            Editor ed = GetEditor();
            if (ed == null) {return;}
            
            try
            {
                // 在AutoCAD中，无法直接判断命令是否活动，我们直接尝试取消可能存在的命令
                WriteTestInfo("尝试取消可能的活动命令...");
                
                // 尝试取消命令，多次尝试以确保效果
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        ed.WriteMessage("\n * Cancel*");
                        ed.Command("_CANCEL");
                        System.Threading.Thread.Sleep(100);
                    }
                    catch
                    {
                        // 忽略取消命令时可能出现的异常
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"清理活动命令时出错: {ex.Message}", ex);
            }
        }
    }
} 