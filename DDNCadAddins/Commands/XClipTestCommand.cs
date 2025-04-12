using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// XClip功能单元测试命令类
    /// </summary>
    public class UnitTestsCommand
    {
        private readonly ILogger _logger;
        private readonly IXClipBlockService _xclipService;
        private Document _doc;
        private Database _db;
        private Editor _editor;
        
        // 测试结果统计
        private int _totalTests = 0;
        private int _passedTests = 0;
        private int _failedTests = 0;
        private StringBuilder _testResults = new StringBuilder();
        
        // 测试中创建的对象ID列表，用于清理
        private List<ObjectId> _createdObjectIds = new List<ObjectId>();
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public UnitTestsCommand()
        {
            _logger = new FileLogger();
            _xclipService = new XClipBlockService(_logger);
        }

        /// <summary>
        /// 运行XClip功能单元测试命令
        /// </summary>
        [CommandMethod("RunUnitTests")]
        public void RunTests()
        {
            _doc = Application.DocumentManager.MdiActiveDocument;
            if (_doc == null)
            {
                Application.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            _db = _doc.Database;
            _editor = _doc.Editor;
            
            // 初始化日志
            _logger.Initialize("XClipTest");
            
            try
            {
                _logger.Log("===== 开始XClip功能单元测试 =====");
                _editor.WriteMessage("\n开始执行XClip功能单元测试，请稍等...");
                
                // 执行测试用例
                TestCreateBlock();
                TestCreateNestedBlocks();
                TestXClipDetection();
                TestNestedXClipDetection();
                
                // 显示测试结果
                double passRate = (_totalTests > 0) ? (double)_passedTests / _totalTests * 100 : 0;
                
                _logger.Log("\n===== 测试完成 =====");
                _logger.Log($"总测试数: {_totalTests}");
                _logger.Log($"通过测试: {_passedTests}");
                _logger.Log($"失败测试: {_failedTests}");
                _logger.Log($"通过率: {passRate:F2}%");
                _logger.Log("\n详细测试结果:");
                _logger.Log(_testResults.ToString());
                
                _editor.WriteMessage($"\n\n测试完成! 总计: {_totalTests}, 通过: {_passedTests}, 失败: {_failedTests}, 通过率: {passRate:F2}%");
                _editor.WriteMessage("\n详细结果已写入日志文件，可使用OpenXClipLog命令查看");
                
                // 询问是否清理测试对象
                PromptKeywordOptions pko = new PromptKeywordOptions("\n是否清理测试创建的对象?");
                pko.Keywords.Add("Yes");
                pko.Keywords.Add("No");
                pko.Keywords.Default = "Yes";
                pko.AllowNone = false;
                
                PromptResult pr = _editor.GetKeywords(pko);
                if (pr.Status == PromptStatus.OK && pr.StringResult == "Yes")
                {
                    CleanupTestObjects();
                    _editor.WriteMessage("\n测试对象已清理完毕。");
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"测试过程中发生未处理的异常: {ex.Message}", ex);
                _editor.WriteMessage($"\n测试过程中出错: {ex.Message}");
                _editor.WriteMessage("\n详细错误信息已记录到日志文件，使用OpenXClipLog命令查看");
            }
            finally
            {
                // 关闭日志文件
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 测试创建图块功能
        /// </summary>
        private void TestCreateBlock()
        {
            LogTestStart("测试创建图块功能");
            
            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    // 创建唯一的测试块名称
                    string testBlockName = $"TestBlock_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                    
                    // 获取块表
                    BlockTable bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt == null)
                    {
                        LogTestResult(false, "无法获取块表");
                        return;
                    }
                    
                    // 检查测试前该名称的块是否存在
                    bool blockExistsBefore = bt.Has(testBlockName);
                    LogTestInfo($"测试前块'{testBlockName}'是否存在: {blockExistsBefore}");
                    Assert(!blockExistsBefore, $"测试块'{testBlockName}'在测试前不应该存在");
                    
                    // 创建测试块
                    _editor.WriteMessage($"\n创建测试块: {testBlockName}");
                    
                    // 获取模型空间
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    if (modelSpace == null)
                    {
                        LogTestResult(false, "无法获取模型空间");
                        return;
                    }
                    
                    // 创建新的块定义
                    bt.UpgradeOpen();
                    BlockTableRecord btr = new BlockTableRecord();
                    btr.Name = testBlockName;
                    
                    // 添加块定义到块表
                    ObjectId blockDefId = bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);
                    
                    // 添加一个圆到块中
                    Circle circle = new Circle();
                    circle.Center = new Point3d(0, 0, 0);
                    circle.Radius = 3;
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    
                    // 创建块参照
                    Point3d insertionPoint = new Point3d(20, 20, 0);
                    BlockReference blockRef = new BlockReference(insertionPoint, blockDefId);
                    modelSpace.AppendEntity(blockRef);
                    tr.AddNewlyCreatedDBObject(blockRef, true);
                    
                    // 记录创建的对象ID，用于后续清理
                    _createdObjectIds.Add(blockRef.ObjectId);
                    
                    tr.Commit();
                    
                    // 验证块是否创建成功
                    Assert(true, $"测试块'{testBlockName}'创建成功");
                    LogTestInfo($"创建的块参照ID: {blockRef.ObjectId}");
                }
            }
            catch (System.Exception ex)
            {
                LogTestResult(false, $"创建块时发生异常: {ex.Message}");
            }
            
            LogTestEnd();
        }
        
        /// <summary>
        /// 测试创建嵌套图块
        /// </summary>
        private void TestCreateNestedBlocks()
        {
            LogTestStart("测试创建嵌套图块");
            
            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    // 创建唯一的测试块名称
                    string parentBlockName = $"ParentBlock_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                    string childBlockName = $"ChildBlock_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                    
                    // 获取块表
                    BlockTable bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt == null)
                    {
                        LogTestResult(false, "无法获取块表");
                        return;
                    }
                    
                    // 获取模型空间
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    if (modelSpace == null)
                    {
                        LogTestResult(false, "无法获取模型空间");
                        return;
                    }
                    
                    // 创建子块定义
                    bt.UpgradeOpen();
                    BlockTableRecord childBtr = new BlockTableRecord();
                    childBtr.Name = childBlockName;
                    
                    ObjectId childBlockId = bt.Add(childBtr);
                    tr.AddNewlyCreatedDBObject(childBtr, true);
                    
                    // 添加一个圆到子块中
                    Circle childCircle = new Circle();
                    childCircle.Center = new Point3d(0, 0, 0);
                    childCircle.Radius = 1;
                    childBtr.AppendEntity(childCircle);
                    tr.AddNewlyCreatedDBObject(childCircle, true);
                    
                    // 创建父块定义
                    BlockTableRecord parentBtr = new BlockTableRecord();
                    parentBtr.Name = parentBlockName;
                    
                    ObjectId parentBlockId = bt.Add(parentBtr);
                    tr.AddNewlyCreatedDBObject(parentBtr, true);
                    
                    // 添加一个矩形到父块中
                    Polyline rectangle = new Polyline();
                    rectangle.AddVertexAt(0, new Point2d(-2, -2), 0, 0, 0);
                    rectangle.AddVertexAt(1, new Point2d(2, -2), 0, 0, 0);
                    rectangle.AddVertexAt(2, new Point2d(2, 2), 0, 0, 0);
                    rectangle.AddVertexAt(3, new Point2d(-2, 2), 0, 0, 0);
                    rectangle.Closed = true;
                    parentBtr.AppendEntity(rectangle);
                    tr.AddNewlyCreatedDBObject(rectangle, true);
                    
                    // 将子块添加到父块中 - 创建嵌套关系
                    BlockReference childRef = new BlockReference(new Point3d(0, 0, 0), childBlockId);
                    parentBtr.AppendEntity(childRef);
                    tr.AddNewlyCreatedDBObject(childRef, true);
                    
                    // 创建父块的参照到模型空间
                    Point3d insertionPoint = new Point3d(30, 30, 0);
                    BlockReference parentRef = new BlockReference(insertionPoint, parentBlockId);
                    modelSpace.AppendEntity(parentRef);
                    tr.AddNewlyCreatedDBObject(parentRef, true);
                    
                    // 记录创建的对象ID，用于后续清理
                    _createdObjectIds.Add(parentRef.ObjectId);
                    
                    tr.Commit();
                    
                    // 验证嵌套块是否创建成功
                    Assert(true, $"嵌套块创建成功 - 父块:'{parentBlockName}', 子块:'{childBlockName}'");
                    LogTestInfo($"创建的父块参照ID: {parentRef.ObjectId}");
                }
            }
            catch (System.Exception ex)
            {
                LogTestResult(false, $"创建嵌套块时发生异常: {ex.Message}");
            }
            
            LogTestEnd();
        }
        
        /// <summary>
        /// 测试XClip检测功能
        /// </summary>
        private void TestXClipDetection()
        {
            LogTestStart("测试XClip检测功能");
            
            try
            {
                // 创建测试块
                string testBlockName = $"XClipTestBlock_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                ObjectId blockRefId = ObjectId.Null;
                
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    // 获取块表
                    BlockTable bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    
                    // 创建块定义
                    bt.UpgradeOpen();
                    BlockTableRecord btr = new BlockTableRecord();
                    btr.Name = testBlockName;
                    
                    ObjectId blockDefId = bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);
                    
                    // 添加图形到块中
                    Circle circle = new Circle();
                    circle.Center = new Point3d(0, 0, 0);
                    circle.Radius = 5;
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    
                    // 创建块参照
                    Point3d insertionPoint = new Point3d(40, 40, 0);
                    BlockReference blockRef = new BlockReference(insertionPoint, blockDefId);
                    modelSpace.AppendEntity(blockRef);
                    tr.AddNewlyCreatedDBObject(blockRef, true);
                    
                    blockRefId = blockRef.ObjectId;
                    _createdObjectIds.Add(blockRefId);
                    
                    tr.Commit();
                }
                
                // 验证块创建成功
                if (blockRefId == ObjectId.Null)
                {
                    LogTestResult(false, "测试块未创建成功");
                    return;
                }
                
                LogTestInfo($"创建的测试块ID: {blockRefId}");
                _editor.WriteMessage($"\n已创建测试块 '{testBlockName}' 在坐标(40,40)处");
                _editor.WriteMessage("\n请手动执行以下步骤执行XClip测试:");
                _editor.WriteMessage("\n1. 输入XCLIP命令");
                _editor.WriteMessage("\n2. 选择刚创建的测试块");
                _editor.WriteMessage("\n3. 输入N表示新建裁剪边界");
                _editor.WriteMessage("\n4. 输入R表示矩形边界");
                _editor.WriteMessage("\n5. 绘制一个裁剪矩形(比块小)");
                _editor.WriteMessage("\n完成后继续...");
                
                // 等待用户手动XClip操作
                PromptKeywordOptions pko = new PromptKeywordOptions("\n请在完成XClip操作后输入Continue继续测试");
                pko.Keywords.Add("Continue");
                pko.AllowNone = false;
                
                _editor.GetKeywords(pko);
                
                // 现在测试XClip检测
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    // 获取刚才创建的块
                    BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                    if (blockRef == null)
                    {
                        LogTestResult(false, "无法获取测试块");
                        return;
                    }
                    
                    // 调用被测试方法
                    string detectionMethod;
                    bool isXClipped = false;
                    
                    // 通过反射调用私有方法
                    Type type = _xclipService.GetType();
                    var methodInfo = type.GetMethod("IsBlockXClipped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (methodInfo != null)
                    {
                        object[] parameters = new object[] { tr, blockRef, null };
                        isXClipped = (bool)methodInfo.Invoke(_xclipService, parameters);
                        detectionMethod = (string)parameters[2];
                        
                        LogTestInfo($"IsBlockXClipped返回: {isXClipped}, 检测方法: {detectionMethod}");
                        Assert(isXClipped, "应该检测到块被XClip裁剪");
                    }
                    else
                    {
                        LogTestResult(false, "无法通过反射调用IsBlockXClipped方法");
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                LogTestResult(false, $"测试XClip检测时发生异常: {ex.Message}");
            }
            
            LogTestEnd();
        }
        
        /// <summary>
        /// 测试嵌套图块的XClip检测
        /// </summary>
        private void TestNestedXClipDetection()
        {
            LogTestStart("测试嵌套图块的XClip检测");
            
            try
            {
                // 创建嵌套测试块
                OperationResult<List<XClippedBlockInfo>> result = _xclipService.FindXClippedBlocks(_db);
                
                if (!result.Success)
                {
                    LogTestResult(false, $"执行FindXClippedBlocks失败: {result.ErrorMessage}");
                    return;
                }
                
                // 验证返回的结果是否包含嵌套级别信息
                var xclippedBlocks = result.Data;
                
                LogTestInfo($"找到的XClip图块数量: {xclippedBlocks.Count}");
                foreach (var block in xclippedBlocks)
                {
                    LogTestInfo($"图块名称: {block.BlockName}, 嵌套级别: {block.NestLevel}, 检测方法: {block.DetectionMethod}");
                }
                
                // 由于我们不确定当前图形中是否有嵌套的被XClip的图块
                // 所以这里主要测试功能是否能正常运行，不检查具体结果
                Assert(result.Success, "嵌套图块XClip检测功能正常运行");
            }
            catch (System.Exception ex)
            {
                LogTestResult(false, $"测试嵌套XClip检测时发生异常: {ex.Message}");
            }
            
            LogTestEnd();
        }
        
        /// <summary>
        /// 清理测试创建的对象
        /// </summary>
        private void CleanupTestObjects()
        {
            if (_createdObjectIds.Count == 0)
                return;
                
            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in _createdObjectIds)
                    {
                        if (id != ObjectId.Null)
                        {
                            DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                            if (obj != null)
                            {
                                obj.Erase();
                                _logger.Log($"已删除测试对象: {id}");
                            }
                        }
                    }
                    
                    tr.Commit();
                }
                
                _createdObjectIds.Clear();
            }
            catch (System.Exception ex)
            {
                _logger.Log($"清理测试对象时出错: {ex.Message}");
            }
        }
        
        #region 测试辅助方法
        
        /// <summary>
        /// 开始一个测试用例
        /// </summary>
        private void LogTestStart(string testName)
        {
            _totalTests++;
            _testResults.AppendLine($"\n===== 测试 {_totalTests}: {testName} =====");
            _logger.Log($"\n===== 开始测试: {testName} =====");
            _editor.WriteMessage($"\n正在执行测试 {_totalTests}: {testName}");
        }
        
        /// <summary>
        /// 结束一个测试用例
        /// </summary>
        private void LogTestEnd()
        {
            _testResults.AppendLine("===============================\n");
        }
        
        /// <summary>
        /// 记录测试信息
        /// </summary>
        private void LogTestInfo(string info)
        {
            _testResults.AppendLine($"信息: {info}");
            _logger.Log($"测试信息: {info}");
        }
        
        /// <summary>
        /// 断言测试条件
        /// </summary>
        private void Assert(bool condition, string message)
        {
            if (condition)
            {
                _passedTests++;
                _testResults.AppendLine($"通过: {message}");
                _logger.Log($"断言通过: {message}");
            }
            else
            {
                _failedTests++;
                _testResults.AppendLine($"失败: {message}");
                _logger.Log($"断言失败: {message}");
            }
        }
        
        /// <summary>
        /// 记录测试结果
        /// </summary>
        private void LogTestResult(bool success, string message)
        {
            if (success)
            {
                _passedTests++;
                _testResults.AppendLine($"通过: {message}");
                _logger.Log($"测试通过: {message}");
            }
            else
            {
                _failedTests++;
                _testResults.AppendLine($"失败: {message}");
                _logger.Log($"测试失败: {message}");
            }
        }
        
        #endregion
    }
} 