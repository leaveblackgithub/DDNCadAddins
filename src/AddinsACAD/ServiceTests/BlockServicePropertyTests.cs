using System.Threading;
using AddinsAcad.ServiceTests;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BlockServicePropertyTests
    {
        [Test]
        public void TestSetLayer_ChangesLayer()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull) { Assert.Inconclusive("测试块创建失败"); return; }

                var testLayer = CommonTestMethods.GetTestLayerName();
                tr.Style.CreateLayer(testLayer);

                var blkService = tr.Block.GetBlockService(refId);
                blkService.Layer = testLayer;

                Assert.AreEqual(testLayer, blkService.Layer);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestSetColorIndex_ChangesColor()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull) { Assert.Inconclusive("测试块创建失败"); return; }

                var blkService = tr.Block.GetBlockService(refId);
                blkService.ColorIndex = CadServiceManager.ColorIndexRed;

                Assert.AreEqual(CadServiceManager.ColorIndexRed, blkService.ColorIndex);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestSetLinetype_ChangesLinetype()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull) { Assert.Inconclusive("测试块创建失败"); return; }

                var testLinetype = CommonTestMethods.GetTestLineTypeName();
                tr.Style.CreateLineType(testLinetype);

                var blkService = tr.Block.GetBlockService(refId);
                blkService.Linetype = testLinetype;

                Assert.AreEqual(testLinetype, blkService.Linetype);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestName_StartsWithBlockDefName()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull) { Assert.Inconclusive("测试块创建失败"); return; }

                var blkService = tr.Block.GetBlockService(refId);
                Assert.IsTrue(
                    blkService.Name.StartsWith(BlockServiceTestUtils.TestBlockName),
                    $"Name 应以 {BlockServiceTestUtils.TestBlockName} 开头，实际: {blkService.Name}");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }
    }
}
