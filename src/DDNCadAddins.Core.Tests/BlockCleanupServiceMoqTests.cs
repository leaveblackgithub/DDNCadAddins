using System;
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
    public class BlockCleanupServiceMoqTests
    {
        private Mock<IBlockRepository> _repoMock;
        private BlockCleanupService _service;

        [SetUp]
        public void SetUp()
        {
            _repoMock = new Mock<IBlockRepository>(MockBehavior.Loose);
            _service = new BlockCleanupService(_repoMock.Object);
        }

        [Test]
        public void CleanupNonXclippedBlocks_ExplodesNonXclippedBlocks()
        {
            var blocks = new List<BlockInfo>
            {
                new BlockInfo { Id = "1", Name = "BlockA", IsXclipped = false },
                new BlockInfo { Id = "2", Name = "BlockB", IsXclipped = true },
                new BlockInfo { Id = "3", Name = "BlockC", IsXclipped = false },
            };

            // Setup 返回数据
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("1")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.IsBlockXclipped("2")).Returns(OpResult<bool>.Success(true));
            _repoMock.Setup(r => r.IsBlockXclipped("3")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock("1"))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 2 }));
            _repoMock.Setup(r => r.ExplodeBlock("3"))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
            // 验证调用次数
            _repoMock.Verify(r => r.ExplodeBlock("1"), Times.Once);
            _repoMock.Verify(r => r.ExplodeBlock("3"), Times.Once);
            _repoMock.Verify(r => r.ExplodeBlock("2"), Times.Never); // XCLIP 跳过
        }

        [Test]
        public void CleanupNonXclippedBlocks_NoBlocks_ReturnsEmptyResult()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>().AsReadOnly()));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_AllXclipped_SkipsAll()
        {
            var blocks = new List<BlockInfo>
            {
                new BlockInfo { Id = "1", Name = "BlockX", IsXclipped = true },
                new BlockInfo { Id = "2", Name = "BlockY", IsXclipped = true },
            };
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("1")).Returns(OpResult<bool>.Success(true));
            _repoMock.Setup(r => r.IsBlockXclipped("2")).Returns(OpResult<bool>.Success(true));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
            _repoMock.Verify(r => r.ExplodeBlock(It.IsAny<string>()), Times.Never); // 全部 XCLIP → 无爆炸
        }

        [Test]
        public void CleanupNonXclippedBlocks_ReprocessAfterExplode()
        {
            var block1 = new BlockInfo { Id = "1", Name = "Outer", IsXclipped = false };
            var blocksWithContent = new List<BlockInfo> { block1 }.AsReadOnly();
            var blocksEmpty = new List<BlockInfo>().AsReadOnly();

            _repoMock.SetupSequence(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocksWithContent))
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocksEmpty));

            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 2 }));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Data.TotalExplodedEntityCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_GetAllBlocksFails_ReturnsFail()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Fail("模拟获取失败"));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void CleanupNonXclippedBlocks_ExplodeBlockFails_LogsFailure()
        {
            var blocks = new List<BlockInfo>
            {
                new BlockInfo { Id = "1", Name = "BlockA", IsXclipped = false },
            };
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Fail("模拟爆炸失败"));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Data.FailureCounts.ContainsKey("模拟爆炸失败"));
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
        }
    }
}
