using System;
using System.Collections.Generic;
using System.Threading;
using AddinsAcad.ServiceTests;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    /// <summary>
    ///     BlockService 扩展测试，覆盖更多边界情况和属性继承逻辑
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BlockServiceExtendedTests
    {
        // ────────────────────────────────────────────────────────────────
        // IsXclipped
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TestIsXclipped_XclippedBlock_ReturnsTrue()
        {
            void Action(ITransactionService tr)
            {
                var blkService = CommonTestMethods.GetFirstBlkServiceOf23432(tr);
                Assert.IsNotNull(blkService);
                Assert.IsTrue(blkService.IsXclipped(), "名为 23432 的块应是 XClipped");
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action);
        }

        [Test]
        public void TestIsXclipped_CreatedBlock_ReturnsFalse()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull)
                {
                    Assert.Inconclusive("测试块创建失败，跳过此测试");
                    return;
                }

                var blkService = tr.Block.GetBlockService(refId);
                Assert.IsNotNull(blkService);
                Assert.IsFalse(blkService.IsXclipped(), "新创建的测试块不应有 XClip");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        // ────────────────────────────────────────────────────────────────
        // HasAttributes
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TestHasAttributes_BlockWithAttributes_ReturnsTrue()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull)
                {
                    Assert.Inconclusive("测试块创建失败，跳过此测试");
                    return;
                }

                var blkService = tr.Block.GetBlockService(refId);
                Assert.IsNotNull(blkService);
                Assert.IsTrue(blkService.HasAttributes(), "含属性定义的测试块应返回 true");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        // ────────────────────────────────────────────────────────────────
        // ExplodeAsShown — 成功路径
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TestExplodeAsShown_ValidBlock_ReturnsSuccess()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull)
                {
                    Assert.Fail("测试块创建失败");
                    return;
                }

                var blkService = tr.Block.GetBlockService(refId);
                var result = blkService.ExplodeAsShown();

                Assert.IsTrue(result.IsSuccess, $"爆炸应成功，实际消息: {result.Message}");
                Assert.IsNotNull(result.Data);
                Assert.Greater(result.Data.EntityIds.Count, 0, "爆炸后应产生至少1个实体");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestExplodeAsShown_AttributesConvertedToText()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull)
                {
                    Assert.Fail("测试块创建失败");
                    return;
                }

                var blkService = tr.Block.GetBlockService(refId);
                var result = blkService.ExplodeAsShown();

                if (!result.IsSuccess)
                {
                    Assert.Fail($"爆炸操作失败: {result.Message}");
                    return;
                }

                // 爆炸结果中应包含属性值 "属性值1"
                var textIds = tr.FilterObjects<DBText>(result.Data.EntityIds,
                    t => t.TextString == BlockServiceTestUtils.StrValue1);
                Assert.AreEqual(1, textIds.Count, "应有1个文本对象的值等于 StrValue1");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestExplodeAsShown_AllEntitiesAddedToSpace()
        {
            long[] handles = null;

            void Action1(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                var blkService = tr.Block.GetBlockService(refId);
                var result = blkService.ExplodeAsShown();

                if (!result.IsSuccess) return;

                handles = new long[result.Data.EntityIds.Count];
                for (var i = 0; i < result.Data.EntityIds.Count; i++)
                    handles[i] = result.Data.EntityIds[i].Handle.Value;
            }

            void Action2(ITransactionService tr)
            {
                if (handles == null || handles.Length == 0) return;

                foreach (var h in handles)
                {
                    CadServiceManager._.CadDb.TryGetObjectId(new Handle(h), out var id);
                    Assert.IsTrue(id.IsValid, $"实体 {h} 应在数据库中存在");
                }
            }

            CadServiceManager._.ExecuteInTransactions("", Action1, Action2);
        }

        // ────────────────────────────────────────────────────────────────
        // ExplodeAsShown — 属性继承
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TestExplodeAsShown_Layer0Child_InheritsBlkLayer()
        {
            void Action(ITransactionService tr)
            {
                var refId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(tr);
                if (refId.IsNull)
                {
                    Assert.Fail("测试块创建失败");
                    return;
                }

                var blkService = tr.Block.GetBlockService(refId);
                var result = blkService.ExplodeAsShown();
                if (!result.IsSuccess)
                {
                    Assert.Fail($"爆炸失败: {result.Message}");
                    return;
                }

                // Layer0 的子实体应继承 TestLayer
                var layerMatches = tr.FilterObjects<Entity>(result.Data.EntityIds,
                    e => e.Layer == BlockServiceTestUtils.NameTestLayer);
                Assert.Greater(layerMatches.Count, 0,
                    "应有实体继承了块参照的图层 TestLayer");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        // ────────────────────────────────────────────────────────────────
        // GetBlockService — 缓存
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TestGetBlockService_CalledTwice_ReturnsSameInstance()
        {
            void Action(ITransactionService tr)
            {
                var ids = CommonTestMethods.GetBlkRefIdsOf23432(tr);
                if (ids.Count == 0)
                {
                    Assert.Inconclusive("无可用块参照，跳过缓存测试");
                    return;
                }

                var service1 = tr.Block.GetBlockService(ids[0]);
                var service2 = tr.Block.GetBlockService(ids[0]);
                Assert.AreSame(service1, service2, "同一 ObjectId 应返回缓存的同一实例");
            }

            CadServiceManager._.ExecuteInTransactions("xclip", Action);
        }

        [Test]
        public void TestGetBlockService_NullId_ReturnsNull()
        {
            void Action(ITransactionService tr)
            {
                var service = tr.Block.GetBlockService(ObjectId.Null);
                Assert.IsNull(service, "传入 ObjectId.Null 应返回 null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        // ────────────────────────────────────────────────────────────────
        // 创建块定义
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TestCreateBlockDef_WithEntities_ReturnsValidId()
        {
            void Action(ITransactionService tr)
            {
                var entities = new List<Entity>
                {
                    new Line(new Point3d(0, 0, 0), new Point3d(5, 5, 0))
                };
                var blkDefId = tr.Block.CreateBlockDef(entities, "TestBlockDef_" + Guid.NewGuid().ToString("N"));
                Assert.IsFalse(blkDefId.IsNull, "创建块定义应返回有效 ID");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestCreateBlockDef_EmptyEntities_ReturnsNullId()
        {
            void Action(ITransactionService tr)
            {
                var blkDefId = tr.Block.CreateBlockDef(new List<Entity>(), "EmptyBlock");
                Assert.AreEqual(ObjectId.Null, blkDefId, "空实体集合应返回 ObjectId.Null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestCreateBlockRefInCurrentSpace_ValidDef_ReturnsValidId()
        {
            void Action(ITransactionService tr)
            {
                var entities = new List<Entity>
                {
                    new Circle(new Point3d(0, 0, 0), Vector3d.ZAxis, 2.0)
                };
                var blkDefId = tr.Block.CreateBlockDef(entities, "TestRef_" + Guid.NewGuid().ToString("N"));
                var refId = tr.Block.CreateBlockRefInCurrentSpace(blkDefId);
                Assert.IsFalse(refId.IsNull, "创建块参照应返回有效 ID");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestCreateBlockRefInCurrentSpace_NullDefId_ReturnsNullId()
        {
            void Action(ITransactionService tr)
            {
                var refId = tr.Block.CreateBlockRefInCurrentSpace(ObjectId.Null);
                Assert.AreEqual(ObjectId.Null, refId, "传入 Null ID 应返回 ObjectId.Null");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }
    }
}
