using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DDNCadAddins.NUnitTests.Framework
{
    /// <summary>
    /// AutoCAD测试运行器 - 负责发现和执行所有测试，并生成报告
    /// </summary>
    public class AcadTestRunner
    {
        // 测试运行报告
        private class TestRun
        {
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int SkippedTests { get; set; }
            public int WarningTests { get; set; }
            public TimeSpan TotalDuration { get; set; }
            public Dictionary<string, List<TestMethodResult>> Results { get; set; }
        }
        
        // 测试方法结果
        private class TestMethodResult
        {
            public string MethodName { get; set; }
            public string Description { get; set; }
            public TestStatus Status { get; set; }
            public string Message { get; set; }
            public TimeSpan Duration { get; set; }
        }
        
        // 编辑器引用
        private Editor _editor;
        
        // 报告生成器
        private ReportGenerator _reportGenerator;
        
        // 测试结果
        private TestRun _testRun;
        
        /// <summary>
        /// 运行所有测试命令
        /// </summary>
        [CommandMethod("RUNUNITTESTS")]
        public void RunUnitTests()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                AcadApp.ShowAlertDialog("没有打开的AutoCAD文档，无法运行测试");
                return;
            }
            
            _editor = doc.Editor;
            _reportGenerator = new ReportGenerator();
            _testRun = new TestRun
            {
                Results = new Dictionary<string, List<TestMethodResult>>(),
                TotalDuration = TimeSpan.Zero
            };
            
            try
            {
                WriteMessage("\n========== 开始单元测试 ==========");
                
                // 记录开始时间
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                
                // 发现并执行所有测试
                DiscoverAndRunTests();
                
                // 停止计时
                totalStopwatch.Stop();
                _testRun.TotalDuration = totalStopwatch.Elapsed;
                
                // 输出测试结果摘要
                WriteTestSummary();
                
                // 生成报告
                string reportPath = _reportGenerator.GenerateReport(true);
                WriteMessage($"\n测试报告已生成: {reportPath}");
                
                WriteMessage("\n========== 单元测试结束 ==========");
            }
            catch (Exception ex)
            {
                WriteMessage($"\n运行测试时出错: {ex.Message}");
                WriteMessage(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// 发现并运行所有测试
        /// </summary>
        private void DiscoverAndRunTests()
        {
            // 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();
            
            // 查找所有测试类
            var testFixtureTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(TestFixtureAttribute), true).Length > 0)
                .ToList();
            
            WriteMessage($"\n发现 {testFixtureTypes.Count} 个测试类");
            
            foreach (var fixtureType in testFixtureTypes)
            {
                // 运行测试类中的所有测试
                RunTestFixture(fixtureType);
            }
        }
        
        /// <summary>
        /// 运行测试类中的所有测试
        /// </summary>
        /// <param name="fixtureType">测试类类型</param>
        private void RunTestFixture(Type fixtureType)
        {
            string fixtureName = fixtureType.Name;
            WriteMessage($"\n------ 运行测试类: {fixtureName} ------");
            
            // 创建测试类实例
            object fixtureInstance;
            try
            {
                fixtureInstance = Activator.CreateInstance(fixtureType);
            }
            catch (Exception ex)
            {
                WriteMessage($"\n创建测试类实例出错: {ex.Message}");
                return;
            }
            
            // 调用类级别的Setup方法
            try
            {
                var setupMethod = fixtureType.GetMethod("FixtureSetup");
                if (setupMethod != null)
                {
                    setupMethod.Invoke(fixtureInstance, null);
                }
            }
            catch (Exception ex)
            {
                WriteMessage($"\n执行测试类Setup出错: {ex.Message}");
                // 继续执行测试方法
            }
            
            // 获取所有测试方法
            var testMethods = fixtureType.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0 ||
                           m.GetCustomAttributes(typeof(AcadTestAttribute), true).Length > 0)
                .ToList();
            
            WriteMessage($"\n发现 {testMethods.Count} 个测试方法");
            
            // 准备保存结果
            if (!_testRun.Results.ContainsKey(fixtureName))
            {
                _testRun.Results[fixtureName] = new List<TestMethodResult>();
            }
            
            // 运行测试方法
            foreach (var testMethod in testMethods)
            {
                RunTestMethod(fixtureInstance, testMethod, fixtureName);
            }
            
            // 调用类级别的TearDown方法
            try
            {
                var tearDownMethod = fixtureType.GetMethod("FixtureTearDown");
                if (tearDownMethod != null)
                {
                    tearDownMethod.Invoke(fixtureInstance, null);
                }
            }
            catch (Exception ex)
            {
                WriteMessage($"\n执行测试类TearDown出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 运行单个测试方法
        /// </summary>
        /// <param name="instance">测试类实例</param>
        /// <param name="methodInfo">测试方法信息</param>
        /// <param name="fixtureName">测试类名称</param>
        private void RunTestMethod(object instance, MethodInfo methodInfo, string fixtureName)
        {
            string methodName = methodInfo.Name;
            
            // 获取测试特性
            AcadTestAttribute acadAttr = methodInfo.GetCustomAttribute<AcadTestAttribute>(true);
            string description = acadAttr?.Description ?? "AutoCAD单元测试";
            
            WriteMessage($"\n正在运行测试: {methodName}");
            Stopwatch sw = Stopwatch.StartNew();
            
            try
            {
                // 使用AcadNUnitTestBase执行测试
                var result = AcadNUnitTestBase.ExecuteAcadTest(methodInfo, instance);
                sw.Stop();
                result.Duration = sw.Elapsed;
                
                // 输出结果
                string statusText = result.Status == TestStatus.Passed ? "通过" : "失败";
                string resultText = $"测试 {methodName} {statusText}，耗时: {result.Duration.TotalSeconds:F2}秒";
                if (result.Status != TestStatus.Passed && !string.IsNullOrEmpty(result.Message))
                {
                    resultText += $"，原因: {result.Message}";
                }
                WriteMessage(resultText);
                
                // 添加到测试结果
                _testRun.Results[fixtureName].Add(new TestMethodResult
                {
                    MethodName = methodName,
                    Description = description,
                    Status = result.Status,
                    Message = result.Message,
                    Duration = result.Duration
                });
                
                // 添加到报告
                _reportGenerator.AddTestResult(fixtureName, methodName, result.Status, result.Message, result.Duration);
                
                // 更新统计
                _testRun.TotalTests++;
                switch (result.Status)
                {
                    case TestStatus.Passed:
                        _testRun.PassedTests++;
                        break;
                    case TestStatus.Failed:
                        _testRun.FailedTests++;
                        break;
                    case TestStatus.Skipped:
                        _testRun.SkippedTests++;
                        break;
                    case TestStatus.Inconclusive:
                        _testRun.WarningTests++;
                        break;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                
                WriteMessage($"\n执行测试 {methodName} 时发生异常: {ex.Message}");
                
                // 添加到测试结果
                _testRun.Results[fixtureName].Add(new TestMethodResult
                {
                    MethodName = methodName,
                    Description = description,
                    Status = TestStatus.Failed,
                    Message = ex.Message,
                    Duration = sw.Elapsed
                });
                
                // 添加到报告
                _reportGenerator.AddTestResult(fixtureName, methodName, TestStatus.Failed, ex.Message, sw.Elapsed);
                
                // 更新统计
                _testRun.TotalTests++;
                _testRun.FailedTests++;
            }
        }
        
        /// <summary>
        /// 输出测试结果摘要
        /// </summary>
        private void WriteTestSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n============ 测试结果摘要 ============");
            sb.AppendLine($"总测试数: {_testRun.TotalTests}");
            sb.AppendLine($"通过数: {_testRun.PassedTests}");
            sb.AppendLine($"失败数: {_testRun.FailedTests}");
            sb.AppendLine($"跳过数: {_testRun.SkippedTests}");
            sb.AppendLine($"警告数: {_testRun.WarningTests}");
            sb.AppendLine($"总耗时: {_testRun.TotalDuration.TotalSeconds:F2}秒");
            
            // 输出失败的测试
            if (_testRun.FailedTests > 0)
            {
                sb.AppendLine("\n失败的测试:");
                foreach (var fixture in _testRun.Results)
                {
                    foreach (var test in fixture.Value.Where(t => t.Status == TestStatus.Failed))
                    {
                        sb.AppendLine($"  {fixture.Key}.{test.MethodName}: {test.Message}");
                    }
                }
            }
            
            WriteMessage(sb.ToString());
        }
        
        /// <summary>
        /// 输出消息到AutoCAD命令行
        /// </summary>
        /// <param name="message">消息内容</param>
        private void WriteMessage(string message)
        {
            if (_editor != null)
            {
                _editor.WriteMessage(message + "\n");
            }
        }
    }
} 