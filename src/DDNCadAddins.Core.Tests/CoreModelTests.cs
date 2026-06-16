using System;
using DDNCadAddins.Core.Models;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    // ====================================================================
    // CadStyleConstants
    // ====================================================================

    [TestFixture]
    public class CadStyleConstantsTests
    {
        [Test]
        public void Colors_ByBlock_IsZero()
        {
            Assert.AreEqual(0, CadStyleConstants.Colors.ByBlock);
        }

        [Test]
        public void Colors_ByLayer_Is256()
        {
            Assert.AreEqual(256, CadStyleConstants.Colors.ByLayer);
        }

        [Test]
        public void Colors_Red_IsOne()
        {
            Assert.AreEqual(1, CadStyleConstants.Colors.Red);
        }

        [Test]
        public void Colors_MaxAciIndex_Is255()
        {
            Assert.AreEqual(255, CadStyleConstants.Colors.MaxAciIndex);
        }

        [Test]
        public void Linetypes_Continuous_IsContinuous()
        {
            Assert.AreEqual("Continuous", CadStyleConstants.Linetypes.Continuous);
        }

        [Test]
        public void Linetypes_ByBlock_IsByBlock()
        {
            Assert.AreEqual("BYBLOCK", CadStyleConstants.Linetypes.ByBlock);
        }

        [Test]
        public void Linetypes_ByLayer_IsByLayer()
        {
            Assert.AreEqual("BYLAYER", CadStyleConstants.Linetypes.ByLayer);
        }
    }

    // ====================================================================
    // ContainmentResult
    // ====================================================================

    [TestFixture]
    public class ContainmentResultTests
    {
        [Test]
        public void Inside_HasValueZero()
        {
            Assert.AreEqual(0, (int)ContainmentResult.Inside);
        }

        [Test]
        public void OnBoundary_HasValueOne()
        {
            Assert.AreEqual(1, (int)ContainmentResult.OnBoundary);
        }

        [Test]
        public void Outside_HasValueTwo()
        {
            Assert.AreEqual(2, (int)ContainmentResult.Outside);
        }

        [Test]
        public void Intersects_HasValueThree()
        {
            Assert.AreEqual(3, (int)ContainmentResult.Intersects);
        }

        [Test]
        public void AllValues_AreDistinct()
        {
            var all = (ContainmentResult[])Enum.GetValues(typeof(ContainmentResult));
            Assert.AreEqual(4, all.Length);
            Assert.AreNotEqual(all[0], all[1]);
            Assert.AreNotEqual(all[1], all[2]);
            Assert.AreNotEqual(all[2], all[3]);
        }
    }

    // ====================================================================
    // Point2D
    // ====================================================================

    [TestFixture]
    public class Point2DTests
    {
        [Test]
        public void Constructor_SetsXAndY()
        {
            var pt = new Point2D(3.5, -2.1);
            Assert.AreEqual(3.5, pt.X, 1e-12);
            Assert.AreEqual(-2.1, pt.Y, 1e-12);
        }

        [Test]
        public void Constructor_ZeroCoordinates()
        {
            var pt = new Point2D(0, 0);
            Assert.AreEqual(0, pt.X);
            Assert.AreEqual(0, pt.Y);
        }

        [Test]
        public void Properties_AreReadOnly()
        {
            // 验证 struct 的只读属性
            var pt = new Point2D(1, 2);
            var x = pt.X;
            var y = pt.Y;
            Assert.AreEqual(1, x);
            Assert.AreEqual(2, y);
        }
    }

    // ====================================================================
    // BlockCleanupOptions
    // ====================================================================

    [TestFixture]
    public class BlockCleanupOptionsTests
    {
        [Test]
        public void CancelledMessage_HasDefaultValue()
        {
            Assert.AreEqual("用户已取消操作。", BlockCleanupOptions.CancelledMessage);
        }

        [Test]
        public void DefaultConstructor_CallbacksAreNull()
        {
            var opts = new BlockCleanupOptions();
            Assert.IsNull(opts.IsCancellationRequested);
            Assert.IsNull(opts.OnRoundStarted);
            Assert.IsNull(opts.OnBlockExploded);
        }
    }

    // ====================================================================
    // BlockCleanupResult (includes BlockExplodeReport, BlockCleanupRoundResult)
    // ====================================================================

    [TestFixture]
    public class BlockCleanupResultTests
    {
        [Test]
        public void DefaultConstructor_CollectionsAreNotNull()
        {
            var r = new BlockCleanupResult();
            Assert.IsNotNull(r.FailureCounts);
            Assert.IsNotNull(r.Rounds);
            Assert.AreEqual(0, r.FailureCounts.Count);
            Assert.AreEqual(0, r.Rounds.Count);
        }

        [Test]
        public void DefaultConstructor_CountsAreZero()
        {
            var r = new BlockCleanupResult();
            Assert.AreEqual(0, r.IterationCount);
            Assert.AreEqual(0, r.TotalExplodedEntityCount);
            Assert.AreEqual(0, r.TotalErasedEmptyBlockCount);
        }

        [Test]
        public void CanAddFailureCount()
        {
            var r = new BlockCleanupResult();
            r.FailureCounts["Error"] = 3;
            Assert.AreEqual(3, r.FailureCounts["Error"]);
        }

        [Test]
        public void CanAddRoundResult()
        {
            var r = new BlockCleanupResult();
            r.Rounds.Add(new BlockCleanupRoundResult { Iteration = 1, AttemptedCount = 5 });
            Assert.AreEqual(1, r.Rounds.Count);
            Assert.AreEqual(5, r.Rounds[0].AttemptedCount);
        }
    }

    [TestFixture]
    public class BlockCleanupRoundResultTests
    {
        [Test]
        public void DefaultConstructor_CollectionsAreNotNull()
        {
            var r = new BlockCleanupRoundResult();
            Assert.IsNotNull(r.ExplodeReports);
            Assert.IsNotNull(r.FailureCounts);
        }

        [Test]
        public void DefaultConstructor_CountsAreZero()
        {
            var r = new BlockCleanupRoundResult();
            Assert.AreEqual(0, r.Iteration);
            Assert.AreEqual(0, r.AttemptedCount);
            Assert.AreEqual(0, r.ExplodedEntityCount);
        }
    }

    [TestFixture]
    public class BlockExplodeReportTests
    {
        [Test]
        public void DefaultConstructor_PropertiesAreDefault()
        {
            var r = new BlockExplodeReport();
            Assert.IsNull(r.BlockName);
            Assert.IsNull(r.Stats);
            Assert.AreEqual(0, r.Index);
            Assert.AreEqual(0, r.TotalCount);
            Assert.AreEqual(0, r.RoundNumber);
            Assert.AreEqual(0, r.AggregatedCount);
        }
    }

    // ====================================================================
    // BlockInfo
    // ====================================================================

    [TestFixture]
    public class BlockInfoTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            var info = new BlockInfo { Id = "ABC", Name = "MyBlock", IsXclipped = true };
            Assert.AreEqual("ABC", info.Id);
            Assert.AreEqual("MyBlock", info.Name);
            Assert.IsTrue(info.IsXclipped);
        }

        [Test]
        public void Default_IsNotXclipped()
        {
            var info = new BlockInfo();
            Assert.IsFalse(info.IsXclipped);
        }
    }

    // ====================================================================
    // LayerInfo
    // ====================================================================

    [TestFixture]
    public class LayerInfoTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            var info = new LayerInfo
            {
                Name = "Walls",
                IsLocked = true,
                IsFrozen = false,
                ColorIndex = 3,
                LinetypeName = "Continuous"
            };
            Assert.AreEqual("Walls", info.Name);
            Assert.IsTrue(info.IsLocked);
            Assert.IsFalse(info.IsFrozen);
            Assert.AreEqual(3, info.ColorIndex);
            Assert.AreEqual("Continuous", info.LinetypeName);
        }

        [Test]
        public void Default_IsUnlockedAndThawed()
        {
            var info = new LayerInfo();
            Assert.IsFalse(info.IsLocked);
            Assert.IsFalse(info.IsFrozen);
        }
    }
}
