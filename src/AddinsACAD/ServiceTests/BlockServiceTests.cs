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
                var blkService = transactionService.GetBlockService(blkId);
                Assert.IsTrue(blkService.IsXclipped());
            }
        }
    }
}
