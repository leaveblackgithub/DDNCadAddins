using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    /// <summary>
    ///     TransactionService 的扩展测试覆盖，专注于边界情况和错误处理
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TransactionServiceExtendedTests
    {
        [Test]
        public void TestGetObject_NullId_ReturnsNull()
        {
            void Action(ITransactionService tr)
            {
                var result = tr.GetObject<BlockReference>(ObjectId.Null);
                Assert.IsNull(result, "传入 ObjectId.Null 应返回 null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestAppendEntityToModelSpace_NullEntity_ReturnsNullId()
        {
            void Action(ITransactionService tr)
            {
                var id = tr.AppendEntityToModelSpace(null);
                Assert.AreEqual(ObjectId.Null, id, "传入 null 实体应返回 ObjectId.Null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestAppendEntitiesToCurrentSpace_MultipleEntities_AllAdded()
        {
            void Action(ITransactionService tr)
            {
                var entities = new List<Entity>
                {
                    new Line(new Point3d(0, 0, 0), new Point3d(5, 0, 0)),
                    new Circle(new Point3d(10, 10, 0), Vector3d.ZAxis, 3.0)
                };
                var ids = tr.AppendEntitiesToCurrentSpace(entities);
                Assert.AreEqual(2, ids.Count, "应添加2个实体");
                foreach (var id in ids)
                    Assert.AreNotEqual(ObjectId.Null, id, "每个实体ID都应有效");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestFilterObjects_WithAlwaysFalseFilter_ReturnsEmpty()
        {
            void Action(ITransactionService tr)
            {
                var ids = tr.GetChildObjectsFromModelspace<BlockReference>();
                if (ids.Count == 0)
                {
                    Assert.Inconclusive("模型空间中没有块参照，跳过此测试");
                    return;
                }

                var filtered = tr.FilterObjects<BlockReference>(ids, _ => false);
                Assert.AreEqual(0, filtered.Count, "始终返回 false 的过滤器应返回空列表");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetBlockTableRecordId_NonExistentName_IsNull()
        {
            void Action(ITransactionService tr)
            {
                var id = tr.GetBlockTableRecordId("__THIS_BLOCK_DOES_NOT_EXIST__");
                Assert.IsTrue(id.IsNull, "不存在的块名应返回 null ID");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetValidColorIndex_OutOfRange_ReturnsDefault()
        {
            void Action(ITransactionService tr)
            {
                var result1 = tr.Style.GetValidColorIndex(-1);
                Assert.AreEqual(CadServiceManager.ColorIndexWhite, result1,
                    "负数颜色索引应返回默认值");

                var result2 = tr.Style.GetValidColorIndex(256);
                Assert.AreEqual(CadServiceManager.ColorIndexWhite, result2,
                    "超出范围的颜色索引应返回默认值");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetValidLayerName_EmptyString_ReturnsLayer0()
        {
            void Action(ITransactionService tr)
            {
                var name = tr.Style.GetValidLayerName(string.Empty);
                Assert.AreEqual(CadServiceManager.Layer0, name,
                    "空字符串应返回图层0");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetLayer_Nonexistent_ReturnsNull()
        {
            void Action(ITransactionService tr)
            {
                var layer = tr.Style.GetLayer("__LAYER_NOT_EXISTS__");
                Assert.IsNull(layer, "不存在的图层应返回 null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestCreateLayer_DuplicateName_ReturnsNull()
        {
            void Action(ITransactionService tr)
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer1 = tr.Style.CreateLayer(layerName);
                Assert.IsNotNull(layer1);

                var layer2 = tr.Style.CreateLayer(layerName);
                Assert.IsNull(layer2, "重复图层名应返回 null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetBlockTable_ReturnsNotNull()
        {
            void Action(ITransactionService tr)
            {
                var bt = tr.GetBlockTable();
                Assert.IsNotNull(bt, "GetBlockTable 应返回有效的块表");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestAppendEntityToBlockTableRecord_ValidEntity_ReturnsValidId()
        {
            void Action(ITransactionService tr)
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var line = new Line(new Point3d(0, 0, 0), new Point3d(3, 3, 0));
                var id = tr.AppendEntityToBlockTableRecord(modelSpace, line);
                Assert.AreNotEqual(ObjectId.Null, id, "应返回有效 ID");
                Assert.IsTrue(id.IsValid);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestAppendEntityToBlockTableRecord_NullEntity_ReturnsNullId()
        {
            void Action(ITransactionService tr)
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var id = tr.AppendEntityToBlockTableRecord(modelSpace, null);
                Assert.AreEqual(ObjectId.Null, id, "传入 null 实体应返回 ObjectId.Null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestAppendEntitiesToBlockTableRecord_MultipleEntities_AllAdded()
        {
            void Action(ITransactionService tr)
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var entities = new List<Entity>
                {
                    new Line(new Point3d(0, 0, 0), new Point3d(1, 0, 0)),
                    new Circle(new Point3d(5, 5, 0), Vector3d.ZAxis, 2.0)
                };
                var ids = tr.AppendEntitiesToBlockTableRecord(modelSpace, entities);
                Assert.AreEqual(2, ids.Count, "应添加 2 个实体");
                foreach (var id in ids)
                    Assert.IsTrue(id.IsValid, "每个 ID 都应有效");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetChildObjects_WithFilter_ReturnsOnlyMatching()
        {
            void Action(ITransactionService tr)
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var line = new Line(new Point3d(999, 999, 0), new Point3d(1000, 999, 0));
                tr.AppendEntityToBlockTableRecord(modelSpace, line);

                var modelSpaceRead = tr.GetModelSpace();
                var filtered = tr.GetChildObjects<Line>(modelSpaceRead, l => l.StartPoint.X >= 999);
                Assert.Greater(filtered.Count, 0, "过滤后应至少有 1 条线");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetCurrentLayerName_ReturnsNonEmpty()
        {
            void Action(ITransactionService tr)
            {
                var name = tr.Style.GetCurrentLayerName();
                Assert.IsNotEmpty(name, "当前图层名不应为空");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetOrCreateLayer_NewName_CreatesLayer()
        {
            void Action(ITransactionService tr)
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer = tr.Style.GetOrCreateLayer(layerName);
                Assert.IsNotNull(layer, "GetOrCreateLayer 对新图层名应返回有效对象");
                Assert.AreEqual(layerName, layer.Name);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetOrCreateLayer_ExistingName_ReturnsExisting()
        {
            void Action(ITransactionService tr)
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer1 = tr.Style.GetOrCreateLayer(layerName);
                var layer2 = tr.Style.GetOrCreateLayer(layerName);
                Assert.IsNotNull(layer1);
                Assert.IsNotNull(layer2, "GetOrCreateLayer 对已存在图层名应返回对象而非 null");
                Assert.AreEqual(layer1.Name, layer2.Name);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetValidLineTypeName_EmptyString_ReturnsContinuous()
        {
            void Action(ITransactionService tr)
            {
                var name = tr.Style.GetValidLineTypeName(string.Empty);
                Assert.IsNotEmpty(name, "空字符串应返回有效的线型名");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetValidLineTypeName_InvalidName_ReturnsFallback()
        {
            void Action(ITransactionService tr)
            {
                var name = tr.Style.GetValidLineTypeName("__LINETYPE_NOT_EXISTS__");
                Assert.IsNotEmpty(name, "无效线型名应返回回退值而非空字符串");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetLineType_ExistingContinuous_ReturnsNotNull()
        {
            void Action(ITransactionService tr)
            {
                var lt = tr.Style.GetLineType(CadServiceManager.LineTypeContinuous);
                Assert.IsNotNull(lt, "Continuous 线型应始终存在");
                Assert.AreEqual(CadServiceManager.LineTypeContinuous, lt.Name);
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestGetLineType_NonExistent_ReturnsNull()
        {
            void Action(ITransactionService tr)
            {
                var lt = tr.Style.GetLineType(CommonTestMethods.GetTestLineTypeName());
                Assert.IsNull(lt, "不存在的线型应返回 null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }
    }
}

