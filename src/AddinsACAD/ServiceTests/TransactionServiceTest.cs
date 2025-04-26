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
                        tr.GetChildObjectsFromModelspace<BlockReference>();

                    Assert.GreaterOrEqual(getChildObjects.Count, 7);
                }
                catch (Exception e)
                {
                    Logger._.Error("测试过程中发生错误", e);
                }
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }

        [Test]
        public void TestGetBlockRef23432()
        {
            void Action1(ITransactionService tr)
            {
                var blkRefIds = CommonTestMethods.GetBlkRefIdsOf23432(tr);
                Assert.AreEqual(blkRefIds.Count, 6);
                // CadServiceManager._.Isolate(blkRefIds[0]);
            }
        
            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }

        [Test]
        public void TestCreateNewLayer()
        {
            
            void Action1(ITransactionService tr)
            {

                var layerName1 = CommonTestMethods.GetTestLayerName();
                var newLayer1 = tr.CreateNewLayer(layerName1);
                Assert.IsNotNull(newLayer1);
                Assert.AreEqual(layerName1, newLayer1.Name);
                var newLayer2 = tr.CreateNewLayer(layerName1);
                Assert.IsNull(newLayer2);
                var layerName2 = CommonTestMethods.GetTestLayerName();

                var lineTypeName = CommonTestMethods.GetTestLineTypeName();
                var colorIndex = CadServiceManager.ColorIndexMagenta;
                var newLayer3 = tr.CreateNewLayer(layerName2, colorIndex,
                    lineTypeName);
                Assert.IsNotNull(newLayer3);
                Assert.AreEqual(newLayer3.Name,layerName2);
                Assert.AreEqual(newLayer3.Color.ColorIndex,colorIndex);
                Assert.AreEqual(newLayer3.LinetypeObjectId.ToString(),tr.GetLineType(lineTypeName).Id.ToString());
            }

            CadServiceManager._.ExecuteInTransactions("", Action1);
        }

        [Test]
        public void TestCreateNewLineType()
        {
            string lineTypeName = CommonTestMethods.GetTestLineTypeName();
            
            void Action1(ITransactionService tr)
            {
                var newLineType1 = tr.CreateNewLineType(lineTypeName);
                Assert.IsNotNull(newLineType1);
                Assert.AreEqual(lineTypeName, newLineType1.Name);
                var newLineType2 = tr.CreateNewLineType(lineTypeName);
                Assert.IsNull(newLineType2);
            }
            CadServiceManager._.ExecuteInTransactions("", Action1);
        }

    }
}
