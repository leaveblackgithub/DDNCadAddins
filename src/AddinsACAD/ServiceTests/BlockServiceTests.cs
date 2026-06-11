using System;
using System.Linq;
using System.Threading;
using AddinsAcad.ServiceTests;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BlockServiceTests
    {
        private const int BlkChildCount = 8;
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
                    var objectId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(transactionService);
                    if (objectId.IsNull)
                    {
                        Assert.Inconclusive("测试块创建失败，跳过此测试");
                        return;
                    }

                    var blkService2 = transactionService.Block.GetBlockService(objectId);
                    if (blkService2 == null)
                    {
                        Assert.Fail($"\n 无法获取 ObjectId: {objectId} 的块服务");
                        return;
                    }

                    if (!blkService2.HasAttributes())
                    {
                        Assert.Fail($"\n 块参照不包含属性: {objectId}");
                        return;
                    }

                    var explodeResult = blkService2.ExplodeAsShown();

                    if (!explodeResult.IsSuccess)
                    {
                        Assert.Fail($"\n 爆炸操作失败: {explodeResult.Message}");
                        return;
                    }

                    if (explodeResult.Data.Count != BlkChildCount)
                    {
                        Assert.Fail($"\n 爆炸结果元素数量不符合预期: 期望{BlkChildCount}个，实际{explodeResult.Data.Count}个");
                    }

                    var txtFrAttr1s = transactionService.FilterObjects<DBText>(
                        explodeResult.Data, txt => txt.TextString == BlockServiceTestUtils.StrValue1);
                    Assert.AreEqual(1, txtFrAttr1s.Count);

                    var txtFrAttr1 = transactionService.GetObject<DBText>(txtFrAttr1s[0]);
                    Assert.AreEqual(BlockServiceTestUtils.NameTestLayer, txtFrAttr1.Layer);
                    Assert.AreEqual(BlockServiceTestUtils.NameTestLinetype, txtFrAttr1.Linetype);

                    var txtFrAttr2s = transactionService.FilterObjects<DBText>(
                        explodeResult.Data, txt => txt.TextString == BlockServiceTestUtils.StrValue2);
                    Assert.AreEqual(1, txtFrAttr2s.Count);

                    var txtFrAttr2 = transactionService.GetObject<DBText>(txtFrAttr2s[0]);
                    Assert.AreEqual(CadServiceManager.ColorIndexMagenta, txtFrAttr2.ColorIndex);
                }
                catch (AssertionException assertionException)
                {
                    Logger._.Error($"\n{assertionException.Message}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"\n 测试过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                }
            }

            CadServiceManager._.ExecuteInTransactions("", Action1);
        }

        // [Test]
        public void TestExplodeWithPropertyAdjustment()
        {
            void Action1(ITransactionService transactionService)
            {
                try
                {
                    var blockRefId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(transactionService);

                    if (!blockRefId.IsValid)
                    {
                        Assert.Fail("\n 创建测试块失败");
                        return;
                    }

                    var blockService = transactionService.Block.GetBlockService(blockRefId);
                    if (blockService == null)
                    {
                        Assert.Fail($"\n 无法获取块服务: {blockRefId}");
                        return;
                    }

                    blockService.UpgradeOpen();
                    blockService.Layer = "TestExplodeLayer";
                    blockService.ColorIndex = CadServiceManager.ColorIndexCyan;
                    blockService.Linetype = "DASHED";

                    var explodeResult = blockService.ExplodeAsShown();

                    if (!explodeResult.IsSuccess)
                    {
                        Assert.Fail($"\n 爆炸操作失败: {explodeResult.Message}");
                        return;
                    }

                    if (explodeResult.Data.Count == 0)
                    {
                        Assert.Fail("\n 爆炸结果为空");
                    }
                }
                catch (AssertionException assertionException)
                {
                    Logger._.Error($"\n{assertionException.Message}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"\n 测试过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                }
            }

            CadServiceManager._.ExecuteInTransactions("", Action1);
        }
    }
}
