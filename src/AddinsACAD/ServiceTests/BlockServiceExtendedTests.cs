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
        public void TestGetBlockService_NullId_ReturnsNull()
        {
            void Action(ITransactionService tr)
            {
                var service = tr.Block.GetBlockService(ObjectId.Null);
                Assert.IsNull(service, "传入 ObjectId.Null 应返回 null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }
    }
}
