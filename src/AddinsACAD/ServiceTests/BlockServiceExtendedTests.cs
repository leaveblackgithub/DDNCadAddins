using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    /// <summary>
    ///     BlockService 集成测试（只读，不修改共享图纸）
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BlockServiceExtendedTests
    {
        [Test]
        public void TestIsXclipped_XclippedBlock_ReturnsTrue()
        {
            void Action(ITransactionService tr)
            {
                var blkService = CommonTestMethods.GetFirstBlkServiceOf23432(tr);
                Assert.IsNotNull(blkService);
                Assert.IsTrue(blkService.IsXclipped(), "名为 23432 的块应是 XClipped");
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action);
        }

        [Test]
        public void TestGetBlockService_CalledTwice_ReturnsSameInstance()
        {
            void Action(ITransactionService tr)
            {
                var ids = CommonTestMethods.GetBlkRefIdsOf23432(tr);
                if (ids.Count == 0)
                {
                    Assert.Inconclusive("无可用块参照，跳过缓存测试");
                    return;
                }

                var service1 = tr.Block.GetBlockService(ids[0]);
                var service2 = tr.Block.GetBlockService(ids[0]);
                Assert.AreSame(service1, service2, "同一 ObjectId 应返回缓存的同一实例");
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action);
        }

        [Test]
        public void TestCopyXclipState_XclippedBlock_CloneRetainsXclip()
        {
            void Action(ITransactionService tr)
            {
                var blkService = CommonTestMethods.GetFirstBlkServiceOf23432(tr);
                Assert.IsNotNull(blkService, "未能找到名为 23432 的 XCLIP 图块");
                Assert.IsTrue(blkService.IsXclipped(), "源图块应是 XClipped");

                var sourceBlkRef = tr.GetObject<BlockReference>(blkService.ObjectId, OpenMode.ForRead);

                var clonedBlock = new BlockReference(sourceBlkRef.Position, sourceBlkRef.BlockTableRecord);
                clonedBlock.ScaleFactors = sourceBlkRef.ScaleFactors;
                clonedBlock.Rotation = sourceBlkRef.Rotation;

                // CreateExtensionDictionary 要求对象已入库，必须先 Append 再复制 XCLIP
                var cloneId = tr.AppendEntityToModelSpace(clonedBlock);
                Assert.IsFalse(cloneId.IsNull, "克隆图块应能加入模型空间");

                var blockService = new BlockService(tr, clonedBlock);
                blockService.CopyXclipState(sourceBlkRef, clonedBlock);

                Assert.IsTrue(blockService.IsXclipped(), "克隆后的图块应保留 XCLIP 状态");

                tr.GetObject<BlockReference>(cloneId, OpenMode.ForWrite).Erase();
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action);
        }
    }
}
