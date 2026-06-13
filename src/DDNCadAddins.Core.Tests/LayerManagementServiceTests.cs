using System.Linq;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Tests.Fakes;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class LayerManagementServiceTests
    {
        private FakeLayerRepository _repository;
        private LayerManagementService _service;

        [SetUp]
        public void SetUp()
        {
            _repository = new FakeLayerRepository
            {
                Layers =
                {
                    new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                    new LayerInfo { Name = "Walls", IsLocked = true, IsFrozen = true },
                    new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false }
                },
                CurrentLayerName = "0"
            };
            _service = new LayerManagementService(_repository);
        }

        [Test]
        public void CaptureAllLayerStates_ReturnsAllLayers()
        {
            var result = _service.CaptureAllLayerStates();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.States.Count);
            Assert.IsTrue(result.Data.States["Walls"].IsLocked);
            Assert.IsTrue(result.Data.States["Walls"].IsFrozen);
        }

        [Test]
        public void CaptureAllLayerStates_EmptyLayerTable_ReturnsEmptySnapshot()
        {
            _repository.Layers.Clear();

            var result = _service.CaptureAllLayerStates();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.States.Count);
        }

        [Test]
        public void CaptureAllLayerStates_RepositoryFails_ReturnsFail()
        {
            _repository.ShouldFailGetAll = true;

            var result = _service.CaptureAllLayerStates();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotEmpty(result.Message);
        }

        [Test]
        public void UnlockAndThawAllLayers_UnlocksAndThawsAllLayers()
        {
            var result = _service.UnlockAndThawAllLayers();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(_repository.Layers.All(layer => !layer.IsLocked && !layer.IsFrozen));
            Assert.AreEqual(3, _repository.UpdatedLayers.Count);
        }

        [Test]
        public void UnlockAndThawAllLayers_RepositoryFails_ReturnsFail()
        {
            _repository.ShouldFailGetAll = true;

            var result = _service.UnlockAndThawAllLayers();

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void UnlockAndThawAllLayers_SkipsInvalidLayerUpdate()
        {
            _repository.UpdateFailLayerNames.Add("Walls");

            var result = _service.UnlockAndThawAllLayers();

            Assert.IsTrue(result.IsSuccess);
            var walls = _repository.Layers.First(item => item.Name == "Walls");
            Assert.IsTrue(walls.IsLocked, "更新失败的图层应保持原状");
            Assert.IsTrue(walls.IsFrozen);
            Assert.IsFalse(_repository.Layers.First(item => item.Name == "Doors").IsLocked);
            Assert.IsFalse(_repository.Layers.First(item => item.Name == "0").IsLocked);
        }

        [Test]
        public void RestoreLayerStates_SkipsInvalidLayerUpdate()
        {
            _repository.UpdateFailLayerNames.Add("Walls");

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Walls"] = new LayerStateEntry { IsLocked = true, IsFrozen = true };
            snapshot.States["Doors"] = new LayerStateEntry { IsLocked = true, IsFrozen = false };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(_repository.Layers.First(item => item.Name == "Doors").IsLocked);
        }

        [Test]
        public void RestoreLayerStates_NullSnapshot_ReturnsSuccess()
        {
            var result = _service.RestoreLayerStates(null);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, _repository.UpdatedLayers.Count);
        }

        [Test]
        public void RestoreLayerStates_EmptySnapshot_ReturnsSuccess()
        {
            var result = _service.RestoreLayerStates(new LayerStateSnapshot());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, _repository.UpdatedLayers.Count);
        }

        [Test]
        public void RestoreLayerStates_RestoresLockedAndFrozenStates()
        {
            _repository.Layers.First(layer => layer.Name == "Walls").IsLocked = false;
            _repository.Layers.First(layer => layer.Name == "Walls").IsFrozen = false;

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Walls"] = new LayerStateEntry { IsLocked = true, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            var walls = _repository.Layers.First(layer => layer.Name == "Walls");
            Assert.IsTrue(walls.IsLocked);
            Assert.IsTrue(walls.IsFrozen);
        }

        [Test]
        public void RestoreLayerStates_SkipsLayer0Freeze()
        {
            var snapshot = new LayerStateSnapshot();
            snapshot.States["0"] = new LayerStateEntry { IsLocked = false, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            var layer0 = _repository.Layers.First(layer => layer.Name == "0");
            Assert.IsFalse(layer0.IsFrozen, "图层0不应被冻结");
        }

        [Test]
        public void RestoreLayerStates_SkipsCurrentLayerFreeze()
        {
            _repository.CurrentLayerName = "Doors";

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Doors"] = new LayerStateEntry { IsLocked = false, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            var doors = _repository.Layers.First(layer => layer.Name == "Doors");
            Assert.IsFalse(doors.IsFrozen, "当前图层不应被冻结");
        }

        [Test]
        public void RestoreLayerStates_SkipsMissingLayer()
        {
            var snapshot = new LayerStateSnapshot();
            snapshot.States["MissingLayer"] = new LayerStateEntry { IsLocked = true, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, _repository.UpdatedLayers.Count);
        }

        [Test]
        public void RestoreLayerStates_GetCurrentLayerFails_ReturnsFail()
        {
            _repository.ShouldFailGetCurrentLayer = true;

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Walls"] = new LayerStateEntry { IsLocked = true, IsFrozen = false };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsFalse(result.IsSuccess);
        }
    }
}
