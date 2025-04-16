using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;
using DDNCadAddins.Models;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 测试运行器 - 负责发现和执行所有测试，并生成报告
    /// </summary>
    public class TestRunner : CommandBase
    {
        // 测试特性类
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class TestAttribute : Attribute
        {
            public string Description { get; private set; }
            
            public TestAttribute(string description = null)
            {
                Description = description;
            }
        }
        
        // 测试状态枚举
        public enum TestStatus
        {
            Passed,
            Failed,
            Skipped,
            Warning
        }
        
        // 测试结果类
        public class TestResult
        {
            public string TestName { get; set; }
            public string Description { get; set; }
            public TestStatus Status { get; set; }
            public string Message { get; set; }
            public TimeSpan Duration { get; set; }
            
            public TestResult(string testName, string description = null)
            {
                TestName = testName;
                Description = description;
                Status = TestStatus.Skipped;
                Duration = TimeSpan.Zero;
            }
        }
        
        // 测试上下文
        private class TestContext
        {
            public string CurrentTestName { get; set; }
            public List<TestResult> Results { get; private set; }
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int SkippedTests { get; set; }
            public TimeSpan TotalDuration { get; set; }
            
            public TestContext()
            {
                Results = new List<TestResult>();
                TotalDuration = TimeSpan.Zero;
            }
        }
        
        // 测试夹具接口，用于定义测试前的准备和测试后的清理工作
        /// <summary>
        /// 定义测试固件的接口，用于测试的设置和清理操作
        /// </summary>
        public interface ITestFixture
        {
            /// <summary>
            /// 在所有测试方法执行之前调用一次
            /// </summary>
            void SetUpFixture();

            /// <summary>
            /// 在每个测试方法执行之前调用
            /// </summary>
            void SetUp();

            /// <summary>
            /// 在每个测试方法执行之后调用
            /// </summary>
            void TearDown();

            /// <summary>
            /// 在所有测试方法执行之后调用一次
            /// </summary>
            void TearDownFixture();
        }
        
        // 当前测试上下文
        private TestContext _context;
        
        // HTML报告模板
        private const string HTML_REPORT_TEMPLATE = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf - 8"" />
    <title>DDNCadAddins测试报告</title>
    <style>
        body { font - family: Arial, sans - serif; margin: 20px; }
        h1 { color: #333; }
        .summary { margin: 20px 0; padding: 10px; background - color: #f5f5f5; border - radius: 5px; }
        .test - list { width: 100%; border - collapse: collapse; }
        .test - list th { background - color: #333; color: white; text - align: left; padding: 8px; }
        .test - list td { border: 1px solid #ddd; padding: 8px; }
        .pass { background - color: #dff0d8; }
        .fail { background - color: #f2dede; }
        .skip { background - color: #fcf8e3; }
        .warning { background - color: #fdf7e3; }
    </style>
</head>
<body>
    <h1>DDNCadAddins测试报告</h1>
    <div class=""summary"">
        <h2>摘要</h2>
        <p>总测试数: {0}</p>
        <p>通过数: {1}</p>
        <p>失败数: {2}</p>
        <p>跳过数: {3}</p>
        <p>总耗时: {4:F2}秒</p>
        <p>测试时间: {5}</p>
    </div>
    <h2>测试详情</h2>
    <table class=""test - list"">
        <tr>
            <th>测试名称</th>
            <th>描述</th>
            <th>状态</th>
            <th>时间</th>
            <th>消息</th>
        </tr>
        {6}
    </table>
</body>
</html>
";
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public TestRunner()
            : base()
        {
            CommandName = "RUNUNITTESTS";
            _context = new TestContext();
        }
        
        /// <summary>
        /// 运行所有测试命令
        /// </summary>
        [CommandMethod("RUNUNITTESTS")]
        public void RunUnitTests()
        {
            Execute();
        }
        
        /// <summary>
        /// 执行测试命令
        /// </summary>
        protected override void ExecuteCommand()
        {
            Document doc = GetDocument();
            if (doc == null)
            {
                AcadApp.ShowAlertDialog("没有打开的AutoCAD文档，无法运行测试");
                return;
            }
            
            Editor ed = doc.Editor;
            
            try
            {
                WriteMessage("\n========== 开始单元测试 ==========");
                
                // 记录开始时间
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                
                // 发现并执行所有测试
                DiscoverAndRunTests();
                
                // 停止计时
                totalStopwatch.Stop();
                _context.TotalDuration = totalStopwatch.Elapsed;
                
                // 输出测试结果摘要
                WriteTestSummary();
                
                // 生成报告
                string reportPath = GenerateReport();
                WriteMessage($"\n测试报告已生成: {reportPath}");
                
                WriteMessage("\n========== 单元测试结束 ==========");
            }
            catch (Exception ex)
            {
                WriteMessage($"\n运行测试时出错: {ex.Message}");
                Logger.LogError($"运行测试时出错: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 发现并运行所有测试
        /// </summary>
        private void DiscoverAndRunTests()
        {
            // 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();
            
            // 查找所有测试方法
            var testMethods = new List<MethodInfo>();
            
            foreach (var type in assembly.GetTypes())
            {
                // 查找具有TestAttribute特性的方法
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0);
                
                testMethods.AddRange(methods);
            }
            
            WriteMessage($"\n发现 {testMethods.Count} 个测试方法");
            _context.TotalTests = testMethods.Count;
            
            // 按类分组并运行测试
            var methodsByType = testMethods.GroupBy(m => m.DeclaringType);
            
            foreach (var group in methodsByType)
            {
                RunTestsInType(group.Key, group.ToList());
            }
        }
        
        /// <summary>
        /// 运行某个类型中的所有测试
        /// </summary>
        private void RunTestsInType(Type type, List<MethodInfo> methods)
        {
            WriteMessage($"\n------ 运行类 {type.Name} 中的测试 ------");
            
            // 创建实例
            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                WriteMessage($"\n创建类型 {type.Name} 的实例失败: {ex.Message}");
                Logger.LogError($"创建测试类实例失败: {ex.Message}", ex);
                return;
            }
            
            // 调用SetUp方法（如果存在）
            if (instance is ITestFixture testFixture)
            {
                try
                {
                    testFixture.SetUp();
                }
                catch (Exception ex)
                {
                    WriteMessage($"\n调用 {type.Name}.SetUp() 失败: {ex.Message}");
                    Logger.LogError($"调用测试类SetUp方法失败: {ex.Message}", ex);
                }
            }
            
            // 运行所有测试方法
            foreach (var method in methods)
            {
                RunTestMethod(instance, method);
            }
            
            // 调用TearDown方法（如果存在）
            if (instance is ITestFixture testFixture2)
            {
                try
                {
                    testFixture2.TearDown();
                }
                catch (Exception ex)
                {
                    WriteMessage($"\n调用 {type.Name}.TearDown() 失败: {ex.Message}");
                    Logger.LogError($"调用测试类TearDown方法失败: {ex.Message}", ex);
                }
            }
        }
        
        /// <summary>
        /// 运行单个测试方法
        /// </summary>
        private void RunTestMethod(object instance, MethodInfo method)
        {
            string methodName = method.Name;
            
            // 获取测试特性
            var testAttr = method.GetCustomAttribute<TestAttribute>(true);
            string description = testAttr?.Description ?? "测试方法";
            
            WriteMessage($"\n正在运行测试: {methodName}");
            
            // 创建测试结果
            var result = new TestResult(methodName, description);
            _context.this..Add(result);
            
            Database db = GetDatabase();
            Stopwatch sw = Stopwatch.StartNew();
            
            try
            {
                // 使用事务来执行测试，确保测试不会意外修改数据库
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 调用测试方法
                        method.Invoke(instance, null);
                        
                        // 测试通过
                        result.Status = TestStatus.Passed;
                        _context.PassedTests++;
                        
                        // 不提交事务，回滚所有更改
                    }
                    catch (TargetInvocationException ex)
                    {
                        // 测试失败
                        result.Status = TestStatus.Failed;
                        result.Message = ex.InnerException?.Message ?? ex.Message;
                        _context.FailedTests++;
                        
                        WriteMessage($"\n测试失败: {result.Message}");
                        Logger.LogError($"测试 {methodName} 失败: {result.Message}", ex.InnerException ?? ex);
                    }
                    catch (Exception ex)
                    {
                        // 测试失败
                        result.Status = TestStatus.Failed;
                        result.Message = ex.Message;
                        _context.FailedTests++;
                        
                        WriteMessage($"\n测试失败: {result.Message}");
                        Logger.LogError($"测试 {methodName} 失败: {result.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                // 事务本身失败
                result.Status = TestStatus.Failed;
                result.Message = $"事务处理失败: {ex.Message}";
                _context.FailedTests++;
                
                WriteMessage($"\n测试失败: {result.Message}");
                Logger.LogError($"测试 {methodName} 事务处理失败: {ex.Message}", ex);
            }
            
            sw.Stop();
            result.Duration = sw.Elapsed;
            
            // 输出结果
            string statusText = result.Status == TestStatus.Passed ? "通过" : "失败";
            WriteMessage($"测试 {methodName} {statusText}, 耗时: {result.this..TotalSeconds:F2}秒");
        }
        
        /// <summary>
        /// 输出测试结果摘要
        /// </summary>
        private void WriteTestSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n============ 测试结果摘要 ============");
            sb.AppendLine($"总测试数: {_context.TotalTests}");
            sb.AppendLine($"通过数: {_context.PassedTests}");
            sb.AppendLine($"失败数: {_context.FailedTests}");
            sb.AppendLine($"跳过数: {_context.SkippedTests}");
            sb.AppendLine($"总耗时: {_context.this..TotalSeconds:F2}秒");
            
            // 输出失败的测试
            if (_context.FailedTests > 0)
            {
                sb.AppendLine("\n失败的测试:");
                foreach (var test in _context.this..Where(t => t.Status == TestStatus.Failed))
                {
                    sb.AppendLine($"  {test.TestName}: {test.Message}");
                }
            }
            
            WriteMessage(sb.ToString());
        }
        
        /// <summary>
        /// 生成HTML测试报告
        /// </summary>
        private string GenerateReport()
        {
            try
            {
                // 创建报告目录
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                string reportsDir = Path.Combine(assemblyDirectory, "TestReports");
                
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }
                
                // 生成报告文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string reportPath = Path.Combine(reportsDir, $"TestReport_{timestamp}.html");
                
                // 生成表格内容
                StringBuilder tableRows = new StringBuilder();
                foreach (var test in _context.Results)
                {
                    string statusClass = "";
                    string statusText = "";
                    
                    switch (test.Status)
                    {
                        case TestStatus.Passed:
                            statusClass = "pass";
                            statusText = "通过";
                            break;
                        case TestStatus.Failed:
                            statusClass = "fail";
                            statusText = "失败";
                            break;
                        case TestStatus.Skipped:
                            statusClass = "skip";
                            statusText = "跳过";
                            break;
                        case TestStatus.Warning:
                            statusClass = "warning";
                            statusText = "警告";
                            break;
                    }
                    
                    tableRows.AppendFormat(
                        "<tr class=\"{0}\">" +
                        "<td>{1}</td>" +
                        "<td>{2}</td>" +
                        "<td>{3}</td>" +
                        "<td>{4:F2}秒</td>" +
                        "<td>{5}</td>" +
                        "</tr>",
                        statusClass,
                        test.TestName,
                        test.Description,
                        statusText,
                        test.this..TotalSeconds,
                        test.Message ?? ""
                    );
                }
                
                // 生成完整报告
                string reportContent = string.Format(HTML_REPORT_TEMPLATE,
                    _context.TotalTests,
                    _context.PassedTests,
                    _context.FailedTests,
                    _context.SkippedTests,
                    _context.this..TotalSeconds,
                    DateTime.Now.ToString(),
                    tableRows.ToString()
                );
                
                // 写入文件
                File.WriteAllText(reportPath, reportContent);
                
                // 打开报告
                System.Diagnostics.Process.Start(reportPath);
                
                return reportPath;
            }
            catch (Exception ex)
            {
                WriteMessage($"\n生成报告时出错: {ex.Message}");
                Logger.LogError($"生成测试报告失败: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 对XClip服务进行测试
        /// </summary>
        [Test("测试XClip服务的基本功能")]
        public void TestXClipService()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) {throw new SystemException("No AutoCAD document is open");}

            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 创建一个测试块
                    ObjectId blockId = CreateTestBlock(db, tr);
                    if (blockId.IsNull) {throw new SystemException("Failed to create test block");}

                    // 获取块引用
                    BlockReference blockRef = tr.GetObject(blockId, OpenMode.ForWrite) as BlockReference;
                    if (blockRef == null) {throw new SystemException("Failed to get block reference");}

                    // 创建XClip
                    Point3d[] points = new Point3d[] {
                        new Point3d(0, 0, 0),
                        new Point3d(10, 0, 0),
                        new Point3d(10, 10, 0),
                        new Point3d(0, 10, 0)
                    };

                    // 测试XClip服务
                    IXClipBlockService xClipService = new XClipBlockService();
                    bool success = xClipService.CreatePolygonalXClipBoundary(blockRef, points, true);

                    if (!success) {throw new SystemException("Failed to create XClip boundary");}

                    // 验证XClip是否存在
                    if (!VerifyXClipExists(blockRef, tr)) {throw new SystemException("XClip boundary was not created successfully");}

                    tr.Commit();
                }
                catch (SystemException ex)
                {
                    tr.Abort();
                    throw new SystemException("XClip test failed: " + ex.Message, ex);
                }
            }
        }
        
        /// <summary>
        /// 辅助方法：验证XClip是否存在
        /// </summary>
        private bool VerifyXClipExists(BlockReference blockRef, Transaction tr)
        {
            try
            {
                // 检查块引用是否有XClip
                if (blockRef.ClipboardFormat == 0) {return false;}

                return true;
            }
            catch (SystemException)
            {
                return false;
            }
        }
        
        /// <summary>
        /// 辅助方法：创建测试块
        /// </summary>
        private ObjectId CreateTestBlock(Database db, Transaction tr)
        {
            try
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                // 检查块是否已存在，如果存在则删除
                if (bt.Has("DDNTest_Block"))
                {
                    bt.UpgradeOpen();
                    BlockTableRecord btr = tr.GetObject(bt["DDNTest_Block"], OpenMode.ForWrite) as BlockTableRecord;
                    
                    // 如果不是布局块，可以删除
                    if (!btr.IsLayout)
                    {
                        // 先清除所有实体
                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForWrite) as Entity;
                            if (ent != null)
                            {
                                ent.Erase();
                            }
                        }
                        
                        // 删除块定义
                        btr.Erase();
                    }
                }
                
                // 创建新块
                BlockTableRecord blockDef = new BlockTableRecord();
                blockDef.Name = "DDNTest_Block";
                
                bt.UpgradeOpen();
                ObjectId blockId = bt.Add(blockDef);
                tr.AddNewlyCreatedDBObject(blockDef, true);
                
                // 添加一个圆形
                Circle circle = new Circle(Point3d.Origin, Autodesk.AutoCAD.Geometry.Vector3d.ZAxis, 5.0);
                circle.LayerId = ObjectId.Null;
                blockDef.AppendEntity(circle);
                tr.AddNewlyCreatedDBObject(circle, true);
                
                // 添加一个矩形
                Polyline rect = new Polyline();
                rect.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(-10, -10), 0, 0, 0);
                rect.AddVertexAt(1, new Autodesk.AutoCAD.Geometry.Point2d(10, -10), 0, 0, 0);
                rect.AddVertexAt(2, new Autodesk.AutoCAD.Geometry.Point2d(10, 10), 0, 0, 0);
                rect.AddVertexAt(3, new Autodesk.AutoCAD.Geometry.Point2d(-10, 10), 0, 0, 0);
                rect.Closed = true;
                rect.LayerId = ObjectId.Null;
                blockDef.AppendEntity(rect);
                tr.AddNewlyCreatedDBObject(rect, true);
                
                tr.Commit();
                
                return blockId;
            }
            catch (SystemException ex)
            {
                Logger.LogError($"创建测试块失败: {ex.Message}", ex);
                throw new SystemException("Failed to create test block: " + ex.Message, ex);
            }
        }
        
        /// <summary>
        /// 对XClip查找功能进行测试
        /// </summary>
        [Test("测试查找XClipped图块功能")]
        public void TestFindXClippedBlocks()
        {
            Database db = GetDatabase();
            if (db == null) {throw new InvalidOperationException("没有打开的AutoCAD文档");}
            
            // 创建测试图层
            ObjectId blueLayerId = CreateTestLayer("DDNTest_Blue", 5);  // 蓝色
            
            // 创建测试块
            ObjectId blockId = CreateTestBlock("DDNTest_FindBlock", blueLayerId);
            
            // 创建两个块引用，一个带XClip，一个不带
            ObjectId xclippedBlockId = CreateBlockReference(blockId, new Point3d(20, 0, 0));
            ObjectId normalBlockId = CreateBlockReference(blockId, new Point3d(-20, 0, 0));
            
            // 给第一个块添加XClip
            var xclipService = new XClipBlockService(AcadService, Logger);
            var clipResult = xclipService.CreateRectangularXClipBoundary(
                db, xclippedBlockId, new Point3d(15, -5, 0), new Point3d(25, 5, 0));
            
            if (!clipResult.Success)
                throw new Exception($"添加XClip失败: {clipResult.ErrorMessage}");
            
            // 查找所有XClipped块
            var findResult = xclipService.FindAllXClippedBlocks(db);
            
            // 验证结果
            if (!findResult.Success)
                throw new Exception($"查找XClipped块失败: {findResult.ErrorMessage}");
            
            if (findResult.Data == null) {throw new Exception("结果数据不应为空");}
            
            // 验证找到的数量
            var foundBlocks = findResult.Data as List<Models.XClippedBlockInfo>;
            if (foundBlocks == null) {throw new Exception("返回的数据应该是XClippedBlockInfo列表");}
            
            // 应该至少找到一个块
            if (foundBlocks.Count == 0) {throw new Exception("应至少找到一个XClipped块");}
            
            // 检查是否包含我们的测试块
            bool foundTestBlock = false;
            foreach (var block in foundBlocks)
            {
                if (block.BlockReferenceId == xclippedBlockId)
                {
                    foundTestBlock = true;
                    break;
                }
            }
            
            if (!foundTestBlock) {throw new Exception("未找到我们创建的XClipped块");}
            
            WriteMessage($"成功找到XClipped块，共找到{foundBlocks.Count}个");
        }
        
        /// <summary>
        /// 创建测试图层
        /// </summary>
        private ObjectId CreateTestLayer(string layerName, int colorIndex)
        {
            Database db = GetDatabase();
            if (db == null) {throw new InvalidOperationException("没有活动的AutoCAD文档");}
            
            ObjectId layerId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 获取图层表
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    
                    // 检查图层是否已存在
                    if (lt.Has(layerName))
                    {
                        layerId = lt[layerName];
                    }
                    else
                    {
                        // 创建新图层
                        LayerTableRecord layer = new LayerTableRecord();
                        layer.Name = layerName;
                        layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorIndex);
                        
                        // 添加到图层表
                        lt.UpgradeOpen();
                        layerId = lt.Add(layer);
                        tr.AddNewlyCreatedDBObject(layer, true);
                    }
                    
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"创建测试图层失败: {ex.Message}", ex);
                    throw;
                }
            }
            
            return layerId;
        }
        
        /// <summary>
        /// 创建块引用
        /// </summary>
        private ObjectId CreateBlockReference(ObjectId blockId, Point3d position)
        {
            Database db = GetDatabase();
            if (db == null) {throw new InvalidOperationException("没有活动的AutoCAD文档");}
            
            ObjectId blockRefId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 获取模型空间
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    
                    // 创建块引用
                    BlockReference blockRef = new BlockReference(position, blockId);
                    ms.AppendEntity(blockRef);
                    tr.AddNewlyCreatedDBObject(blockRef, true);
                    
                    blockRefId = blockRef.ObjectId;
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"创建块引用失败: {ex.Message}", ex);
                    throw;
                }
            }
            
            return blockRefId;
        }
        
        /// <summary>
        /// 检查块是否被裁剪
        /// </summary>
        private bool CheckBlockIsClipped(ObjectId blockRefId)
        {
            Database db = GetDatabase();
            if (db == null) {throw new InvalidOperationException("没有活动的AutoCAD文档");}
            
            bool isClipped = false;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                    isClipped = blockRef.IsClipped;
                    
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"检查块裁剪状态失败: {ex.Message}", ex);
                    throw;
                }
            }
            
            return isClipped;
        }
    }

    public abstract class AcadTestFixtureBase : ITestFixture
    {
        protected Document ActiveDocument { get; private set; }
        protected Database Database { get; private set; }
        protected Editor Editor { get; private set; }

        public virtual void SetUpFixture()
        {
            // ... existing code ...
        }

        public virtual void SetUp()
        {
            ActiveDocument = AcadApp.DocumentManager.MdiActiveDocument;
            if (ActiveDocument == null) {throw new SystemException("No AutoCAD document is open");}

            Database = ActiveDocument.Database;
            Editor = ActiveDocument.Editor;
        }

        public virtual void TearDown()
        {
            // ... existing code ...
        }

        public virtual void TearDownFixture()
        {
            // ... existing code ...
        }
    }

    public abstract class AcadNUnitTestBase
    {
        protected Document ActiveDocument { get; private set; }
        protected Database Database { get; private set; }
        protected Editor Editor { get; private set; }

        public virtual void Setup()
        {
            ActiveDocument = AcadApp.DocumentManager.MdiActiveDocument;
            if (ActiveDocument == null) {throw new SystemException("No AutoCAD document is open");}

            Database = ActiveDocument.Database;
            Editor = ActiveDocument.Editor;
        }

        public virtual void Teardown()
        {
            // ... existing code ...
        }
    }
} 