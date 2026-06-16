using System.Collections.Generic;
using System.Linq;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using Moq;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class LayerManagementServiceTests
    {
        private Mock<ILayerRepository> _repoMock;
        private LayerManagementService _service;

        [SetUp]
        public void SetUp()
        {
            _repoMock = new Mock<ILayerRepository>(MockBehavior.Loose);
            _service = new LayerManagementService(_repoMock.Object);

            // 默认配置（测试可覆盖）
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Success("0"));
            _repoMock.Setup(r => r.GetLayer(It.IsAny<string>()))
                .Returns((string name) => OpResult<LayerInfo>.Success(new LayerInfo { Name = name }));
        }

        [Test]
        public void CaptureAllLayerStates_ReturnsAllLayers()
        {
            var layers = new[]
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = true, IsFrozen = true },
                new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false },
            }.ToList().AsReadOnly();
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers));

            var result = _service.CaptureAllLayerStates();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.States.Count);
            Assert.IsTrue(result.Data.States["Walls"].IsLocked);
            Assert.IsTrue(result.Data.States["Walls"].IsFrozen);
        }

        [Test]
        public void CaptureAllLayerStates_EmptyLayerTable_ReturnsEmptySnapshot()
        {
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(new List<LayerInfo>().AsReadOnly()));

            var result = _service.CaptureAllLayerStates();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.States.Count);
        }

        [Test]
        public void CaptureAllLayerStates_RepositoryFails_ReturnsFail()
        {
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Fail("模拟获取失败"));

            var result = _service.CaptureAllLayerStates();

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void UnlockAndThawAllLayers_UnlocksAndThawsAllLayers()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = true, IsFrozen = true },
                new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.UpdateLayer(It.IsAny<LayerInfo>()))
                .Returns(OpResult.Success());

            var result = _service.UnlockAndThawAllLayers();

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Walls" && !l.IsLocked && !l.IsFrozen)), Times.Once);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Doors")), Times.Once);
        }

        [Test]
        public void UnlockAndThawAllLayers_RepositoryFails_ReturnsFail()
        {
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Fail("模拟获取失败"));

            var result = _service.UnlockAndThawAllLayers();

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void UnlockAndThawAllLayers_SkipsInvalidLayerUpdate()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = true, IsFrozen = true },
                new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Walls")))
                .Returns(OpResult.Fail("模拟更新失败"));
            _repoMock.Setup(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name != "Walls")))
                .Returns(OpResult.Success());

            var result = _service.UnlockAndThawAllLayers();

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Walls")), Times.Once);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Doors")), Times.Once);
        }

        [Test]
        public void RestoreLayerStates_SkipsInvalidLayerUpdate()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Success("0"));
            _repoMock.Setup(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Walls")))
                .Returns(OpResult.Fail("模拟更新失败"));
            _repoMock.Setup(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name != "Walls")))
                .Returns(OpResult.Success());

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Walls"] = new LayerStateEntry { IsLocked = true, IsFrozen = true };
            snapshot.States["Doors"] = new LayerStateEntry { IsLocked = true, IsFrozen = false };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Doors" && l.IsLocked)), Times.Once);
        }

        [Test]
        public void RestoreLayerStates_NullSnapshot_ReturnsSuccess()
        {
            var result = _service.RestoreLayerStates(null);

            Assert.IsTrue(result.IsSuccess);
        }

        [Test]
        public void RestoreLayerStates_EmptySnapshot_ReturnsSuccess()
        {
            var result = _service.RestoreLayerStates(new LayerStateSnapshot());

            Assert.IsTrue(result.IsSuccess);
        }

        [Test]
        public void RestoreLayerStates_RestoresLockedAndFrozenStates()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Success("0"));
            _repoMock.Setup(r => r.UpdateLayer(It.IsAny<LayerInfo>()))
                .Returns(OpResult.Success());

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Walls"] = new LayerStateEntry { IsLocked = true, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Walls" && l.IsLocked && l.IsFrozen)), Times.Once);
        }

        [Test]
        public void RestoreLayerStates_SkipsLayer0Freeze()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Success("0"));
            _repoMock.Setup(r => r.GetLayer("0"))
                .Returns(OpResult<LayerInfo>.Success(new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false }));
            _repoMock.Setup(r => r.UpdateLayer(It.IsAny<LayerInfo>()))
                .Returns(OpResult.Success());

            var snapshot = new LayerStateSnapshot();
            snapshot.States["0"] = new LayerStateEntry { IsLocked = false, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "0" && !l.IsFrozen)), Times.Once);
        }

        [Test]
        public void RestoreLayerStates_SkipsCurrentLayerFreeze()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Success("Doors"));
            _repoMock.Setup(r => r.GetLayer("Doors"))
                .Returns(OpResult<LayerInfo>.Success(new LayerInfo { Name = "Doors", IsLocked = false, IsFrozen = false }));
            _repoMock.Setup(r => r.UpdateLayer(It.IsAny<LayerInfo>()))
                .Returns(OpResult.Success());

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Doors"] = new LayerStateEntry { IsLocked = false, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.Is<LayerInfo>(l => l.Name == "Doors" && !l.IsFrozen)), Times.Once);
        }

        [Test]
        public void RestoreLayerStates_SkipsMissingLayer()
        {
            var layers = new List<LayerInfo>
            {
                new LayerInfo { Name = "0", IsLocked = false, IsFrozen = false },
                new LayerInfo { Name = "Walls", IsLocked = false, IsFrozen = false },
            };
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly()));
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Success("0"));

            _repoMock.Setup(r => r.GetLayer("MissingLayer"))
                .Returns(OpResult<LayerInfo>.Fail("图层不存在"));

            var snapshot = new LayerStateSnapshot();
            snapshot.States["MissingLayer"] = new LayerStateEntry { IsLocked = true, IsFrozen = true };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsTrue(result.IsSuccess);
            _repoMock.Verify(r => r.UpdateLayer(It.IsAny<LayerInfo>()), Times.Never);
        }

        [Test]
        public void RestoreLayerStates_GetCurrentLayerFails_ReturnsFail()
        {
            _repoMock.Setup(r => r.GetAllLayers())
                .Returns(OpResult<IReadOnlyList<LayerInfo>>.Success(new List<LayerInfo>().AsReadOnly()));
            _repoMock.Setup(r => r.GetCurrentLayerName())
                .Returns(OpResult<string>.Fail("模拟获取失败"));

            var snapshot = new LayerStateSnapshot();
            snapshot.States["Walls"] = new LayerStateEntry { IsLocked = true, IsFrozen = false };

            var result = _service.RestoreLayerStates(snapshot);

            Assert.IsFalse(result.IsSuccess);
        }
    }
}
