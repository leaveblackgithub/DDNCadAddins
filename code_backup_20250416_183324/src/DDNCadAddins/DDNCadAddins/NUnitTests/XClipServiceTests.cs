using System;
using System.Collections.Generic;
using NUnit.Framework;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Models;
using DDNCadAddins.Services;
using DDNCadAddins.NUnitTests.Framework;

namespace DDNCadAddins.NUnitTests
{
    /// <summary>
    /// XClipService单元测试类
    /// </summary>
    [TestFixture]
    public class XClipServiceTests : AcadTestFixtureBase
    {
        // 测试用图层ID
        private ObjectId _redLayerId;
        private ObjectId _blueLayerId;
        private ObjectId _greenLayerId;
        
        // 测试用块ID
        private ObjectId _testBlockId;
        private ObjectId _testNestedBlockId;
        
        /// <summary>
        /// 初始化测试环境
        /// </summary>
        protected override void SetupTestEnvironment()
        {
            base.SetupTestEnvironment();
            
            // 创建测试图层
            _redLayerId = CreateTestLayer("DDNTest_Red", 1);  // 红色
            _blueLayerId = CreateTestLayer("DDNTest_Blue", 5); // 蓝色
            _greenLayerId = CreateTestLayer("DDNTest_Green", 3); // 绿色
            
            // 创建测试块
            Database db = GetDatabase();
            if (db != null)
            {
                // 清理可能存在的块
                EnsureTestBlockDeleted("DDNTest_Block");
                EnsureTestBlockDeleted("DDNTest_NestedBlock");
                
                // 创建新的测试块
                WithTransaction(tr => {
                    // 创建简单块
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    
                    // 创建块定义
                    BlockTableRecord blockDef = new BlockTableRecord();
                    blockDef.Name = "DDNTest_Block";
                    
                    bt.UpgradeOpen();
                    _testBlockId = bt.Add(blockDef);
                    tr.AddNewlyCreatedDBObject(blockDef, true);
                    
                    // 添加一个圆形
                    Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 5.0);
                    circle.LayerId = _redLayerId;
                    blockDef.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    
                    // 添加一个矩形
                    Polyline rect = new Polyline();
                    rect.AddVertexAt(0, new Point2d(-10, -10), 0, 0, 0);
                    rect.AddVertexAt(1, new Point2d(10, -10), 0, 0, 0);
                    rect.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                    rect.AddVertexAt(3, new Point2d(-10, 10), 0, 0, 0);
                    rect.Closed = true;
                    rect.LayerId = _blueLayerId;
                    blockDef.AppendEntity(rect);
                    tr.AddNewlyCreatedDBObject(rect, true);
                    
                    // 创建嵌套块
                    BlockTableRecord nestedBlockDef = new BlockTableRecord();
                    nestedBlockDef.Name = "DDNTest_NestedBlock";
                    _testNestedBlockId = bt.Add(nestedBlockDef);
                    tr.AddNewlyCreatedDBObject(nestedBlockDef, true);
                    
                    // 添加一个矩形
                    Polyline outerRect = new Polyline();
                    outerRect.AddVertexAt(0, new Point2d(-15, -15), 0, 0, 0);
                    outerRect.AddVertexAt(1, new Point2d(15, -15), 0, 0, 0);
                    outerRect.AddVertexAt(2, new Point2d(15, 15), 0, 0, 0);
                    outerRect.AddVertexAt(3, new Point2d(-15, 15), 0, 0, 0);
                    outerRect.Closed = true;
                    outerRect.LayerId = _greenLayerId;
                    nestedBlockDef.AppendEntity(outerRect);
                    tr.AddNewlyCreatedDBObject(outerRect, true);
                    
                    // 添加对第一个块的引用
                    BlockReference blockRef = new BlockReference(Point3d.Origin, _testBlockId);
                    nestedBlockDef.AppendEntity(blockRef);
                    tr.AddNewlyCreatedDBObject(blockRef, true);
                });
            }
        }
        
        /// <summary>
        /// 清理测试环境
        /// </summary>
        protected override void CleanupTestEnvironment()
        {
            // 删除测试块
            EnsureTestBlockDeleted("DDNTest_Block");
            EnsureTestBlockDeleted("DDNTest_NestedBlock");
            
            base.CleanupTestEnvironment();
        }
        
        [Test]
        [Description("测试创建XClip边界的功能")]
        public void TestCreateXClipBoundary()
        {
            // 创建一个新的块引用
            ObjectId blockRefId = ObjectId.Null;
            
            WithTransaction(tr => {
                // 创建块引用
                BlockTable bt = tr.GetObject(GetDatabase().BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                BlockReference blockRef = new BlockReference(new Point3d(0, 0, 0), _testBlockId);
                ms.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                blockRefId = blockRef.ObjectId;
            });
            
            Assert.IsNotNull(blockRefId);
            Assert.AreNotEqual(ObjectId.Null, blockRefId);
            
            // 使用服务创建XClip
            OperationResult result = XClipBlockService.CreateRectangularXClipBoundary(
                GetDatabase(), blockRefId, 
                new Point3d(-5, -5, 0), new Point3d(5, 5, 0));
            
            // 验证结果
            Assert.IsTrue(result.Success, $"创建XClip失败: {result.ErrorMessage}");
            
            // 验证XClip已创建
            bool hasXClip = false;
            WithTransaction(tr => {
                BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                hasXClip = blockRef.IsClipped;
            });
            
            Assert.IsTrue(hasXClip, "块引用应该有XClip边界");
        }
        
        [AcadTest("测试查找XClipped图块功能")]
        public void TestFindXClippedBlocks()
        {
            // 创建两个块引用，一个带XClip，一个不带
            ObjectId xclippedBlockId = ObjectId.Null;
            ObjectId normalBlockId = ObjectId.Null;
            
            WithTransaction(tr => {
                // 获取模型空间
                BlockTable bt = tr.GetObject(GetDatabase().BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 创建第一个带XClip的块引用
                BlockReference blockRef1 = new BlockReference(new Point3d(20, 0, 0), _testBlockId);
                ms.AppendEntity(blockRef1);
                tr.AddNewlyCreatedDBObject(blockRef1, true);
                xclippedBlockId = blockRef1.ObjectId;
                
                // 创建第二个正常的块引用
                BlockReference blockRef2 = new BlockReference(new Point3d(-20, 0, 0), _testBlockId);
                ms.AppendEntity(blockRef2);
                tr.AddNewlyCreatedDBObject(blockRef2, true);
                normalBlockId = blockRef2.ObjectId;
            });
            
            // 给第一个块添加XClip
            OperationResult clipResult = XClipBlockService.CreateRectangularXClipBoundary(
                GetDatabase(), xclippedBlockId, 
                new Point3d(15, -5, 0), new Point3d(25, 5, 0));
            
            Assert.IsTrue(clipResult.Success, "添加XClip失败");
            
            // 查找所有XClipped块
            var findResult = XClipBlockService.FindAllXClippedBlocks(GetDatabase());
            
            // 验证结果
            Assert.IsTrue(findResult.Success, $"查找XClipped块失败: {findResult.ErrorMessage}");
            Assert.IsNotNull(findResult.Data, "结果数据不应为空");
            
            // 验证找到的数量
            var foundBlocks = findResult.Data as List<XClippedBlockInfo>;
            Assert.IsNotNull(foundBlocks, "返回的数据应该是XClippedBlockInfo列表");
            
            // 应该至少找到一个块
            Assert.Greater(foundBlocks.Count, 0, "应至少找到一个XClipped块");
            
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
            
            Assert.IsTrue(foundTestBlock, "未找到我们创建的XClipped块");
        }
        
        [AcadTest("测试自动XClip图块功能")]
        public void TestAutoXClipBlock()
        {
            // 创建一个新的块引用
            ObjectId blockRefId = ObjectId.Null;
            
            WithTransaction(tr => {
                // 创建块引用
                BlockTable bt = tr.GetObject(GetDatabase().BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                BlockReference blockRef = new BlockReference(new Point3d(40, 0, 0), _testBlockId);
                ms.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                blockRefId = blockRef.ObjectId;
            });
            
            // 使用AutoXClip
            var result = XClipBlockService.AutoXClipBlock(GetDatabase(), blockRefId);
            
            // 验证结果
            Assert.IsTrue(result.Success, $"自动XClip失败: {result.ErrorMessage}");
            
            // 验证XClip已创建
            bool hasXClip = false;
            WithTransaction(tr => {
                BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                hasXClip = blockRef.IsClipped;
            });
            
            Assert.IsTrue(hasXClip, "自动XClip后块引用应该有XClip边界");
        }
        
        [AcadTest("测试嵌套图块的XClip功能")]
        public void TestNestedBlockXClip()
        {
            // 创建一个嵌套块引用
            ObjectId nestedBlockRefId = ObjectId.Null;
            
            WithTransaction(tr => {
                // 创建块引用
                BlockTable bt = tr.GetObject(GetDatabase().BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                BlockReference blockRef = new BlockReference(new Point3d(60, 0, 0), _testNestedBlockId);
                ms.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                nestedBlockRefId = blockRef.ObjectId;
            });
            
            // 使用AutoXClip
            var result = XClipBlockService.AutoXClipBlock(GetDatabase(), nestedBlockRefId);
            
            // 验证结果
            Assert.IsTrue(result.Success, $"嵌套块自动XClip失败: {result.ErrorMessage}");
            
            // 验证XClip已创建
            bool hasXClip = false;
            WithTransaction(tr => {
                BlockReference blockRef = tr.GetObject(nestedBlockRefId, OpenMode.ForRead) as BlockReference;
                hasXClip = blockRef.IsClipped;
            });
            
            Assert.IsTrue(hasXClip, "嵌套块应该有XClip边界");
        }
    }
} 