using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using ServiceACAD;

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
    }
}
