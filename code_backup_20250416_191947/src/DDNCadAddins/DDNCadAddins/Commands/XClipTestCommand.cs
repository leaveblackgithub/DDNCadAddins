using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using DDNCadAddins.Services;
using DDNCadAddins.Commands;
using DDNCadAddins.Commands.XClipTest;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// XClip图块测试命令类 - 实现XClip测试相关的CAD命令
    /// </summary>
    public class XClipTestCommand : TestCommandBase
    {
        private readonly XClipTestCreator _testCreator;
        private readonly XClipTestValidator _testValidator;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public XClipTestCommand()
            : base()
        {
            // 创建辅助类实例
            _testCreator = new XClipTestCreator(AcadService, XClipBlockService);
            _testValidator = new XClipTestValidator(AcadService, XClipBlockService);
        }
        
        /// <summary>
        /// 运行测试1：顶层XClipped图块测试
        /// </summary>
        [CommandMethod("DDNTEST_TOPLEVEL_XCLIP")]
        public void TestTopLevelXClip()
        {
            CommandName = "DDNTEST_TOPLEVEL_XCLIP";
            Execute();
        }
        
        /// <summary>
        /// 运行测试2：嵌套XClipped图块测试
        /// </summary>
        [CommandMethod("DDNTEST_NESTED_XCLIP")]
        public void TestNestedXClip()
        {
            CommandName = "DDNTEST_NESTED_XCLIP";
            Execute();
        }
        
        /// <summary>
        /// 运行测试3：混合场景测试（顶层和嵌套的XClip图块混合）
        /// </summary>
        [CommandMethod("DDNTEST_MIXED_XCLIP")]
        public void TestMixedXClip()
        {
            CommandName = "DDNTEST_MIXED_XCLIP";
            Execute();
        }
        
        /// <summary>
        /// 运行测试4：特殊属性测试（BYLAYER和BYBLOCK颜色、线型、图层显隐）
        /// </summary>
        [CommandMethod("DDNTEST_ATTRIBUTE_XCLIP")]
        public void TestAttributeXClip()
        {
            CommandName = "DDNTEST_ATTRIBUTE_XCLIP";
            Execute();
        }
        
        /// <summary>
        /// 运行测试5：复杂变换测试（旋转、缩放、镜像）
        /// </summary>
        [CommandMethod("DDNTEST_TRANSFORM_XCLIP")]
        public void TestTransformXClip()
        {
            CommandName = "DDNTEST_TRANSFORM_XCLIP";
            Execute();
        }
        
        /// <summary>
        /// 执行综合测试
        /// </summary>
        [CommandMethod("DDNTEST_COMPREHENSIVE")]
        public void TestComprehensive()
        {
            CommandName = "DDNTEST_COMPREHENSIVE";
            Execute();
        }

        /// <summary>
        /// 清理测试数据
        /// </summary>
        [CommandMethod("DDNTEST_CLEANUP")]
        public void CleanupTests()
        {
            CommandName = "DDNTEST_CLEANUP";
            Execute();
        }

        /// <summary>
        /// 测试所有功能并自动验证结果
        /// </summary>
        [CommandMethod("DDNTEST_AUTO")]
        public void RunAutomatedTest()
        {
            CommandName = "DDNTEST_AUTO";
            Execute();
        }
        
        /// <summary>
        /// 执行命令的具体实现
        /// </summary>
        protected override void ExecuteCommand()
        {
            switch (CommandName)
            {
                case "DDNTEST_TOPLEVEL_XCLIP":
                    ExecuteTopLevelXClipTest();
                    break;
                case "DDNTEST_NESTED_XCLIP":
                    ExecuteNestedXClipTest();
                    break;
                case "DDNTEST_MIXED_XCLIP":
                    ExecuteMixedXClipTest();
                    break;
                case "DDNTEST_ATTRIBUTE_XCLIP":
                    ExecuteAttributeXClipTest();
                    break;
                case "DDNTEST_TRANSFORM_XCLIP":
                    ExecuteTransformXClipTest();
                    break;
                case "DDNTEST_COMPREHENSIVE":
                    ExecuteComprehensiveTest();
                    break;
                case "DDNTEST_CLEANUP":
                    ExecuteCleanupTests();
                    break;
                case "DDNTEST_AUTO":
                    ExecuteAutomatedTest();
                    break;
                default:
                    WriteTestError($"未知测试命令: {CommandName}");
                    break;
            }
        }
        
        /// <summary>
        /// 执行顶层XClipped图块测试
        /// </summary>
        private void ExecuteTopLevelXClipTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 创建测试图层
                var layers = _testCreator.CreateTestLayers();
                
                WriteTestStart("顶层XClipped图块");
                
                // 创建测试块
                ObjectId blockId = _testCreator.CreateSimpleBlock(db, "DDNTest_TopLevel", layers.RedLayer);
                
                // 对块进行XClip
                var xclipResult = XClipBlockService.AutoXClipBlock(db, blockId);
                
                if (xclipResult.Success)
                {
                    WriteTestSuccess(xclipResult.Message);
                    WriteTestInfo("请执行ISOLATEXCLIPPEDBLOCKS命令测试隔离功能");
                }
                else
                {
                    WriteTestError(xclipResult.ErrorMessage);
                }
            }, "执行顶层图块测试时出错");
        }
        
        /// <summary>
        /// 执行嵌套XClipped图块测试
        /// </summary>
        private void ExecuteNestedXClipTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 创建测试图层
                var layers = _testCreator.CreateTestLayers();
                
                WriteTestStart("嵌套XClipped图块");
                
                // 创建测试场景：A(红色图层) -> B(蓝色图层) -> C(绿色图层)
                // C将被XClip并需要被移动到顶层
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 创建块C (最内层，将被XClip)
                    ObjectId blockCId = _testCreator.CreateTestBlock(tr, db, "DDNTest_C", layers.GreenLayer);
                    
                    // 创建块B (包含块C)
                    ObjectId blockBId = _testCreator.CreateNestedBlock(tr, db, "DDNTest_B", blockCId, layers.BlueLayer);
                    
                    // 创建块A (包含块B)
                    ObjectId blockAId = _testCreator.CreateNestedBlock(tr, db, "DDNTest_A", blockBId, layers.RedLayer);
                    
                    tr.Commit();
                }
                
                // 找到最内层的块C引用
                ObjectId nestedBlockCId = _testCreator.FindNestedBlockReference(db, "DDNTest_C");
                
                if (nestedBlockCId != ObjectId.Null)
                {
                    // 对块C进行XClip
                    var xclipResult = XClipBlockService.AutoXClipBlock(db, nestedBlockCId);
                    
                    if (xclipResult.Success)
                    {
                        WriteTestSuccess(xclipResult.Message);
                        WriteTestInfo("请执行ISOLATEXCLIPPEDBLOCKS命令测试隔离功能");
                    }
                    else
                    {
                        WriteTestError(xclipResult.ErrorMessage);
                    }
                }
                else
                {
                    WriteTestError("未能找到嵌套块C的引用");
                }
            }, "执行嵌套图块测试时出错");
        }
        
        /// <summary>
        /// 执行混合场景测试
        /// </summary>
        private void ExecuteMixedXClipTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 创建测试图层
                var layers = _testCreator.CreateTestLayers();
                
                WriteTestStart("混合XClipped图块");
                
                // 创建顶层测试块
                ObjectId topLevelBlockId = _testCreator.CreateSimpleBlock(db, "DDNTest_TopLevel_Mixed", layers.RedLayer);
                
                // 创建嵌套测试块
                ObjectId nestedBlockId = ObjectId.Null;
                
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 创建内层块
                    ObjectId innerBlockId = _testCreator.CreateTestBlock(tr, db, "DDNTest_Inner_Mixed", layers.GreenLayer);
                    
                    // 创建外层块
                    nestedBlockId = _testCreator.CreateNestedBlock(tr, db, "DDNTest_Outer_Mixed", innerBlockId, layers.BlueLayer);
                    
                    tr.Commit();
                }
                
                // 找到内层块引用
                ObjectId innerBlockRefId = _testCreator.FindNestedBlockReference(db, "DDNTest_Inner_Mixed");
                
                // 对两个块都进行XClip
                var topLevelXclipResult = XClipBlockService.AutoXClipBlock(db, topLevelBlockId);
                var nestedXclipResult = innerBlockRefId != ObjectId.Null ? 
                    XClipBlockService.AutoXClipBlock(db, innerBlockRefId) : 
                    OperationResult.ErrorResult("未找到内层块引用", TimeSpan.Zero);
                
                if (topLevelXclipResult.Success && nestedXclipResult.Success)
                {
                    WriteTestSuccess("成功创建混合XClip测试块");
                    WriteTestInfo("请执行ISOLATEXCLIPPEDBLOCKS命令测试隔离功能");
                }
                else
                {
                    if (!topLevelXclipResult.Success)
                        WriteTestError($"创建顶层XClip测试块失败: {topLevelXclipResult.ErrorMessage}");
                    
                    if (!nestedXclipResult.Success)
                        WriteTestError($"创建嵌套XClip测试块失败: {nestedXclipResult.ErrorMessage}");
                }
            }, "执行混合场景测试时出错");
        }
        
        /// <summary>
        /// 执行特殊属性测试
        /// </summary>
        private void ExecuteAttributeXClipTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 创建测试图层
                var layers = _testCreator.CreateTestLayers();
                
                WriteTestStart("特殊属性XClipped图块");
                
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 冻结一个图层
                    _testCreator.SetLayerFrozen(tr, db, layers.Layer2, true);
                    
                    // 加载虚线线型
                    _testCreator.LoadLineType(tr, db, "DASHED");
                    
                    tr.Commit();
                }
                
                // 创建特殊属性的测试块
                ObjectId specialBlockId = _testCreator.CreateSpecialAttributeBlock(db, "DDNTest_Special", layers);
                
                // 对块进行XClip
                var xclipResult = XClipBlockService.AutoXClipBlock(db, specialBlockId);
                
                if (xclipResult.Success)
                {
                    WriteTestSuccess(xclipResult.Message);
                    WriteTestInfo("请执行ISOLATEXCLIPPEDBLOCKS命令测试隔离功能");
                    WriteTestInfo("请检查隔离后的块是否保持以下特性:");
                    WriteTestInfo("1. 冻结图层(Layer2)上的实体仍然不可见");
                    WriteTestInfo("2. BYLAYER颜色显示与图层一致");
                    WriteTestInfo("3. BYBLOCK颜色显示与块颜色一致");
                    WriteTestInfo("4. 虚线线型正确显示");
                }
                else
                {
                    WriteTestError(xclipResult.ErrorMessage);
                }
            }, "执行特殊属性测试时出错");
        }
        
        /// <summary>
        /// 执行复杂变换测试
        /// </summary>
        private void ExecuteTransformXClipTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 创建测试图层
                var layers = _testCreator.CreateTestLayers();
                
                WriteTestStart("复杂变换XClipped图块");
                
                // 创建带变换的测试块
                ObjectId transformBlockId = _testCreator.CreateTransformedBlock(db, "DDNTest_Transform", layers.RedLayer);
                
                // 对块进行XClip
                var xclipResult = XClipBlockService.AutoXClipBlock(db, transformBlockId);
                
                if (xclipResult.Success)
                {
                    WriteTestSuccess(xclipResult.Message);
                    WriteTestInfo("请执行ISOLATEXCLIPPEDBLOCKS命令测试隔离功能");
                    WriteTestInfo("请检查隔离后的块是否保持旋转(45度)和非均匀缩放(X = 0.5,Y = 2.0)状态");
                }
                else
                {
                    WriteTestError(xclipResult.ErrorMessage);
                }
            }, "执行复杂变换测试时出错");
        }
        
        /// <summary>
        /// 执行综合测试
        /// </summary>
        private void ExecuteComprehensiveTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 创建测试图层
                var layers = _testCreator.CreateTestLayers();
                
                WriteTestStart("全面测试");
                
                // 清理之前的测试数据
                _testCreator.CleanupTests();
                
                // 创建所有测试环境
                WriteTestInfo("正在创建测试环境...");
                var testData = _testCreator.CreateTestEnvironment();
                
                WriteTestSuccess($"测试环境创建完成，共创建了{testData.BlockIds.Count}个测试块");
                WriteTestInfo("请执行ISOLATEXCLIPPEDBLOCKS命令测试隔离功能");
            }, "执行综合测试时出错");
        }
        
        /// <summary>
        /// 执行清理测试数据
        /// </summary>
        private void ExecuteCleanupTests()
        {
            ExecuteTestOperation(() => {
                WriteTestStart("清理测试数据");
                _testCreator.CleanupTests();
                WriteTestSuccess("测试数据清理完成");
            }, "清理测试数据时出错");
        }
        
        /// <summary>
        /// 执行自动化测试
        /// </summary>
        private void ExecuteAutomatedTest()
        {
            Database db = GetDatabase();
            
            ExecuteTestOperation(() => {
                // 输出测试开始信息
                WriteTestStart("自动化测试");
                
                // 清理之前的测试数据
                _testCreator.CleanupTests();
                
                // 1. 创建测试环境
                WriteTestInfo("[第1步] 创建测试环境");
                var testData = _testCreator.CreateTestEnvironment();
                
                // 2. 获取初始状态
                WriteTestInfo("[第2步] 记录初始状态");
                var initialStates = _testValidator.CaptureBlockStates(db, testData.BlockIds);
                
                // 3. 执行XClip命令
                WriteTestInfo("[第3步] 执行ISOLATEXCLIPPEDBLOCKS命令");
                _testValidator.ExecuteIsolateCommand(GetDocument());
                
                // 4. 验证结果
                WriteTestInfo("[第4步] 验证测试结果");
                _testValidator.VerifyResults(db, initialStates);
                
                // 5. 输出测试报告
                WriteTestEnd("自动化测试");
            }, "执行自动化测试时出错");
        }
    }
} 