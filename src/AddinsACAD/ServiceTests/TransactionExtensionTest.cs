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
    public class TransactionExtensionTest
    {
        [Test]
        public void TestGetModelSpaceForWrite()
        {
            void Action1(IDocumentService serviceDoc, Transaction tr)
            {
                using (var modelSpace = tr.GetModelSpace(serviceDoc.CadDb, OpenMode.ForWrite))
                    Assert.NotNull(modelSpace);
            }

            ;
            TestUtils.ExecuteInTransactions("", Action1);
        }
                [Test]
        public void TestGetModelSpaceForWrite2()
        {
            void Action1(ITransactionService tr)
            {
                using (var modelSpace = tr.GetModelSpace( OpenMode.ForWrite))
                    Assert.NotNull(modelSpace);
            }

            ;
            CadServiceManager._.ActiveServiceDoc.ExecuteInTransactions("", Action1);
        }


        [Test]
        public void TestGetModelSpaceChildObjs()
        {
            void Action1(IDocumentService serviceDoc, Transaction tr)
            {
                try
                {
                    var getChildObjects =
                        tr.GetChildObjectsFrModelspace(serviceDoc.CadDb, obj => obj is BlockReference);

                    Assert.AreEqual(getChildObjects.Count, 7);

                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

            }

            TestUtils.ExecuteInTransactions("xclip", Action1);
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

            CadServiceManager._.ActiveServiceDoc.ExecuteInTransactions("xclip", Action1);
        }

        // [Test]
        // public void TestGetBlockRef23432()
        // {
        //     void Action1(IDocumentService serviceDoc, Transaction tr)
        //     {
        //         var blkRefIds = tr.GetChildObjectsFrModelspace(serviceDoc.CadDb,
        //             obj => obj is BlockReference && ((BlockReference)obj).Name == "23432");
        //         Assert.Equals(blkRefIds.Count, 1);
        //
        //     }
        //
        //     TestUtils.ExecuteInTransactions("xclip", Action1);
        // }
    }
}
