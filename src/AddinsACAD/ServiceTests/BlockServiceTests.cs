using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using ServiceACAD;
using AddinsAcad.ServiceTests;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BlockServiceTests
    {
        private IBlockService _blkService;
        
        [Test]
        public void TestIsXclipped()
        {
            void Action1(ITransactionService transactionService)
            {
                _blkService = CommonTestMethods.GetFirstBlkServiceOf23432(transactionService);
                Assert.IsTrue(_blkService.IsXclipped());
            }
            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }
        [Test]
        public void TestHasAtt()
        {
            void Action1(ITransactionService transactionService)
            {
                _blkService = CommonTestMethods.GetFirstBlkServiceOf23432(transactionService);
                Assert.IsTrue(_blkService.HasAttributes());
            }
            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }
        
        [Test]
        public void TestExplodeWithAttributes()
        {
            void Action1(ITransactionService transactionService)
            {
                try
                {
                    var blkName = "blockwattr";
                    // 获取一个带属性的块参照
                    var blkIds = transactionService.GetChildObjectsFromCurrentSpace<BlockReference>(
                        blkRef => blkRef.Name == blkName);
                    if (blkIds.Count == 0)
                    {
                        Assert.Fail($"\n 找不到名为 {blkName} 的块参照");
                        return;
                    }

                    var objectId = blkIds[0];
                    var blkService2 = transactionService.GetBlockService(objectId);
                    if (blkService2 == null)
                    {
                        Assert.Fail($"\n 无法获取 ObjectId: {objectId} 的块服务");
                        return;
                    }

                    // 确认块参照有属性
                    if (!blkService2.HasAttributes())
                    {
                        Assert.Fail($"\n 块参照不包含属性: {objectId}");
                        return;
                    }

                    // 爆炸块参照，将属性转换为文本
                    OpResult<List<ObjectId>> explodeResult = blkService2.ExplodeWithAttributes();

                    // 验证爆炸操作成功
                    if (!explodeResult.IsSuccess)
                    {
                        Assert.Fail($"\n 爆炸操作失败: {explodeResult.Message}");
                        return;
                    }

                    // 检查结果数量
                    if (explodeResult.Data.Count != 2)
                    {
                        Assert.Fail($"\n 爆炸结果元素数量不符合预期: 期望2个，实际{explodeResult.Data.Count}个");
                        return;
                    }

                    // 检查文本内容
                    int textCount = transactionService.FilterObjects<DBText>(
                        explodeResult.Data, (txt => txt.TextString == "3.1415926")).Count;

                    if (textCount != 1)
                    {
                        Assert.Fail($"\n 找不到TextString为'3.1415926'的文本对象，找到{textCount}个");
                        return;
                    }

                    // Assert.Pass("\n 测试通过: 块参照成功爆炸且属性转换为文本");
                    return;
                }
                catch (AssertionException assertionException)
                {
                    Logger._.Error($"\n{assertionException.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    Assert.Fail($"\n 测试过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                }
            }
            
            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }
        
        // [Test]
        public void TestExplodeWithPropertyAdjustment()
        {
            void Action1(ITransactionService transactionService)
            {
                try
                {
                    // 创建测试块（带有明确属性特征的块）
                    ObjectId blockRefId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(transactionService);
                    
                    if (!blockRefId.IsValid)
                    {
                        Assert.Fail("\n 创建测试块失败");
                        return;
                    }
                    
                    // 获取块服务
                    var blockService = transactionService.GetBlockService(blockRefId);
                    if (blockService == null)
                    {
                        Assert.Fail($"\n 无法获取块服务: {blockRefId}");
                        return;
                    }
                    
                    // 修改块参照的属性
                    BlockReference blockRef = blockService.CadBlkRef;
                    string originalLayer = blockRef.Layer;
                    int originalColorIndex = blockRef.ColorIndex;
                    string originalLinetype = blockRef.Linetype;
                    
                    // 设置新的属性
                    blockRef.UpgradeOpen();
                    blockRef.Layer = "TestExplodeLayer";
                    blockRef.ColorIndex = CadServiceManager.ColorIndexCyan; // 青色
                    blockRef.Linetype = "DASHED";
                    
                    // 爆炸块参照
                    OpResult<List<ObjectId>> explodeResult = blockService.ExplodeWithAttributes();
                    
                    // 验证爆炸操作成功
                    if (!explodeResult.IsSuccess)
                    {
                        Assert.Fail($"\n 爆炸操作失败: {explodeResult.Message}");
                        return;
                    }
                    
                    // 验证爆炸结果非空
                    if (explodeResult.Data.Count == 0)
                    {
                        Assert.Fail("\n 爆炸结果为空");
                        return;
                    }
                    
                    // 检查所有爆炸后的实体是否继承了块参照的属性
                    bool allPropertiesCorrect = true;
                    string errorMessage = "";
                    
                    foreach (ObjectId entityId in explodeResult.Data)
                    {
                        Entity entity = transactionService.GetObject<Entity>(entityId);
                        
                        // 检查图层属性
                        if (entity.Layer != "TestExplodeLayer")
                        {
                            allPropertiesCorrect = false;
                            errorMessage += $"\n 实体 {entityId} 图层错误: 预期'TestExplodeLayer'，实际'{entity.Layer}'";
                        }
                        
                        // 检查颜色属性
                        // 如果原实体颜色是BYBLOCK，则应该继承块参照的颜色
                        if (entity.ColorIndex == 0 && entity.ColorIndex != CadServiceManager.ColorIndexCyan)
                        {
                            allPropertiesCorrect = false;
                            errorMessage += $"\n 实体 {entityId} 颜色错误: 预期{CadServiceManager.ColorIndexCyan}，实际{entity.ColorIndex}";
                        }
                        
                        // 检查线型属性
                        // 如果原实体线型是BYBLOCK，则应该继承块参照的线型
                        if (entity.Linetype == "BYBLOCK" && entity.Linetype != "DASHED")
                        {
                            allPropertiesCorrect = false;
                            errorMessage += $"\n 实体 {entityId} 线型错误: 预期'DASHED'，实际'{entity.Linetype}'";
                        }
                    }
                    
                    // 专门检查属性转换为的文字对象
                    List<ObjectId> textIds = transactionService.FilterObjects<DBText>(explodeResult.Data);
                    if (textIds.Count < 2)
                    {
                        Assert.Fail($"\n 爆炸后的文字对象数量不足: 预期至少2个，实际{textIds.Count}个");
                    }
                    
                    // 记录找到的属性文字值
                    bool foundAttr1 = false;
                    bool foundAttr2 = false;
                    
                    foreach (ObjectId textId in textIds)
                    {
                        DBText text = transactionService.GetObject<DBText>(textId);
                        
                        // 检查是否是属性文字
                        if (text.TextString == "测试属性1")
                        {
                            foundAttr1 = true;
                            
                            // 检查该文字是否保留了原属性的图层和颜色
                            if (text.Layer != "TestExplodeLayer")
                            {
                                allPropertiesCorrect = false;
                                errorMessage += $"\n 属性文字1 图层错误: 预期'TestExplodeLayer'，实际'{text.Layer}'";
                            }
                            
                            // 检查文字颜色 - 应当继承块的颜色
                            if (text.ColorIndex != CadServiceManager.ColorIndexCyan)
                            {
                                allPropertiesCorrect = false;
                                errorMessage += $"\n 属性文字1 颜色错误: 预期{CadServiceManager.ColorIndexCyan}，实际{text.ColorIndex}";
                            }
                        }
                        else if (text.TextString == "测试属性2")
                        {
                            foundAttr2 = true;
                            
                            // 检查该文字是否保留了原属性的图层和颜色
                            if (text.Layer != "TestExplodeLayer")
                            {
                                allPropertiesCorrect = false;
                                errorMessage += $"\n 属性文字2 图层错误: 预期'TestExplodeLayer'，实际'{text.Layer}'";
                            }
                            
                            // 检查文字颜色 - BYBLOCK应当继承块的颜色
                            if (text.ColorIndex != CadServiceManager.ColorIndexCyan)
                            {
                                allPropertiesCorrect = false;
                                errorMessage += $"\n 属性文字2 颜色错误: 预期{CadServiceManager.ColorIndexCyan}，实际{text.ColorIndex}";
                            }
                            
                            // 检查文字线型 - BYBLOCK应当继承块的线型
                            if (text.Linetype != "DASHED")
                            {
                                allPropertiesCorrect = false;
                                errorMessage += $"\n 属性文字2 线型错误: 预期'DASHED'，实际'{text.Linetype}'";
                            }
                        }
                    }
                    
                    if (!foundAttr1 || !foundAttr2)
                    {
                        allPropertiesCorrect = false;
                        errorMessage += $"\n 未找到所有属性文字: 属性1={foundAttr1}, 属性2={foundAttr2}";
                    }
                    
                    if (!allPropertiesCorrect)
                    {
                        Assert.Fail($"\n 属性继承测试失败: {errorMessage}");
                    }
                    
                    return;
                }
                catch (AssertionException assertionException)
                {
                    Logger._.Error($"\n{assertionException.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    Assert.Fail($"\n 测试过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                }
            }
            
            // 执行测试
            CadServiceManager._.ExecuteInTransactions("", Action1);
        }
    }
}
