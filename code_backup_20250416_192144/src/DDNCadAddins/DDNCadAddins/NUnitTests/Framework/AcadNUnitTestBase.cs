using System;
using System.Diagnostics;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DDNCadAddins.NUnitTests.Framework
{
    /// <summary>
    /// AutoCAD NUnit测试基类 - 所有测试类应继承自此类
    /// </summary>
    public abstract class AcadNUnitTestBase
    {
        /// <summary>
        /// 日志服务
        /// </summary>
        protected ILogger Logger { get; private set; }
        
        /// <summary>
        /// 消息服务
        /// </summary>
        protected IUserMessageService MessageService { get; private set; }
        
        /// <summary>
        /// AutoCAD服务
        /// </summary>
        protected IAcadService AcadService { get; private set; }
        
        /// <summary>
        /// 用户界面服务
        /// </summary>
        protected IUserInterfaceService UiService { get; private set; }
        
        /// <summary>
        /// 测试计时器
        /// </summary>
        private Stopwatch _stopwatch;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        protected AcadNUnitTestBase()
        {
            Logger = new FileLogger();
            MessageService = new AcadUserMessageService(Logger);
            AcadService = new AcadService(Logger);
            UiService = new AcadUserInterfaceService(Logger, MessageService);
            _stopwatch = new Stopwatch();
        }
        
        /// <summary>
        /// 在每个测试方法执行前准备环境
        /// </summary>
        [SetUp]
        public virtual void Setup()
        {
            // 初始化日志
            string testName = TestContext.CurrentContext.Test.Name;
            Logger.Initialize($"UnitTest_{testName}");
            Logger.Log($"开始执行测试: {testName}");
            
            _stopwatch.Restart();
        }
        
        /// <summary>
        /// 在每个测试方法执行后清理
        /// </summary>
        [TearDown]
        public virtual void TearDown()
        {
            _stopwatch.Stop();
            
            // 记录测试结果
            string testName = TestContext.CurrentContext.Test.Name;
            TestStatus status = TestContext.CurrentContext.Result.Outcome.Status;
            string message = TestContext.CurrentContext.Result.Message;
            
            if (status == TestStatus.Passed)
            {
                Logger.Log($"测试通过: {testName}, 耗时: {_stopwatch.ElapsedMilliseconds}ms");
            }
            else if (status == TestStatus.Failed)
            {
                Logger.LogError($"测试失败: {testName}, 原因: {message}", 
                    new Exception(TestContext.CurrentContext.Result.Message));
                
                // 如果是在AutoCAD中运行，也输出到命令行
                Document doc = AcadService.GetMdiActiveDocument();
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n测试 {testName} 失败: {message}");
                }
            }
            
            Logger.Close();
        }
        
        /// <summary>
        /// 获取当前文档
        /// </summary>
        protected Document GetDocument()
        {
            return AcadService.GetMdiActiveDocument();
        }
        
        /// <summary>
        /// 获取当前数据库
        /// </summary>
        protected Database GetDatabase()
        {
            Document doc = GetDocument();
            return doc?.Database;
        }
        
        /// <summary>
        /// 获取当前编辑器
        /// </summary>
        protected Editor GetEditor()
        {
            Document doc = GetDocument();
            return doc?.Editor;
        }
        
        /// <summary>
        /// 使用事务执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="commitTransaction">是否提交事务，默认为true</param>
        protected void WithTransaction(Action<Transaction> action, bool commitTransaction = true)
        {
            Database db = GetDatabase();
            if (db == null) {return;}
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    action(tr);
                    
                    if (commitTransaction)
                    {
                        tr.Commit();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"事务操作中出错: {ex.Message}", ex);
                    throw;
                }
            }
        }
        
        /// <summary>
        /// 输出消息到测试上下文和AutoCAD命令行
        /// </summary>
        /// <param name="message">消息内容</param>
        protected void WriteMessage(string message)
        {
            TestContext.WriteLine(message);
            
            Document doc = GetDocument();
            if (doc != null)
            {
                doc.Editor.WriteMessage($"\n{message}");
            }
        }
        
        /// <summary>
        /// 写入测试成功信息
        /// </summary>
        /// <param name="message">成功信息</param>
        protected void WriteTestSuccess(string message)
        {
            WriteMessage($"成功: {message}");
        }
        
        /// <summary>
        /// 写入测试失败信息
        /// </summary>
        /// <param name="message">失败信息</param>
        protected void WriteTestError(string message)
        {
            WriteMessage($"错误: {message}");
        }
        
        /// <summary>
        /// 写入测试警告信息
        /// </summary>
        /// <param name="message">警告信息</param>
        protected void WriteTestWarning(string message)
        {
            WriteMessage($"警告: {message}");
        }
        
        /// <summary>
        /// 写入测试信息
        /// </summary>
        /// <param name="message">测试信息</param>
        protected void WriteTestInfo(string message)
        {
            WriteMessage(message);
        }
        
        /// <summary>
        /// 执行AutoCAD测试方法
        /// </summary>
        /// <param name="methodInfo">方法信息</param>
        /// <param name="instance">测试类实例</param>
        /// <returns>测试结果</returns>
        public static TestResult ExecuteAcadTest(MethodInfo methodInfo, object instance)
        {
            // 获取AcadTest特性
            var acadTestAttr = methodInfo.GetCustomAttribute<AcadTestAttribute>();
            if (acadTestAttr == null)
            {
                // 如果没有AcadTest特性，默认所有选项都为true
                acadTestAttr = new AcadTestAttribute
                {
                    RequiresTransaction = true,
                    RequiresTestSetup = true,
                    RequiresTestCleanup = true
                };
            }
            
            // 创建一个新的测试上下文
            var test = new TestExecutionContext.IsolatedContext();
            test.CurrentTest = new Test(methodInfo);
            TestExecutionContext.CurrentContext.CurrentTest.Name = methodInfo.Name;
            
            // 准备测试环境
            try
            {
                if (acadTestAttr.RequiresTestSetup)
                {
                    if (instance is AcadNUnitTestBase testBase)
                    {
                        testBase.Setup();
                    }
                }
                
                // 执行测试方法
                if (acadTestAttr.RequiresTransaction)
                {
                    // 获取当前数据库
                    Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                    if (doc == null)
                    {
                        return new TestResult 
                        { 
                            Status = TestStatus.Failed, 
                            Message = "没有活动的AutoCAD文档" 
                        };
                    }
                    
                    Database db = doc.Database;
                    bool success = false;
                    string errorMessage = string.Empty;
                    
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            // 调用测试方法
                            methodInfo.Invoke(instance, null);
                            success = true;
                            
                            // 测试成功后提交事务
                            tr.Commit();
                        }
                        catch (TargetInvocationException ex)
                        {
                            errorMessage = ex.InnerException?.Message ?? ex.Message;
                        }
                        catch (Exception ex)
                        {
                            errorMessage = ex.Message;
                        }
                    }
                    
                    if (success)
                    {
                        return new TestResult { Status = TestStatus.Passed };
                    }
                    else
                    {
                        return new TestResult 
                        { 
                            Status = TestStatus.Failed, 
                            Message = errorMessage 
                        };
                    }
                }
                else
                {
                    // 不需要事务的情况
                    try
                    {
                        methodInfo.Invoke(instance, null);
                        return new TestResult { Status = TestStatus.Passed };
                    }
                    catch (TargetInvocationException ex)
                    {
                        return new TestResult 
                        { 
                            Status = TestStatus.Failed, 
                            Message = ex.InnerException?.Message ?? ex.Message 
                        };
                    }
                    catch (Exception ex)
                    {
                        return new TestResult 
                        { 
                            Status = TestStatus.Failed, 
                            Message = ex.Message 
                        };
                    }
                }
            }
            finally
            {
                // 清理测试环境
                if (acadTestAttr.RequiresTestCleanup)
                {
                    if (instance is AcadNUnitTestBase testBase)
                    {
                        testBase.TearDown();
                    }
                }
            }
        }
        
        /// <summary>
        /// 测试结果类
        /// </summary>
        public class TestResult
        {
            /// <summary>
            /// 测试状态
            /// </summary>
            public TestStatus Status { get; set; }
            
            /// <summary>
            /// 错误消息
            /// </summary>
            public string Message { get; set; }
            
            /// <summary>
            /// 测试耗时
            /// </summary>
            public TimeSpan Duration { get; set; }
        }
    }
} 