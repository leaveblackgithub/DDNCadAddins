using DDNCadAddins.Core.Models;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     BlockExplodeResult 的纯单元测试.
    ///     覆盖 Add 累加、Aggregate 聚合、边界情况.
    /// </summary>
    [TestFixture]
    public class BlockExplodeResultTests
    {
        [Test]
        public void DefaultConstructor_AllCountsAreZero()
        {
            var r = new BlockExplodeResult();
            Assert.AreEqual(0, r.EntityCount);
            Assert.AreEqual(0, r.AttributeTextCount);
            Assert.AreEqual(0, r.LayerAdjustedCount);
            Assert.AreEqual(0, r.ColorAdjustedCount);
        }

        [Test]
        public void Add_WithNull_DoesNotThrow()
        {
            var r = new BlockExplodeResult { EntityCount = 5 };
            r.Add(null);
            Assert.AreEqual(5, r.EntityCount);
        }

        [Test]
        public void Add_AccumulatesAllFields()
        {
            var a = new BlockExplodeResult
            {
                EntityCount = 10,
                AttributeTextCount = 2,
                LayerAdjustedCount = 8,
                ColorAdjustedCount = 3
            };
            var b = new BlockExplodeResult
            {
                EntityCount = 5,
                AttributeTextCount = 1,
                LayerAdjustedCount = 2,
                ColorAdjustedCount = 7
            };

            a.Add(b);

            Assert.AreEqual(15, a.EntityCount);
            Assert.AreEqual(3, a.AttributeTextCount);
            Assert.AreEqual(10, a.LayerAdjustedCount);
            Assert.AreEqual(10, a.ColorAdjustedCount);
        }

        [Test]
        public void Add_ToEmpty_EqualsOther()
        {
            var a = new BlockExplodeResult();
            var b = new BlockExplodeResult
            {
                EntityCount = 42,
                AttributeTextCount = 7,
                LayerAdjustedCount = 5,
                ColorAdjustedCount = 9
            };

            a.Add(b);

            Assert.AreEqual(42, a.EntityCount);
            Assert.AreEqual(7, a.AttributeTextCount);
            Assert.AreEqual(5, a.LayerAdjustedCount);
            Assert.AreEqual(9, a.ColorAdjustedCount);
        }

        [Test]
        public void Aggregate_SingleResult_ReturnsSame()
        {
            var r = new BlockExplodeResult
            {
                EntityCount = 100,
                AttributeTextCount = 20,
                LayerAdjustedCount = 50,
                ColorAdjustedCount = 30
            };

            var agg = BlockExplodeResult.Aggregate(r);

            Assert.AreEqual(100, agg.EntityCount);
            Assert.AreEqual(20, agg.AttributeTextCount);
            Assert.AreEqual(50, agg.LayerAdjustedCount);
            Assert.AreEqual(30, agg.ColorAdjustedCount);
        }

        [Test]
        public void Aggregate_EmptyArray_ReturnsZeroResult()
        {
            var agg = BlockExplodeResult.Aggregate();

            Assert.AreEqual(0, agg.EntityCount);
            Assert.AreEqual(0, agg.AttributeTextCount);
            Assert.AreEqual(0, agg.LayerAdjustedCount);
            Assert.AreEqual(0, agg.ColorAdjustedCount);
        }

        [Test]
        public void Aggregate_MultipleResults_SumsAll()
        {
            var r1 = new BlockExplodeResult { EntityCount = 1, AttributeTextCount = 1 };
            var r2 = new BlockExplodeResult { EntityCount = 2, LayerAdjustedCount = 3 };
            var r3 = new BlockExplodeResult { ColorAdjustedCount = 5 };

            var agg = BlockExplodeResult.Aggregate(r1, r2, r3);

            Assert.AreEqual(3, agg.EntityCount);
            Assert.AreEqual(1, agg.AttributeTextCount);
            Assert.AreEqual(3, agg.LayerAdjustedCount);
            Assert.AreEqual(5, agg.ColorAdjustedCount);
        }

        [Test]
        public void Aggregate_WithNull_HandlesGracefully()
        {
            var r1 = new BlockExplodeResult { EntityCount = 10 };

            // null 被 Add 跳过，不抛异常
            var agg = BlockExplodeResult.Aggregate(r1, null);

            Assert.AreEqual(10, agg.EntityCount);
        }
    }
}
