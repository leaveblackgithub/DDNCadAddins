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

        [Test]
        public void CleanupNonXclippedBlocks_CancellationRequested_ReturnsFail()
        {
            var explodedCount = 0;
            var options = new BlockCleanupOptions
            {
                IsCancellationRequested = () => explodedCount >= 1,
                OnBlockExploded = _ => explodedCount++
            };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BlockCleanupOptions.CancelledMessage, result.Message);
            Assert.AreEqual(1, _repository.ExplodedBlockIds.Count);
        }

        [Test]
        public void CleanupNonXclippedBlocks_OnBlockExploded_InvokesCallback()
        {
            var callbackCount = 0;
            var options = new BlockCleanupOptions
            {
                OnBlockExploded = _ => callbackCount++
            };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, callbackCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_BlockExplodeReport_HasCorrectSequenceInfo()
        {
            var reports = new System.Collections.Generic.List<BlockExplodeReport>();
            var options = new BlockCleanupOptions
            {
                OnBlockExploded = report => reports.Add(report)
            };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, reports.Count);

            // BlockA (Id="1") should be first in round 1
            var blockAReport = reports.FirstOrDefault(r => r.BlockName == "BlockA");
            Assert.IsNotNull(blockAReport);
            Assert.AreEqual(1, blockAReport.Index);
            Assert.AreEqual(2, blockAReport.TotalCount); // 2 non-xclipped blocks in this round
            Assert.AreEqual(1, blockAReport.RoundNumber);

            // BlockC (Id="3") should be second in round 1
            var blockCReport = reports.FirstOrDefault(r => r.BlockName == "BlockC");
            Assert.IsNotNull(blockCReport);
            Assert.AreEqual(2, blockCReport.Index);
            Assert.AreEqual(2, blockCReport.TotalCount);
            Assert.AreEqual(1, blockCReport.RoundNumber);
        }

        [Test]
        public void CleanupNonXclippedBlocks_MultipleRounds_ReportsHaveCorrectRoundNumbers()
        {
            _repository.Blocks.Clear();
            _repository.Blocks.Add(new BlockInfo { Id = "10", Name = "Outer1", IsXclipped = false });
            _repository.ExplodeEntityCounts["10"] = 1;
            _repository.FollowUpBlocksAfterExplode["10"] = new BlockInfo { Id = "11", Name = "Inner1", IsXclipped = false };
            _repository.ExplodeEntityCounts["11"] = 1;
            _repository.FollowUpBlocksAfterExplode["11"] = new BlockInfo { Id = "12", Name = "Inner2", IsXclipped = false };
            _repository.ExplodeEntityCounts["12"] = 1;

            var reports = new System.Collections.Generic.List<BlockExplodeReport>();
            var options = new BlockCleanupOptions
            {
                OnBlockExploded = report => reports.Add(report)
            };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(reports.Count, 2);

            // First round should have round number 1
            var firstRoundReports = reports.Where(r => r.RoundNumber == 1).ToList();
            Assert.GreaterOrEqual(firstRoundReports.Count, 1);

            // Subsequent rounds should have increasing round numbers
            var maxRound = reports.Max(r => r.RoundNumber);
            Assert.GreaterOrEqual(maxRound, 1);

            // All reports should have valid Index and TotalCount
            foreach (var report in reports)
            {
                Assert.Greater(report.Index, 0);
                Assert.Greater(report.TotalCount, 0);
                Assert.GreaterOrEqual(report.RoundNumber, 1);
            }
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_NoDuplicateProcessing()
        {
            _repository.Blocks.Clear();
            _repository.Blocks.Add(new BlockInfo { Id = "20", Name = "Block1", IsXclipped = false });
            _repository.Blocks.Add(new BlockInfo { Id = "21", Name = "Block2", IsXclipped = false });
            _repository.Blocks.Add(new BlockInfo { Id = "22", Name = "Block3", IsXclipped = false });
            _repository.ExplodeEntityCounts["20"] = 1;
            _repository.ExplodeEntityCounts["21"] = 1;
            _repository.ExplodeEntityCounts["22"] = 1;

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);

            // Verify each block was processed exactly once
            Assert.AreEqual(3, _repository.ExplodedBlockIds.Count);
            Assert.AreEqual(1, _repository.ExplodedBlockIds.Count(id => id == "20"));
            Assert.AreEqual(1, _repository.ExplodedBlockIds.Count(id => id == "21"));
            Assert.AreEqual(1, _repository.ExplodedBlockIds.Count(id => id == "22"));
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_EarlyTerminationAfterEmptyRounds()
        {
            _repository.Blocks.Clear();
            _repository.Blocks.Add(new BlockInfo { Id = "30", Name = "NoFollowUpBlock", IsXclipped = false });
            _repository.ExplodeEntityCounts["30"] = 1;

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.LessOrEqual(result.Data.IterationCount, 3);
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_SameNameBlocksBatched()
        {
            _repository.Blocks.Clear();
            _repository.Blocks.Add(new BlockInfo { Id = "40", Name = "SameName", IsXclipped = false });
            _repository.Blocks.Add(new BlockInfo { Id = "41", Name = "SameName", IsXclipped = false });
            _repository.Blocks.Add(new BlockInfo { Id = "42", Name = "SameName", IsXclipped = false });
            _repository.ExplodeEntityCounts["40"] = 1;
            _repository.ExplodeEntityCounts["41"] = 1;
            _repository.ExplodeEntityCounts["42"] = 1;

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
            Assert.AreEqual(1, result.Data.IterationCount);
            // Same name blocks should be aggregated into 1 report
            Assert.AreEqual(1, result.Data.Rounds[0].ExplodeReports.Count);
            Assert.AreEqual(3, result.Data.Rounds[0].ExplodeReports[0].AggregatedCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_NameBasedDeduplication()
        {
            _repository.Blocks.Clear();
            _repository.Blocks.Add(new BlockInfo { Id = "50", Name = "SharedBlock", IsXclipped = false });
            _repository.ExplodeEntityCounts["50"] = 1;
            _repository.FollowUpBlocksAfterExplode["50"] = new BlockInfo { Id = "51", Name = "SharedBlock", IsXclipped = false };
            _repository.ExplodeEntityCounts["51"] = 1;

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data.TotalExplodedEntityCount);
            Assert.AreEqual(1, _repository.ExplodedBlockIds.Count);
        }
    }
}
