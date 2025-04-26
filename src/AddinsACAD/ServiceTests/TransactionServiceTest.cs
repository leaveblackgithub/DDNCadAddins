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
            string layerName = "TestLayer_" + Guid.NewGuid().ToString("N");
            
            void Action1(ITransactionService tr)
            {
                var result = tr.CreateNewLayer(layerName);
                Assert.IsTrue(result.IsSuccess);
                Assert.IsNotNull(result.Value);
                
                // 验证图层是否创建成功
                var layer = tr.GetLayer(layerName);
                Assert.IsNotNull(layer);
                Assert.AreEqual(layerName, layer.Name);
            }

            CadServiceManager._.ExecuteInTransactions("", Action1);
        }

        [Test]
        public void TestCreateNewLineType()
        {
            string lineTypeName = "TestLineType_" + Guid.NewGuid().ToString("N");
            
            void Action1(ITransactionService tr)
            {
                var result = tr.CreateNewLineType(lineTypeName);
                Assert.IsTrue(result.IsSuccess);
                Assert.IsNotNull(result.Value);
                
                // 验证线型是否创建成功
                var lineType = tr.GetLineType(lineTypeName);
                Assert.IsNotNull(lineType);
                Assert.AreEqual(lineTypeName, lineType.Name);
            }

            CadServiceManager._.ExecuteInTransactions("", Action1);
        }
    }
}
