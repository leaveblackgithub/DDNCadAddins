using System.Linq;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Tests.Fakes;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class BlockCleanupServiceTests
    {
        private FakeBlockRepository _repository;
        private BlockCleanupService _service;

        [SetUp]
        public void SetUp()
        {
            _repository = new FakeBlockRepository
            {
                Blocks =
                {
                    new BlockInfo { Id = "1", Name = "BlockA", IsXclipped = false },
                    new BlockInfo { Id = "2", Name = "BlockB", IsXclipped = true },
                    new BlockInfo { Id = "3", Name = "BlockC", IsXclipped = false }
                }
            };
            _repository.ExplodeEntityCounts["1"] = 2;
            _repository.ExplodeEntityCounts["3"] = 1;
            _service = new BlockCleanupService(_repository);
        }

        [Test]
        public void CleanupNonXclippedBlocks_ExplodesNonXclippedBlocks()
        {
            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
            Assert.AreEqual(0, result.Data.TotalErasedEmptyBlockCount);
            CollectionAssert.AreEquivalent(new[] { "1", "3" }, _repository.ExplodedBlockIds);
            Assert.IsFalse(_repository.Blocks.Any(block => block.Id == "1" || block.Id == "3"));
        }

        [Test]
        public void CleanupNonXclippedBlocks_NoBlocks_ReturnsEmptyResult()
        {
            _repository.Blocks.Clear();

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data.IterationCount);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_RepositoryFails_ReturnsFail()
        {
            _repository.ShouldFailGetAll = true;

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void CleanupNonXclippedBlocks_SkipsXclippedBlocks()
        {
            _repository.Blocks.RemoveAll(block => block.Id == "1" || block.Id == "3");
            _repository.Blocks.Add(new BlockInfo { Id = "4", Name = "BlockD", IsXclipped = true });

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
            Assert.AreEqual(0, _repository.ExplodedBlockIds.Count);
        }

        [Test]
        public void CleanupNonXclippedBlocks_ErasesEmptyDefinitionBlock()
        {
            _repository.Blocks.RemoveAll(block => block.Id == "1" || block.Id == "3");
            _repository.Blocks.Add(new BlockInfo { Id = "5", Name = "EmptyBlock", IsXclipped = false });
            _repository.EmptyDefinitionBlockIds.Add("5");

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data.TotalErasedEmptyBlockCount);
            CollectionAssert.Contains(_repository.ErasedBlockIds, "5");
        }

        [Test]
        public void CleanupNonXclippedBlocks_RecordsExplodeFailure()
        {
            _repository.Blocks.RemoveAll(block => block.Id == "1" || block.Id == "3");
            _repository.Blocks.Add(new BlockInfo { Id = "6", Name = "FailBlock", IsXclipped = false });
            _repository.ExplodeFailBlockIds.Add("6");

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
            Assert.IsTrue(result.Data.FailureCounts.ContainsKey("模拟爆炸失败"));
            Assert.AreEqual(1, result.Data.FailureCounts["模拟爆炸失败"]);
        }

        [Test]
        public void CleanupNonXclippedBlocks_RunsMultipleRoundsWhenNestedBlocksExist()
        {
            _repository.Blocks.Clear();
            _repository.Blocks.Add(new BlockInfo { Id = "10", Name = "Outer", IsXclipped = false });
            _repository.ExplodeEntityCounts["10"] = 1;
            _repository.FollowUpBlocksAfterExplode["10"] = new BlockInfo { Id = "11", Name = "Inner", IsXclipped = false };
            _repository.ExplodeEntityCounts["11"] = 2;

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(result.Data.IterationCount, 2);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
        }
    }
}
