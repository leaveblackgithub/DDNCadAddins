using System;
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
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                Assert.NotNull(modelSpace);
            });
        }

        /// <summary>
        ///     依赖 xclip.dwg 中块参照数量。侧数据库无法满足，保留在原文档数据库.
        /// </summary>
        [Test]
        public void TestGetModelSpaceChildObjs2()
        {
            void Action1(ITransactionService tr)
            {
                try
                {
                    var getChildObjects = tr.GetChildObjectsFromModelspace<BlockReference>();
                    Assert.GreaterOrEqual(getChildObjects.Count, 1);
                }
                catch (Exception e)
                {
                    Logger._.Error("测试过程中发生错误", e);
                }
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }

        /// <summary>
        ///     依赖 xclip.dwg 中特定块定义。侧数据库无法满足，保留在原文档数据库.
        /// </summary>
        [Test]
        public void TestGetBlockRef23432()
        {
            void Action1(ITransactionService tr)
            {
                var blkRefIds = CommonTestMethods.GetBlkRefIdsOf23432(tr);
                Assert.GreaterOrEqual(blkRefIds.Count, 1);
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action1);
        }

        [Test]
        public void TestCreateNewLayer()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var layerName1 = CommonTestMethods.GetTestLayerName();
                var newLayer1 = tr.Style.CreateLayer(layerName1);
                Assert.IsNotNull(newLayer1);
                Assert.AreEqual(layerName1, newLayer1.Name);
                var newLayer2 = tr.Style.CreateLayer(layerName1);
                Assert.IsNull(newLayer2);
                var layerName2 = CommonTestMethods.GetTestLayerName();

                var lineTypeName = CommonTestMethods.GetTestLineTypeName();
                var colorIndex = CadServiceManager.Colors.Magenta;
                var newLayer3 = tr.Style.CreateLayer(layerName2, colorIndex, lineTypeName);
                Assert.IsNotNull(newLayer3);
                Assert.AreEqual(newLayer3.Name, layerName2);
                Assert.AreEqual(newLayer3.Color.ColorIndex, colorIndex);
                Assert.AreEqual(newLayer3.LinetypeObjectId.ToString(),
                    tr.Style.GetLineType(lineTypeName).Id.ToString());
            });
        }

        [Test]
        public void TestCreateNewLineType()
        {
            var lineTypeName = CommonTestMethods.GetTestLineTypeName();

            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var newLineType1 = tr.Style.CreateLineType(lineTypeName);
                Assert.IsNotNull(newLineType1);
                Assert.AreEqual(lineTypeName, newLineType1.Name);
                var newLineType2 = tr.Style.CreateLineType(lineTypeName);
                Assert.IsNull(newLineType2);
            });
        }
    }
}