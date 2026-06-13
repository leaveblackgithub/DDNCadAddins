using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class PropertyComparisonUtilsTests
    {
        [Test]
        public void ValueEquals_SameString_ReturnsTrue()
        {
            Assert.IsTrue(PropertyComparisonUtils.ValueEquals("Layer0", "layer0"));
        }

        [Test]
        public void ValueEquals_DifferentValues_ReturnsFalse()
        {
            Assert.IsFalse(PropertyComparisonUtils.ValueEquals("0", "1"));
        }

        [Test]
        public void ValueEquals_BothNull_ReturnsTrue()
        {
            Assert.IsTrue(PropertyComparisonUtils.ValueEquals(null, null));
        }

        [Test]
        public void ValueEquals_IntAndShortSameValue_ReturnsTrue()
        {
            Assert.IsTrue(PropertyComparisonUtils.ValueEquals((short)7, 7));
        }
    }
}
