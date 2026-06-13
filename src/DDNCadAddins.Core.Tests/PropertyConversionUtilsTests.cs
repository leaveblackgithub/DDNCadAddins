using System;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class PropertyConversionUtilsTests
    {
        [Test]
        public void CanBeConvertedFrom_SameType_ReturnsTrue()
        {
            Assert.IsTrue(PropertyConversionUtils.CanBeConvertedFrom(typeof(int), typeof(int)));
        }

        [Test]
        public void CanBeConvertedFrom_ByteToInt_ReturnsTrue()
        {
            Assert.IsTrue(PropertyConversionUtils.CanBeConvertedFrom(typeof(int), typeof(byte)));
        }

        [Test]
        public void CanBeConvertedFrom_DoubleToInt_ReturnsFalse()
        {
            Assert.IsFalse(PropertyConversionUtils.CanBeConvertedFrom(typeof(int), typeof(double)));
        }

        [Test]
        public void CanBeConvertedFrom_StringToEnum_ReturnsTrue()
        {
            Assert.IsTrue(PropertyConversionUtils.CanBeConvertedFrom(typeof(DayOfWeek), typeof(string)));
        }

        [Test]
        public void CanBeConvertedFrom_NullTargetType_ReturnsFalse()
        {
            Assert.IsFalse(PropertyConversionUtils.CanBeConvertedFrom(null, typeof(int)));
        }
    }
}
