using System;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    /// <summary>
    ///     TransactionServiceForStyle 的单元测试，专注于图层状态管理功能
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TransactionServiceForStyleTests
    {
        [Test]
        public void TestCaptureAllLayerStates_ReturnsSnapshot()
        {
            void Action(ITransactionService tr)
            {
                var result = tr.Style.CaptureAllLayerStates();
                Assert.IsTrue(result.IsSuccess, "捕获图层状态应成功");
                Assert.IsNotNull(result.Data, "快照数据不应为 null");
                Assert.Greater(result.Data.States.Count, 0, "快照应至少包含图层0");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestUnlockAndThawAllLayers_SuccessfullyUnlocksAndThaws()
        {
            void Action1(ITransactionService tr)
            {
                var testLayerName = CommonTestMethods.GetTestLayerName();
                var layer = tr.Style.CreateLayer(testLayerName);
                Assert.IsNotNull(layer, "创建测试图层应成功");

                layer.IsLocked = true;
                layer.IsFrozen = true;
            }

            void Action2(ITransactionService tr)
            {
                var result = tr.Style.UnlockAndThawAllLayers();
                Assert.IsTrue(result.IsSuccess, "解锁解冻操作应成功");
            }

            CadServiceManager._.ExecuteInTransactions("", Action1, Action2);
        }

        [Test]
        public void TestRestoreLayerStates_NullSnapshot_ReturnsSuccess()
        {
            void Action(ITransactionService tr)
            {
                var result = tr.Style.RestoreLayerStates(null);
                Assert.IsTrue(result.IsSuccess, "传入 null 快照应返回成功");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestRestoreLayerStates_EmptySnapshot_ReturnsSuccess()
        {
            void Action(ITransactionService tr)
            {
                var emptySnapshot = new LayerStateSnapshot();
                var result = tr.Style.RestoreLayerStates(emptySnapshot);
                Assert.IsTrue(result.IsSuccess, "传入空快照应返回成功");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestRestoreLayerStates_SkipsLayer0Freeze()
        {
            void Action(ITransactionService tr)
            {
                var layer0 = tr.Style.GetLayer("0");
                Assert.IsNotNull(layer0, "图层0应始终存在");

                var snapshot = new LayerStateSnapshot();
                snapshot.States[layer0.Id] = new LayerStateEntry
                {
                    IsLocked = false,
                    IsFrozen = true
                };

                var result = tr.Style.RestoreLayerStates(snapshot);
                Assert.IsTrue(result.IsSuccess, "恢复图层状态应成功");

                var layer0After = tr.Style.GetLayer("0");
                Assert.IsFalse(layer0After.IsFrozen, "图层0不应被冻结");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestRestoreLayerStates_HandlesInvalidLayerId()
        {
            void Action(ITransactionService tr)
            {
                var snapshot = new LayerStateSnapshot();
                snapshot.States[ObjectId.Null] = new LayerStateEntry
                {
                    IsLocked = true,
                    IsFrozen = true
                };

                var testLayerName = CommonTestMethods.GetTestLayerName();
                var layer = tr.Style.CreateLayer(testLayerName);
                snapshot.States[layer.Id] = new LayerStateEntry
                {
                    IsLocked = true,
                    IsFrozen = false
                };

                var result = tr.Style.RestoreLayerStates(snapshot);
                Assert.IsTrue(result.IsSuccess, "即使包含无效ID，恢复操作也应成功");

                var restoredLayer = tr.Style.GetLayer(testLayerName);
                Assert.IsTrue(restoredLayer.IsLocked, "有效图层的状态应被恢复");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestRestoreLayerStates_SkipsCurrentLayerFreeze()
        {
            void Action(ITransactionService tr)
            {
                var db = HostApplicationServices.WorkingDatabase;
                var currentLayerId = db.Clayer;
                
                var snapshot = new LayerStateSnapshot();
                snapshot.States[currentLayerId] = new LayerStateEntry
                {
                    IsLocked = false,
                    IsFrozen = true
                };

                var result = tr.Style.RestoreLayerStates(snapshot);
                Assert.IsTrue(result.IsSuccess, "恢复图层状态应成功");

                var currentLayerAfter = tr.GetObject<LayerTableRecord>(currentLayerId);
                Assert.IsFalse(currentLayerAfter.IsFrozen, "当前图层不应被冻结");
            }

            CadServiceManager._.ExecuteInTransactions("", Action);
        }

        [Test]
        public void TestCaptureAndRestore_BasicWorkflow()
        {
            void Action1(ITransactionService tr)
            {
                var layerName = CommonTestMethods.GetTestLayerName();
                var layer = tr.Style.CreateLayer(layerName);
                layer.IsLocked = true;
            }

            void Action2(ITransactionService tr)
            {
                var snapshot = tr.Style.CaptureAllLayerStates();
                Assert.IsTrue(snapshot.IsSuccess, "捕获状态应成功");
                Assert.Greater(snapshot.Data.States.Count, 0, "快照应包含图层");
            }

            CadServiceManager._.ExecuteInTransactions("", Action1, Action2);
        }
    }
}
