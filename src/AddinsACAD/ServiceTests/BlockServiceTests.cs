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
        [Test]
        public void TestIsXclipped()
        {
            void Action1(ITransactionService transactionService)
            {
                var blkId = CommonTestMethods.GetBlkRefIdsOf23432(transactionService)[0];
                var blk=transactionService.GetObject<BlockReference>(blkId);
                var blkService = new BlockService(blk);
                Assert.IsTrue(blkService.IsXclipped());
            }
        }
    }
}
