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
    }
}
