using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using DDNCadAddins.Services;
using AcadException = Autodesk.AutoCAD.Runtime.Exception;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 测试运行器 - 负责发现和执行所有测试，并生成报告
    /// </summary>
    public class TestRunner : CommandBase
    {
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class TestAttribute : Attribute
        {
            public string Description { get; private set; }

            public TestAttribute(string description = null)
            {
                Description = description;
            }
        }

        public enum TestStatus
        {
            Passed,
            Failed,
            Skipped,
            Warning
        }

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
                Message = string.Empty;
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
        
        // 当前测试上下文
        private TestContext _context;
        
        // HTML报告模板
        private const string HTML_REPORT_TEMPLATE = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>测试报告</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #333; }
        .summary { margin: 20px 0; padding: 10px; background-color: #f5f5f5; border-radius: 5px; }
        .summary span { margin-right: 15px; }
        .passed { color: green; }
        .failed { color: red; }
        .skipped { color: orange; }
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }
        th { background-color: #f2f2f2; }
        tr:hover { background-color: #f5f5f5; }
        .status-passed { background-color: #dff0d8; }
        .status-failed { background-color: #f2dede; }
        .status-skipped { background-color: #fcf8e3; }
        .message { font-family: monospace; white-space: pre-wrap; }
    </style>
</head>
<body>
    <h1>DDNCadAddins 测试报告</h1>
    <div class='summary'>
        <span>总测试: {0}</span>
        <span class='passed'>通过: {1}</span>
        <span class='failed'>失败: {2}</span>
        <span class='skipped'>跳过: {3}</span>
        <span>总耗时: {4}</span>
    </div>
    <table>
        <tr>
            <th>测试名称</th>
            <th>描述</th>
            <th>状态</th>
            <th>耗时</th>
            <th>消息</th>
        </tr>
        {5}
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
        /// 检查命令是否可以执行
        /// </summary>
        /// <returns>是否可以执行</returns>
        public bool IsActive()
        {
            var editor = GetEditor();
            if (editor == null)
            {
                MessageService.ShowError("当前没有打开的文档，无法执行命令。");
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// 开始执行命令
        /// </summary>
        public void StartCommand()
        {
            Execute();
        }
        
        // 执行单元测试的命令
        [CommandMethod("RUNUNITTESTS")]
        public void RunUnitTests()
        {
            if (!IsActive())
                return;
                
            ExecuteCommand();
        }
        
        // 执行命令的主体逻辑
        protected override void ExecuteCommand()
        {
            try
            {
                // 初始化测试上下文
                _context = new TestContext();
                
                // 显示开始提示
                var editor = GetCurrentEditor();
                if (editor != null)
                {
                    editor.WriteMessage("\n正在运行测试...\n");
                }
                
                // 发现并运行测试
                DiscoverAndRunTests();
                
                // 显示测试摘要
                WriteTestSummary();
                
                // 生成报告
                string reportContent = GenerateReport();
                
                // 保存报告
                string reportPath = Path.Combine(Path.GetTempPath(), "DDNCadAddins_TestReport.html");
                File.WriteAllText(reportPath, reportContent);
                
                // 打开报告
                Process.Start(reportPath);
                
                // 显示完成提示
                editor = GetCurrentEditor();
                if (editor != null)
                {
                    editor.WriteMessage("\n测试完成。报告已保存至 {0}\n", reportPath);
                }
            }
            catch (SystemException ex)
            {
                var editor = GetCurrentEditor();
                if (editor != null)
                {
                    editor.WriteMessage("\n运行测试时发生错误: {0}\n", ex.Message);
                }
            }
        }
        
        // 发现并运行测试方法
        private void DiscoverAndRunTests()
        {
            // 获取当前程序集中的所有类型
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] types = assembly.GetTypes();
            
            // 查找包含测试方法的类型
            foreach (Type type in types)
            {
                // 检查当前类型中是否有带有Test特性的方法
                var testMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                    .ToList();
                
                if (testMethods.Count > 0)
                {
                    // 如果找到了测试方法，则运行它们
                    RunTestsInType(type, testMethods);
                }
            }
        }
        
        // 运行特定类型中的测试方法
        private void RunTestsInType(Type type, List<MethodInfo> methods)
        {
            // 创建测试类的实例
            object instance = null;
            
            try
            {
                // 尝试创建测试类的实例
                instance = Activator.CreateInstance(type);
                
                // 检查是否实现了ITestFixture接口
                ITestFixture fixture = instance as ITestFixture;
                
                // 如果实现了ITestFixture接口，则调用SetUpFixture方法
                if (fixture != null)
                {
                    fixture.SetUpFixture();
                }
                
                // 运行所有测试方法
                foreach (MethodInfo method in methods)
                {
                    RunTestMethod(instance, method);
                }
                
                // 如果实现了ITestFixture接口，则调用TearDownFixture方法
                if (fixture != null)
                {
                    fixture.TearDownFixture();
                }
            }
            catch (SystemException ex)
            {
                // 创建测试类实例失败
                var editor = GetCurrentEditor();
                if (editor != null)
                {
                    editor.WriteMessage("\n创建测试类 {0} 的实例时发生错误: {1}\n", type.Name, ex.Message);
                }
            }
        }
        
        // 运行单个测试方法
        private void RunTestMethod(object instance, MethodInfo method)
        {
            // 获取测试方法的特性
            TestAttribute attr = method.GetCustomAttribute<TestAttribute>();
            string description = attr?.Description;
            
            // 创建测试结果对象
            TestResult result = new TestResult(method.Name, description);
            
            // 更新当前测试名称
            _context.CurrentTestName = method.Name;
            
            // 开始计时
            Stopwatch stopwatch = Stopwatch.StartNew();
            
            try
            {
                // 检查是否实现了ITestFixture接口
                ITestFixture fixture = instance as ITestFixture;
                
                // 如果实现了ITestFixture接口，则调用SetUp方法
                if (fixture != null)
                {
                    fixture.SetUp();
                }
                
                // 调用测试方法
                method.Invoke(instance, null);
                
                // 如果方法正常执行完毕，则标记为通过
                result.Status = TestStatus.Passed;
                result.Message = "Test passed";
                
                // 如果实现了ITestFixture接口，则调用TearDown方法
                if (fixture != null)
                {
                    fixture.TearDown();
                }
            }
            catch (SystemException ex)
            {
                // 将异常信息记录到测试结果中
                result.Status = TestStatus.Failed;
                
                // 获取内部异常信息
                SystemException innerEx = ex.InnerException as SystemException;
                result.Message = innerEx != null ? 
                    $"{innerEx.GetType().Name}: {innerEx.Message}\n{innerEx.StackTrace}" : 
                    $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                
                // 尝试调用TearDown方法清理资源
                try
                {
                    ITestFixture fixture = instance as ITestFixture;
                    if (fixture != null)
                    {
                        fixture.TearDown();
                    }
                }
                catch { /* Ignore errors in TearDown process */ }
            }
            finally
            {
                // Stop timing
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                
                // Add test result to context
                _context.Results.Add(result);
                
                // Update statistics
                _context.TotalTests++;
                switch (result.Status)
                {
                    case TestStatus.Passed:
                        _context.PassedTests++;
                        break;
                    case TestStatus.Failed:
                        _context.FailedTests++;
                        break;
                    case TestStatus.Skipped:
                        _context.SkippedTests++;
                        break;
                }
                
                // Add to total duration
                _context.TotalDuration += result.Duration;
                
                // Output test result
                string statusStr = result.Status.ToString().ToUpper();
                var editor = GetCurrentEditor();
                if (editor != null)
                {
                    editor.WriteMessage("\n[{0}] {1} ({2})\n", statusStr, method.Name, result.Duration.ToString(@"hh\:mm\:ss\.fff"));
                    if (result.Status == TestStatus.Failed)
                    {
                        editor.WriteMessage("\n{0}\n", result.Message);
                    }
                }
            }
        }
        
        // Get current document editor
        private Editor GetCurrentEditor()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            return doc?.Editor;
        }
        
        // Output test summary
        private void WriteTestSummary()
        {
            var editor = GetCurrentEditor();
            if (editor == null) return;
            
            editor.WriteMessage("\n\n=== Test Summary ===\n");
            editor.WriteMessage($"Total Tests: {_context.TotalTests}\n");
            editor.WriteMessage($"Passed: {_context.PassedTests}\n");
            editor.WriteMessage($"Failed: {_context.FailedTests}\n");
            editor.WriteMessage($"Skipped: {_context.SkippedTests}\n");
            editor.WriteMessage($"Total Duration: {_context.TotalDuration.TotalSeconds:F2} seconds\n");
            editor.WriteMessage("===============\n");
        }
        
        // Generate HTML report
        private string GenerateReport()
        {
            StringBuilder tableRowsBuilder = new StringBuilder();
            
            foreach (var result in _context.Results)
            {
                string statusClass = string.Empty;
                
                switch (result.Status)
                {
                    case TestStatus.Passed:
                        statusClass = "status-passed";
                        break;
                    case TestStatus.Failed:
                        statusClass = "status-failed";
                        break;
                    case TestStatus.Skipped:
                        statusClass = "status-skipped";
                        break;
                }
                
                string tableRow = $@"
                <tr class='{statusClass}'>
                    <td>{result.TestName}</td>
                    <td>{result.Description}</td>
                    <td>{result.Status}</td>
                    <td>{result.Duration.TotalSeconds:F3} seconds</td>
                    <td class='message'>{result.Message}</td>
                </tr>";
                
                tableRowsBuilder.Append(tableRow);
            }
            
            string report = string.Format(
                HTML_REPORT_TEMPLATE, 
                _context.TotalTests,
                _context.PassedTests,
                _context.FailedTests,
                _context.SkippedTests,
                $"{_context.TotalDuration.TotalSeconds:F2} seconds",
                tableRowsBuilder.ToString());
                
            return report;
        }
        
        [Test("Test XClip Service Basic Functionality")]
        public void TestXClipService()
        {
            try
            {
                // Get current active document
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;
                
                // Use transaction to test XClip functionality
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Create test block
                        ObjectId blockId = CreateTestBlock(db, tr);
                        
                        // Create block reference
                        Point3d insertionPoint = new Point3d(0, 0, 0);
                        ObjectId blockRefId = CreateBlockReference(blockId, insertionPoint);
                        
                        // Get block reference object
                        using (BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForWrite) as BlockReference)
                        {
                            // Create clipping region
                            Point3d p1 = new Point3d(insertionPoint.X - 10, insertionPoint.Y - 10, 0);
                            Point3d p2 = new Point3d(insertionPoint.X + 10, insertionPoint.Y + 10, 0);
                            
                            // Use XClipBlockService for clipping
                            IXClipBlockService xclipService = ServiceLocator.GetService<IXClipBlockService>();
                            if (xclipService == null)
                            {
                                throw new SystemException("Unable to get XClipBlockService instance");
                            }
                            
                            // Use correct method according to interface definition
                            bool clipResult = xclipService.ClipBlockWithRectangle(blockRef, p1, p2);
                            
                            if (!clipResult)
                            {
                                throw new SystemException("Unable to create XClip boundary");
                            }
                        }
                        
                        tr.Commit();
                    }
                    catch
                    {
                        tr.Abort();
                        throw;
                    }
                }
            }
            catch (SystemException ex)
            {
                throw new SystemException("Error occurred while testing XClip service", ex);
            }
        }
        
        // Verify if block reference has clipping boundary
        private static bool VerifyXClipExists(BlockReference blockRef, Transaction tr)
        {
            bool isClipped = false;
            
            if (blockRef != null && !blockRef.IsDisposed)
            {
                // Check if ClipBoundary property has value
                if (blockRef.ExtensionDictionary.IsValid)
                {
                    // Open extension dictionary
                    DBDictionary extDict = tr.GetObject(blockRef.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                    if (extDict != null && extDict.Contains("ACAD_FILTER"))
                    {
                        // Has filter dictionary, indicates a clipping boundary
                        isClipped = true;
                    }
                }
            }
            
            return isClipped;
        }
        
        // Create test block
        private static ObjectId CreateTestBlock(Database db, Transaction tr)
        {
            // Open block table
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            
            // Create new block
            string blockName = "TestBlock_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            
            // Check if block name already exists
            if (bt.Has(blockName))
            {
                // If exists, return existing block ID
                return bt[blockName];
            }
            
            // Create new block table record
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            
            // Add graphics to block
            
            // Add circle
            Circle circle = new Circle();
            circle.Center = new Point3d(0, 0, 0);
            circle.Radius = 10;
            btr.AppendEntity(circle);
            
            // Add line
            Line line1 = new Line(new Point3d(-10, 0, 0), new Point3d(10, 0, 0));
            btr.AppendEntity(line1);
            
            Line line2 = new Line(new Point3d(0, -10, 0), new Point3d(0, 10, 0));
            btr.AppendEntity(line2);
            
            // Add text
            DBText text = new DBText();
            text.Position = new Point3d(0, 0, 0);
            text.Height = 2;
            text.TextString = "Test Block";
            btr.AppendEntity(text);
            
            // Open block table for writing
            bt.UpgradeOpen();
            
            // Add block table record to block table
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            
            return btrId;
        }
        
        [Test("Test finding XClipped blocks functionality")]
        public void TestFindXClippedBlocks()
        {
            try
            {
                // Get current active document
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;
                
                // Use transaction for testing
                var testLayer1 = "TEST_LAYER1";
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Create layers for testing
                        ObjectId layer1Id = CreateTestLayer(testLayer1, 1);
                        ObjectId layer2Id = CreateTestLayer("TEST_LAYER2", 2);
                        
                        // Create test blocks
                        ObjectId blockId = CreateTestBlock(db, tr);
                        
                        // Create block references on different layers
                        int spacing = 30;
                        List<ObjectId> blockRefIds = new List<ObjectId>();
                        
                        // Block 1 - Layer 1, with XClip
                        Point3d pos1 = new Point3d(0, 0, 0);
                        ObjectId blockRef1Id = CreateBlockReference(blockId, pos1);
                        BlockReference blockRef1 = tr.GetObject(blockRef1Id, OpenMode.ForWrite) as BlockReference;
                        blockRef1.LayerId = layer1Id;
                        ApplyXClip(blockRef1);
                        blockRefIds.Add(blockRef1Id);
                        
                        // Block 2 - Layer 1, without XClip
                        Point3d pos2 = new Point3d(spacing, 0, 0);
                        ObjectId blockRef2Id = CreateBlockReference(blockId, pos2);
                        BlockReference blockRef2 = tr.GetObject(blockRef2Id, OpenMode.ForWrite) as BlockReference;
                        blockRef2.LayerId = layer1Id;
                        blockRefIds.Add(blockRef2Id);
                        
                        // Block 3 - Layer 2, with XClip
                        Point3d pos3 = new Point3d(0, spacing, 0);
                        ObjectId blockRef3Id = CreateBlockReference(blockId, pos3);
                        BlockReference blockRef3 = tr.GetObject(blockRef3Id, OpenMode.ForWrite) as BlockReference;
                        blockRef3.LayerId = layer2Id;
                        ApplyXClip(blockRef3);
                        blockRefIds.Add(blockRef3Id);
                        
                        // Block 4 - Layer 2, without XClip
                        Point3d pos4 = new Point3d(spacing, spacing, 0);
                        ObjectId blockRef4Id = CreateBlockReference(blockId, pos4);
                        BlockReference blockRef4 = tr.GetObject(blockRef4Id, OpenMode.ForWrite) as BlockReference;
                        blockRef4.LayerId = layer2Id;
                        blockRefIds.Add(blockRef4Id);
                        
                        // Commit transaction to make sure all objects are created
                        tr.Commit();
                    }
                    catch
                    {
                        tr.Abort();
                        throw;
                    }
                }
                
                // Create a new transaction for finding XClipped blocks
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Get XClip service
                        IXClipBlockService xclipService = ServiceLocator.GetService<IXClipBlockService>();
                        if (xclipService == null)
                        {
                            throw new SystemException("Unable to get XClipBlockService instance");
                        }
                        
                        // Test finding all XClipped blocks
                        var result = xclipService.FindAllXClippedBlocks(db);
                        if (!result.Success)
                        {
                            throw new SystemException("Error finding XClipped blocks: " + result.Message);
                        }
                        
                        // Verify we found exactly 2 XClipped blocks
                        List<XClippedBlockInfo> xclippedBlocks = result.Data;
                        if (xclippedBlocks.Count != 2)
                        {
                            throw new SystemException($"Expected to find 2 XClipped blocks, but found {xclippedBlocks.Count}");
                        }
                        
                        // Test finding XClipped blocks by layer
                        LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                        ObjectId layer1Id = lt[testLayer1];
                        
                        var layerResult = xclipService.FindXClippedBlocksByLayer(testLayer1);
                        if (!layerResult.Any())
                        {
                            throw new SystemException("Error finding XClipped blocks by layer: " + testLayer1);
                        }
                        
                        // Verify we found exactly 1 XClipped block on TEST_LAYER1
                        // 将ObjectId列表转换为XClippedBlockInfo列表
                        List<XClippedBlockInfo> layerXclippedBlocks = new List<XClippedBlockInfo>();
                        // 使用事务创建XClippedBlockInfo对象
                        foreach (ObjectId objId in layerResult)
                        {
                            BlockReference blockRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                            if (blockRef != null)
                            {
                                layerXclippedBlocks.Add(new XClippedBlockInfo
                                {
                                    BlockReferenceId = objId,
                                    BlockName = blockRef.Name,
                                    DetectionMethod = "ByLayer",
                                    NestLevel = 0
                                });
                            }
                        }
                        
                        if (layerXclippedBlocks.Count != 1)
                        {
                            throw new SystemException($"Expected to find 1 XClipped block on TEST_LAYER1, but found {layerXclippedBlocks.Count}");
                        }
                        
                        tr.Commit();
                    }
                    catch
                    {
                        tr.Abort();
                        throw;
                    }
                }
            }
            catch (SystemException ex)
            {
                throw new SystemException("Error in TestFindXClippedBlocks", ex);
            }
        }
        
        // 创建测试图层
        private static ObjectId CreateTestLayer(string layerName, int colorIndex)
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            
            // 使用事务创建图层
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 打开图层表
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                
                // 检查是否已存在同名图层
                if (lt.Has(layerName))
                {
                    // 如果已存在，则返回现有图层的ID
                    ObjectId layerId = lt[layerName];
                    tr.Commit();
                    return layerId;
                }
                
                // 创建新的图层表记录
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;
                ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorIndex);
                
                // 打开图层表进行写入
                lt.UpgradeOpen();
                
                // 将图层表记录添加到图层表
                ObjectId ltrId = lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
                
                tr.Commit();
                
                return ltrId;
            }
        }
        
        // 创建块参照
        private static ObjectId CreateBlockReference(ObjectId blockId, Point3d position)
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            
            // 使用事务创建块参照
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 打开模型空间
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;
                
                // 创建块参照
                BlockReference blockRef = new BlockReference(position, blockId);
                
                // 添加到模型空间
                ObjectId blockRefId = ms.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                tr.Commit();
                
                return blockRefId;
            }
        }
        
        // 检查块是否被裁剪
        private static bool CheckBlockIsClipped(ObjectId blockRefId)
        {
            bool isClipped = false;
            
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                
                if (blockRef != null && !blockRef.IsDisposed)
                {
                    // 检查 ClipBoundary 属性是否有值
                    if (blockRef.ExtensionDictionary.IsValid)
                    {
                        // 打开扩展字典
                        DBDictionary extDict = tr.GetObject(blockRef.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                        if (extDict != null && extDict.Contains("ACAD_FILTER"))
                        {
                            // 有过滤器字典，表示有裁剪边界
                            isClipped = true;
                        }
                    }
                }
                
                tr.Commit();
            }
            
            return isClipped;
        }

        // 显示测试进度
        private void WriteTestProgress(string testName, TestStatus status, string message)
        {
            var editor = this.GetEditor();
            
            // 根据状态使用不同颜色
            string statusText = string.Empty;
            
            switch (status)
            {
                case TestStatus.Passed:
                    statusText = "通过";
                    editor.WriteMessage("\n[{0}] {1}: {2}\n", testName, statusText, message);
                    break;
                case TestStatus.Failed:
                    statusText = "失败";
                    editor.WriteMessage("\n[{0}] {1}: {2}\n", testName, statusText, message);
                    break;
                case TestStatus.Skipped:
                    statusText = "跳过";
                    editor.WriteMessage("\n[{0}] {1}: {2}\n", testName, statusText, message);
                    break;
                case TestStatus.Warning:
                    statusText = "警告";
                    editor.WriteMessage("\n[{0}] {1}: {2}\n", testName, statusText, message);
                    break;
            }
        }

        private static void ApplyXClip(BlockReference blockRef)
        {
            if (blockRef == null || blockRef.IsDisposed)
                return;
                
            // 获取块的几何边界
            Extents3d extents = blockRef.GeometricExtents;
                
            // 创建稍微大一点的裁剪边界（边界框的110%）
            double margin = 0.1; // 10%的边距
            double width = extents.MaxPoint.X - extents.MinPoint.X;
            double height = extents.MaxPoint.Y - extents.MinPoint.Y;
                
            Point3d minPoint = new Point3d(
                extents.MinPoint.X - width * margin,
                extents.MinPoint.Y - height * margin,
                0
            );
                
            Point3d maxPoint = new Point3d(
                extents.MaxPoint.X + width * margin,
                extents.MaxPoint.Y + height * margin,
                0
            );
            
            // 获取ClipBlockWithRectangle服务并应用裁剪
            IXClipBlockService xclipService = ServiceLocator.GetService<IXClipBlockService>();
            if (xclipService != null)
            {
                xclipService.ClipBlockWithRectangle(blockRef, minPoint, maxPoint);
            }
        }
    }

    public abstract class AcadTestFixtureBase : ITestFixture
    {
        protected Document ActiveDocument { get; private set; }
        protected Database Database { get; private set; }
        protected Editor Editor { get; private set; }

        public virtual void SetUpFixture()
        {
            // 在所有测试方法执行之前调用一次
        }

        public virtual void SetUp()
        {
            ActiveDocument = AcadApp.DocumentManager.MdiActiveDocument;
            if (ActiveDocument == null)
                throw new SystemException("No AutoCAD document is open");

            Database = ActiveDocument.Database;
            Editor = ActiveDocument.Editor;
        }

        public virtual void TearDown()
        {
            // 在每个测试方法执行之后调用
        }

        public virtual void TearDownFixture()
        {
            // 在所有测试方法执行之后调用一次
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
            if (ActiveDocument == null)
                throw new SystemException("No AutoCAD document is open");

            Database = ActiveDocument.Database;
            Editor = ActiveDocument.Editor;
        }

        public virtual void Teardown()
        {
            // 清理资源
        }
    }
} 
