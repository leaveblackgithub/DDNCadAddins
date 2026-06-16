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
    ///     TransactionService 扩展测试 — 全部使用内存侧数据库，不依赖任何图纸.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TransactionServiceExtendedTests
    {
        [Test]
        public void TestGetObject_NullId_ReturnsNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var result = tr.GetObject<BlockReference>(ObjectId.Null);
                Assert.IsNull(result, "传入 ObjectId.Null 应返回 null");
            });
        }

        [Test]
        public void TestAppendEntityToModelSpace_NullEntity_ReturnsNullId()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var id = tr.AppendEntityToModelSpace(null);
                Assert.AreEqual(ObjectId.Null, id, "传入 null 实体应返回 ObjectId.Null");
            });
        }

        [Test]
        public void TestAppendEntitiesToCurrentSpace_MultipleEntities_AllAdded()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
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
            });
        }

        [Test]
        public void TestFilterObjects_WithAlwaysFalseFilter_ReturnsEmpty()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                // 先添加一些实体确保有内容
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var line = new Line(new Point3d(0, 0, 0), new Point3d(5, 0, 0));
                tr.AppendEntityToBlockTableRecord(modelSpace, line);
            });

            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = tr.GetChildObjectsFromModelspace<BlockReference>();
                var filtered = tr.FilterObjects<BlockReference>(ids, _ => false);
                Assert.AreEqual(0, filtered.Count, "始终返回 false 的过滤器应返回空列表");
            });
        }

        [Test]
        public void TestGetBlockTableRecordId_NonExistentName_IsNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var id = tr.GetBlockTableRecordId("__THIS_BLOCK_DOES_NOT_EXIST__");
                Assert.IsTrue(id.IsNull, "不存在的块名应返回 null ID");
            });
        }

        [Test]
        public void TestGetValidLayerName_EmptyString_ReturnsLayer0()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var name = tr.Style.GetValidLayerName(string.Empty);
                Assert.AreEqual(CadServiceManager.Layers.Default, name,
                    "空字符串应返回图层0");
            });
        }

        [Test]
        public void TestGetLayer_Nonexistent_ReturnsNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var layer = tr.Style.GetLayer("__LAYER_NOT_EXISTS__");
                Assert.IsNull(layer, "不存在的图层应返回 null");
            });
        }

        [Test]
        public void TestCreateLayer_DuplicateName_ReturnsNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer1 = tr.Style.CreateLayer(layerName);
                Assert.IsNotNull(layer1);

                var layer2 = tr.Style.CreateLayer(layerName);
                Assert.IsNull(layer2, "重复图层名应返回 null");
            });
        }

        [Test]
        public void TestGetBlockTable_ReturnsNotNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var bt = tr.GetBlockTable();
                Assert.IsNotNull(bt, "GetBlockTable 应返回有效的块表");
            });
        }

        [Test]
        public void TestAppendEntityToBlockTableRecord_ValidEntity_ReturnsValidId()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var line = new Line(new Point3d(0, 0, 0), new Point3d(3, 3, 0));
                var id = tr.AppendEntityToBlockTableRecord(modelSpace, line);
                Assert.AreNotEqual(ObjectId.Null, id, "应返回有效 ID");
                Assert.IsTrue(id.IsValid);
            });
        }

        [Test]
        public void TestAppendEntityToBlockTableRecord_NullEntity_ReturnsNullId()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var id = tr.AppendEntityToBlockTableRecord(modelSpace, null);
                Assert.AreEqual(ObjectId.Null, id, "传入 null 实体应返回 ObjectId.Null");
            });
        }

        [Test]
        public void TestAppendEntitiesToBlockTableRecord_MultipleEntities_AllAdded()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
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
            });
        }

        [Test]
        public void TestGetChildObjects_WithFilter_ReturnsOnlyMatching()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var modelSpace = tr.GetModelSpace(OpenMode.ForWrite);
                var line = new Line(new Point3d(999, 999, 0), new Point3d(1000, 999, 0));
                tr.AppendEntityToBlockTableRecord(modelSpace, line);

                var modelSpaceRead = tr.GetModelSpace();
                var filtered = tr.GetChildObjects<Line>(modelSpaceRead, l => l.StartPoint.X >= 999);
                Assert.Greater(filtered.Count, 0, "过滤后应至少有 1 条线");
            });
        }

        [Test]
        public void TestGetCurrentLayerName_ReturnsNonEmpty()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var name = tr.Style.GetCurrentLayerName();
                Assert.IsNotEmpty(name, "当前图层名不应为空");
            });
        }

        [Test]
        public void TestGetOrCreateLayer_NewName_CreatesLayer()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer = tr.Style.GetOrCreateLayer(layerName);
                Assert.IsNotNull(layer, "GetOrCreateLayer 对新图层名应返回有效对象");
                Assert.AreEqual(layerName, layer.Name);
            });
        }

        [Test]
        public void TestGetOrCreateLayer_ExistingName_ReturnsExisting()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer1 = tr.Style.GetOrCreateLayer(layerName);
                var layer2 = tr.Style.GetOrCreateLayer(layerName);
                Assert.IsNotNull(layer1);
                Assert.IsNotNull(layer2, "GetOrCreateLayer 对已存在图层名应返回对象而非 null");
                Assert.AreEqual(layer1.Name, layer2.Name);
            });
        }

        [Test]
        public void TestGetLineType_ExistingContinuous_ReturnsNotNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var lt = tr.Style.GetLineType(CadServiceManager.Linetypes.Continuous);
                Assert.IsNotNull(lt, "Continuous 线型应始终存在");
                Assert.AreEqual(CadServiceManager.Linetypes.Continuous, lt.Name);
            });
        }

        [Test]
        public void TestGetLineType_NonExistent_ReturnsNull()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var lt = tr.Style.GetLineType(CommonTestMethods.GetTestLineTypeName());
                Assert.IsNull(lt, "不存在的线型应返回 null");
            });
        }
    }
}