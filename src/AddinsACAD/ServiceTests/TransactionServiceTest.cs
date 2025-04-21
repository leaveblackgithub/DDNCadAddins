using System;
using System.Diagnostics;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using ServiceACAD;
using TestRunnerACAD;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TransactionServiceTest
    {
                [Test]
        public void TestGetModelSpaceForWrite2()
        {
            void Action1(ITransactionService tr)
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                    Assert.NotNull(modelSpace);
            }

            ;
            CadServiceManager._.ExecuteInTransactions("", Action1);
        }



        [Test]
        public void TestGetModelSpaceChildObjs2()
        {
            void Action1(ITransactionService tr)
            {
                try
                {
                    var getChildObjects =
                        tr.GetChildObjectsFromModelspace(obj => obj is BlockReference);

                    Assert.AreEqual(getChildObjects.Count, 7);

                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }

        [Test]
        public void TestGetBlockRef23432()
        {
            void Action1(  ITransactionService tr)
            {
                var blkRefIds = tr.GetChildObjectsFromModelspace
                    (obj => obj is BlockReference && ((BlockReference)obj).Name == "23432");
                Assert.AreEqual(blkRefIds.Count, 6);
        
            }
        
            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }
    }
}
