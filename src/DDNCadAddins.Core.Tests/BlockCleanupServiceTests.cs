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
    public class BlockCleanupServiceTests
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
            Assert.AreEqual(0, result.Data.TotalErasedEmptyBlockCount);
            _repoMock.Verify(r => r.ExplodeBlock("1"), Times.Once);
            _repoMock.Verify(r => r.ExplodeBlock("3"), Times.Once);
            _repoMock.Verify(r => r.ExplodeBlock("2"), Times.Never);
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
        public void CleanupNonXclippedBlocks_RepositoryFails_ReturnsFail()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Fail("模拟获取失败"));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void CleanupNonXclippedBlocks_SkipsXclippedBlocks()
        {
            var blocks = new List<BlockInfo>
            {
                new BlockInfo { Id = "4", Name = "BlockD", IsXclipped = true },
            };
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("4")).Returns(OpResult<bool>.Success(true));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
            _repoMock.Verify(r => r.ExplodeBlock(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void CleanupNonXclippedBlocks_ErasesEmptyDefinitionBlock()
        {
            var blocks = new List<BlockInfo>
            {
                new BlockInfo { Id = "5", Name = "EmptyBlock", IsXclipped = false },
            };
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("5")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock("5"))
                .Returns(OpResult<BlockExplodeResult>.Fail(BlockCleanupService.EmptyDefinitionMessage));
            _repoMock.Setup(r => r.EraseEmptyBlock("5"))
                .Returns(OpResult<bool>.Success(true));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data.TotalErasedEmptyBlockCount);
            _repoMock.Verify(r => r.EraseEmptyBlock("5"), Times.Once);
        }

        [Test]
        public void CleanupNonXclippedBlocks_RecordsExplodeFailure()
        {
            var blocks = new List<BlockInfo>
            {
                new BlockInfo { Id = "6", Name = "FailBlock", IsXclipped = false },
            };
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(blocks.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("6")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock("6"))
                .Returns(OpResult<BlockExplodeResult>.Fail("模拟爆炸失败"));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Data.TotalExplodedEntityCount);
            Assert.IsTrue(result.Data.FailureCounts.ContainsKey("模拟爆炸失败"));
            Assert.AreEqual(1, result.Data.FailureCounts["模拟爆炸失败"]);
        }

        [Test]
        public void CleanupNonXclippedBlocks_RunsMultipleRoundsWhenNestedBlocksExist()
        {
            var blocksState = new List<BlockInfo>
            {
                new BlockInfo { Id = "10", Name = "Outer", IsXclipped = false },
            };
            var explodedCountMap = new Dictionary<string, int>
            {
                ["10"] = 0,
                ["11"] = 0,
            };

            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(() => OpResult<IReadOnlyList<BlockInfo>>.Success(blocksState.ToList().AsReadOnly()));

            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));

            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns((string id) =>
                {
                    explodedCountMap.TryGetValue(id, out var count);
                    explodedCountMap[id] = count + 1;
                    blocksState.RemoveAll(b => b.Id == id);

                    if (id == "10")
                        blocksState.Add(new BlockInfo { Id = "11", Name = "Inner", IsXclipped = false });

                    var ec = id == "10" ? 1 : 2;
                    return OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = ec });
                });

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(result.Data.IterationCount, 2);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
            Assert.AreEqual(1, explodedCountMap["10"]);
            Assert.AreEqual(1, explodedCountMap["11"]);
        }

        [Test]
        public void CleanupNonXclippedBlocks_CancellationRequested_ReturnsFail()
        {
            var explodeCount = 0;
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "1", Name = "BlockA", IsXclipped = false },
                    new BlockInfo { Id = "3", Name = "BlockC", IsXclipped = false },
                }.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var options = new BlockCleanupOptions
            {
                IsCancellationRequested = () => explodeCount >= 1,
                OnBlockExploded = _ => explodeCount++,
            };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BlockCleanupOptions.CancelledMessage, result.Message);
        }

        [Test]
        public void CleanupNonXclippedBlocks_OnBlockExploded_InvokesCallback()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "1", Name = "BlockA", IsXclipped = false },
                    new BlockInfo { Id = "3", Name = "BlockC", IsXclipped = false },
                }.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var callbackCount = 0;
            var options = new BlockCleanupOptions
            {
                OnBlockExploded = _ => callbackCount++,
            };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, callbackCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_BlockExplodeReport_HasCorrectSequenceInfo()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "1", Name = "BlockA", IsXclipped = false },
                    new BlockInfo { Id = "3", Name = "BlockC", IsXclipped = false },
                }.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("1")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.IsBlockXclipped("3")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var reports = new List<BlockExplodeReport>();
            var options = new BlockCleanupOptions { OnBlockExploded = report => reports.Add(report) };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, reports.Count);

            var blockAReport = reports.FirstOrDefault(r => r.BlockName == "BlockA");
            Assert.IsNotNull(blockAReport);
            Assert.AreEqual(1, blockAReport.Index);
            Assert.AreEqual(2, blockAReport.TotalCount);
            Assert.AreEqual(1, blockAReport.RoundNumber);

            var blockCReport = reports.FirstOrDefault(r => r.BlockName == "BlockC");
            Assert.IsNotNull(blockCReport);
            Assert.AreEqual(2, blockCReport.Index);
            Assert.AreEqual(2, blockCReport.TotalCount);
            Assert.AreEqual(1, blockCReport.RoundNumber);
        }

        [Test]
        public void CleanupNonXclippedBlocks_MultipleRounds_ReportsHaveCorrectRoundNumbers()
        {
            _repoMock.SetupSequence(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "10", Name = "Outer1", IsXclipped = false },
                }.AsReadOnly()))
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "11", Name = "Inner1", IsXclipped = false },
                }.AsReadOnly()))
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "12", Name = "Inner2", IsXclipped = false },
                }.AsReadOnly()))
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>().AsReadOnly()));

            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var reports = new List<BlockExplodeReport>();
            var options = new BlockCleanupOptions { OnBlockExploded = report => reports.Add(report) };

            var result = _service.CleanupNonXclippedBlocks(options);

            Assert.IsTrue(result.IsSuccess);
            Assert.GreaterOrEqual(reports.Count, 2);

            var firstRoundReports = reports.Where(r => r.RoundNumber == 1).ToList();
            Assert.GreaterOrEqual(firstRoundReports.Count, 1);

            var maxRound = reports.Max(r => r.RoundNumber);
            Assert.GreaterOrEqual(maxRound, 1);

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
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "20", Name = "Block1", IsXclipped = false },
                    new BlockInfo { Id = "21", Name = "Block2", IsXclipped = false },
                    new BlockInfo { Id = "22", Name = "Block3", IsXclipped = false },
                }.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
            _repoMock.Verify(r => r.ExplodeBlock("20"), Times.Once);
            _repoMock.Verify(r => r.ExplodeBlock("21"), Times.Once);
            _repoMock.Verify(r => r.ExplodeBlock("22"), Times.Once);
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_EarlyTerminationAfterEmptyRounds()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "30", Name = "NoFollowUpBlock", IsXclipped = false },
                }.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped("30")).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock("30"))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.LessOrEqual(result.Data.IterationCount, 3);
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_SameNameBlocksBatched()
        {
            _repoMock.Setup(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "40", Name = "SameName", IsXclipped = false },
                    new BlockInfo { Id = "41", Name = "SameName", IsXclipped = false },
                    new BlockInfo { Id = "42", Name = "SameName", IsXclipped = false },
                }.AsReadOnly()));
            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Data.TotalExplodedEntityCount);
            Assert.AreEqual(1, result.Data.IterationCount);
            Assert.AreEqual(1, result.Data.Rounds[0].ExplodeReports.Count);
            Assert.AreEqual(3, result.Data.Rounds[0].ExplodeReports[0].AggregatedCount);
        }

        [Test]
        public void CleanupNonXclippedBlocks_Optimization_NameBasedDeduplication()
        {
            _repoMock.SetupSequence(r => r.GetAllBlocksInCurrentSpace())
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>
                {
                    new BlockInfo { Id = "50", Name = "SharedBlock", IsXclipped = false },
                }.AsReadOnly()))
                .Returns(OpResult<IReadOnlyList<BlockInfo>>.Success(new List<BlockInfo>().AsReadOnly()));

            _repoMock.Setup(r => r.IsBlockXclipped(It.IsAny<string>())).Returns(OpResult<bool>.Success(false));
            _repoMock.Setup(r => r.ExplodeBlock(It.IsAny<string>()))
                .Returns(OpResult<BlockExplodeResult>.Success(new BlockExplodeResult { EntityCount = 1 }));

            var result = _service.CleanupNonXclippedBlocks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Data.TotalExplodedEntityCount);
            _repoMock.Verify(r => r.ExplodeBlock("50"), Times.Once);
        }
    }
}
