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
                catch { /* 忽略TearDown过程中的错误 */ }
            }
            finally
            {
                // 停止计时
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                
                // 将测试结果添加到上下文中
                _context.Results.Add(result);
                
                // 更新统计信息
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
                
                // 累加总耗时
                _context.TotalDuration += result.Duration;
                
                // 输出测试结果
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
        
        // 获取当前文档的编辑器
        private Editor GetCurrentEditor()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            return doc?.Editor;
        }
        
        // 输出测试摘要
        private void WriteTestSummary()
        {
            var editor = GetCurrentEditor();
            if (editor == null) return;
            
            editor.WriteMessage("\n\n=== 测试摘要 ===\n");
            editor.WriteMessage($"总测试: {_context.TotalTests}\n");
            editor.WriteMessage($"通过: {_context.PassedTests}\n");
            editor.WriteMessage($"失败: {_context.FailedTests}\n");
            editor.WriteMessage($"跳过: {_context.SkippedTests}\n");
            editor.WriteMessage($"总耗时: {_context.TotalDuration.TotalSeconds:F2}秒\n");
            editor.WriteMessage("===============\n");
        }
        
        // 生成HTML报告
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
                    <td>{result.Duration.TotalSeconds:F3}秒</td>
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
                $"{_context.TotalDuration.TotalSeconds:F2}秒",
                tableRowsBuilder.ToString());
                
            return report;
        }
        
        [Test("测试XClip服务基本功能")]
        public void TestXClipService()
        {
            try
            {
                // 获取当前活动文档
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;
                
                // 使用事务测试XClip功能
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 创建测试图块
                        ObjectId blockId = CreateTestBlock(db, tr);
                        
                        // 创建块参照
                        Point3d insertionPoint = new Point3d(0, 0, 0);
                        ObjectId blockRefId = CreateBlockReference(blockId, insertionPoint);
                        
                        // 获取块参照对象
                        using (BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForWrite) as BlockReference)
                        {
                            // 创建裁剪区域
                            Point3d p1 = new Point3d(insertionPoint.X - 10, insertionPoint.Y - 10, 0);
                            Point3d p2 = new Point3d(insertionPoint.X + 10, insertionPoint.Y + 10, 0);
                            
                            // 使用XClipBlockService进行裁剪
                            IXClipBlockService xclipServiceInterface = ServiceLocator.GetService<IXClipBlockService>();
                            XClipBlockService xclipService = xclipServiceInterface as XClipBlockService;
                            if (xclipService == null)
                            {
                                throw new SystemException("无法获取XClipBlockService实例");
                            }
                            
                            bool clipResult = xclipService.ClipBlockWithRectangle(blockRef, p1, p2);
                            
                            if (!clipResult)
                            {
                                throw new SystemException("无法创建XClip裁剪边界");
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
                throw new SystemException("测试XClip服务时发生错误", ex);
            }
        }
        
        // 验证块参照是否有裁剪边界
        private static bool VerifyXClipExists(BlockReference blockRef, Transaction tr)
        {
            bool isClipped = false;
            
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
            
            return isClipped;
        }
        
        // 创建测试图块
        private static ObjectId CreateTestBlock(Database db, Transaction tr)
        {
            // 打开块表
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            
            // 创建新块
            string blockName = "TestBlock_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            
            // 检查是否已存在同名块
            if (bt.Has(blockName))
            {
                // 如果已存在，则返回现有块的ID
                return bt[blockName];
            }
            
            // 创建新的块表记录
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            
            // 向块中添加图形
            
            // 添加圆
            Circle circle = new Circle();
            circle.Center = new Point3d(0, 0, 0);
            circle.Radius = 10;
            btr.AppendEntity(circle);
            
            // 添加线
            Line line1 = new Line(new Point3d(-10, 0, 0), new Point3d(10, 0, 0));
            btr.AppendEntity(line1);
            
            Line line2 = new Line(new Point3d(0, -10, 0), new Point3d(0, 10, 0));
            btr.AppendEntity(line2);
            
            // 添加文本
            DBText text = new DBText();
            text.Position = new Point3d(0, 0, 0);
            text.Height = 2;
            text.TextString = "Test Block";
            btr.AppendEntity(text);
            
            // 打开块表进行写入
            bt.UpgradeOpen();
            
            // 将块表记录添加到块表
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            
            return btrId;
        }
        
        [Test("测试查找XClipped图块功能")]
        public void TestFindXClippedBlocks()
        {
            // 获取当前活动文档
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            // 使用事务创建测试块并进行裁剪测试
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 创建测试图层
                    string testLayerName = "TestLayer_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    ObjectId layerId = CreateTestLayer(testLayerName, 1); // 红色图层
                    
                    // 创建测试图块
                    ObjectId blockId = CreateTestBlock(db, tr);
                    
                    // 创建多个块参照
                    Point3d insertionPoint1 = new Point3d(0, 0, 0);
                    ObjectId blockRefId1 = CreateBlockReference(blockId, insertionPoint1);
                    
                    Point3d insertionPoint2 = new Point3d(30, 0, 0);
                    ObjectId blockRefId2 = CreateBlockReference(blockId, insertionPoint2);
                    
                    Point3d insertionPoint3 = new Point3d(0, 30, 0);
                    ObjectId blockRefId3 = CreateBlockReference(blockId, insertionPoint3);
                    
                    // 设置块参照的图层
                    using (BlockReference blockRef = tr.GetObject(blockRefId1, OpenMode.ForWrite) as BlockReference)
                    {
                        blockRef.LayerId = layerId;
                    }
                    
                    using (BlockReference blockRef = tr.GetObject(blockRefId2, OpenMode.ForWrite) as BlockReference)
                    {
                        blockRef.LayerId = layerId;
                    }
                    
                    // 只裁剪第一个和第二个块参照
                    using (BlockReference blockRef1 = tr.GetObject(blockRefId1, OpenMode.ForWrite) as BlockReference)
                    {
                        Point3d p1 = new Point3d(-5, -5, 0);
                        Point3d p2 = new Point3d(5, 5, 0);
                        
                        // 通过ServiceLocator获取XClipBlockService实例
                        IXClipBlockService xclipServiceInterface = ServiceLocator.GetService<IXClipBlockService>();
                        XClipBlockService xclipService = xclipServiceInterface as XClipBlockService;
                        if (xclipService == null)
                        {
                            throw new SystemException("无法获取XClipBlockService实例");
                        }
                        xclipService.ClipBlockWithRectangle(blockRef1, p1, p2);
                    }
                    
                    using (BlockReference blockRef2 = tr.GetObject(blockRefId2, OpenMode.ForWrite) as BlockReference)
                    {
                        Point3d p1 = new Point3d(25, -5, 0);
                        Point3d p2 = new Point3d(35, 5, 0);
                        
                        // 使用同一个服务实例
                        IXClipBlockService xclipServiceInterface = ServiceLocator.GetService<IXClipBlockService>();
                        XClipBlockService xclipService = xclipServiceInterface as XClipBlockService;
                        if (xclipService == null)
                        {
                            throw new SystemException("无法获取XClipBlockService实例");
                        }
                        xclipService.ClipBlockWithRectangle(blockRef2, p1, p2);
                    }
                    
                    // 使用XClipBlockService查找被裁剪的块参照
                    IXClipBlockService xclipServiceInterface = ServiceLocator.GetService<IXClipBlockService>();
                    XClipBlockService xclipService = xclipServiceInterface as XClipBlockService;
                    if (xclipService == null)
                    {
                        throw new SystemException("无法获取XClipBlockService实例");
                    }
                    List<ObjectId> clippedBlocks = xclipService.FindXClippedBlocks(db, ed);
                    
                    // 验证结果
                    if (clippedBlocks.Count < 2)
                    {
                        throw new SystemException($"查找结果数量不正确，期望至少2个，实际{clippedBlocks.Count}个");
                    }
                    
                    // 检查第一个和第二个块参照是否在结果中
                    bool foundRef1 = clippedBlocks.Contains(blockRefId1);
                    bool foundRef2 = clippedBlocks.Contains(blockRefId2);
                    bool foundRef3 = clippedBlocks.Contains(blockRefId3);
                    
                    if (!foundRef1 || !foundRef2)
                    {
                        throw new SystemException("未找到所有被裁剪的块参照");
                    }
                    
                    if (foundRef3)
                    {
                        throw new SystemException("查找结果中包含未被裁剪的块参照");
                    }
                    
                    // 检查按图层过滤功能
                    List<ObjectId> clippedBlocksByLayer = null;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // 获取当前图层
                        string currentLayerName = SymbolUtilityServices.GetNameOfCurrentLayer(db);
                        
                        // 使用XClipBlockService查找指定图层上被裁剪的块参照
                        IXClipBlockService xclipServiceInterface = ServiceLocator.GetService<IXClipBlockService>();
                        XClipBlockService xclipService = xclipServiceInterface as XClipBlockService;
                        if (xclipService == null)
                        {
                            throw new SystemException("无法获取XClipBlockService实例");
                        }
                        clippedBlocksByLayer = xclipService.FindXClippedBlocksByLayer(db, ed, currentLayerName);
                        
                        tr.Commit();
                    }
                    
                    if (clippedBlocksByLayer.Count != 2)
                    {
                        throw new SystemException($"按图层过滤结果数量不正确，期望2个，实际{clippedBlocksByLayer.Count}个");
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
    }

    public interface ITestFixture
    {
        void SetUpFixture();
        void SetUp();
        void TearDown();
        void TearDownFixture();
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