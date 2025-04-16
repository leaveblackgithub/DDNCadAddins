using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework.Interfaces;
using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace DDNCadAddins.NUnitTests.Framework
{
    /// <summary>
    /// 测试报告生成器 - 负责生成HTML格式的测试报告
    /// </summary>
    public class ReportGenerator
    {
        // 报告对象
        private ExtentReports _extentReports;
        
        // 测试与报告节点的映射
        private Dictionary<string, ExtentTest> _testNodes;
        
        // 报告文件夹路径
        private string _reportDirPath;
        
        // 报告文件路径
        private string _reportFilePath;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ReportGenerator()
        {
            Initialize();
        }
        
        /// <summary>
        /// 初始化报告生成器
        /// </summary>
        private void Initialize()
        {
            try
            {
                _testNodes = new Dictionary<string, ExtentTest>();
                
                // 创建报告目录
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                _reportDirPath = Path.Combine(assemblyDirectory, "NUnitTests", "Reports");
                
                // 确保目录存在
                if (!Directory.Exists(_reportDirPath))
                {
                    Directory.CreateDirectory(_reportDirPath);
                }
                
                // 生成报告文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _reportFilePath = Path.Combine(_reportDirPath, $"TestReport_{timestamp}.html");
                
                // 创建报告
                _extentReports = new ExtentReports();
                
                // 添加HTML报告生成器
                var htmlReporter = new ExtentHtmlReporter(_reportFilePath);
                htmlReporter.Config.ReportName = "DDNCadAddins 单元测试报告";
                htmlReporter.Config.DocumentTitle = "DDNCadAddins 测试报告";
                htmlReporter.Config.Theme = AventStack.ExtentReports.Reporter.Configuration.Theme.Standard;
                
                _extentReports.AttachReporter(htmlReporter);
                
                // 添加系统信息
                _extentReports.AddSystemInfo("操作系统", Environment.OSVersion.ToString());
                _extentReports.AddSystemInfo("AutoCAD版本", Application.Version.ToString());
                _extentReports.AddSystemInfo("测试时间", DateTime.Now.ToString());
            }
            catch (Exception ex)
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n初始化报告生成器出错: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 开始测试套件
        /// </summary>
        /// <param name="suiteName">测试套件名称</param>
        /// <returns>测试节点</returns>
        public ExtentTest StartTestSuite(string suiteName)
        {
            if (_extentReports == null) {Initialize();}
                
            if (_testNodes.ContainsKey(suiteName)) {return _testNodes[suiteName];}
                
            var suiteNode = _extentReports.CreateTest(suiteName);
            _testNodes[suiteName] = suiteNode;
            return suiteNode;
        }
        
        /// <summary>
        /// 开始测试
        /// </summary>
        /// <param name="suiteName">套件名称</param>
        /// <param name="testName">测试名称</param>
        /// <param name="description">测试描述</param>
        /// <returns>测试节点</returns>
        public ExtentTest StartTest(string suiteName, string testName, string description = null)
        {
            if (!_testNodes.ContainsKey(suiteName)) {StartTestSuite(suiteName);}
                
            var suiteNode = _testNodes[suiteName];
            var testNode = suiteNode.CreateNode(testName, description);
            
            string key = $"{suiteName}.{testName}";
            _testNodes[key] = testNode;
            
            return testNode;
        }
        
        /// <summary>
        /// 添加测试结果
        /// </summary>
        /// <param name="suiteName">套件名称</param>
        /// <param name="testName">测试名称</param>
        /// <param name="status">测试状态</param>
        /// <param name="message">测试消息</param>
        /// <param name="duration">测试耗时</param>
        public void AddTestResult(string suiteName, string testName, TestStatus status, string message, TimeSpan duration)
        {
            string key = $"{suiteName}.{testName}";
            ExtentTest testNode;
            
            if (!_testNodes.ContainsKey(key))
            {
                testNode = StartTest(suiteName, testName);
            }
            else
            {
                testNode = _testNodes[key];
            }
            
            Status reportStatus = ConvertStatus(status);
            
            string durationStr = $"耗时: {duration.TotalSeconds:F2}秒";
            if (string.IsNullOrEmpty(message))
            {
                testNode.Log(reportStatus, durationStr);
            }
            else
            {
                testNode.Log(reportStatus, $"{message} - {durationStr}");
            }
        }
        
        /// <summary>
        /// 将NUnit测试状态转换为报告状态
        /// </summary>
        /// <param name="status">NUnit测试状态</param>
        /// <returns>报告状态</returns>
        private Status ConvertStatus(TestStatus status)
        {
            switch (status)
            {
                case TestStatus.Passed:
                    return Status.Pass;
                case TestStatus.Failed:
                    return Status.Fail;
                case TestStatus.Skipped:
                    return Status.Skip;
                case TestStatus.Inconclusive:
                    return Status.Warning;
                default:
                    return Status.Info;
            }
        }
        
        /// <summary>
        /// 生成报告
        /// </summary>
        /// <param name="openReport">是否自动打开报告</param>
        /// <returns>报告文件路径</returns>
        public string GenerateReport(bool openReport = true)
        {
            try
            {
                if (_extentReports != null)
                {
                    _extentReports.Flush();
                }
                
                if (openReport && File.Exists(_reportFilePath))
                {
                    // 打开报告文件
                    System.Diagnostics.Process.Start(_reportFilePath);
                }
                
                return _reportFilePath;
            }
            catch (Exception ex)
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n生成报告出错: {ex.Message}");
                }
                
                return null;
            }
        }
    }
} 