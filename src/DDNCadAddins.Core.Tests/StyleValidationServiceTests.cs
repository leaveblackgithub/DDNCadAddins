using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class StyleValidationServiceTests
    {
        private StyleValidationService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new StyleValidationService();
        }

        [Test]
        public void GetValidColorIndex_ValidValue_ReturnsSame()
        {
            Assert.AreEqual((short)7, _service.GetValidColorIndex(7));
            Assert.AreEqual((short)0, _service.GetValidColorIndex(0));
            Assert.AreEqual((short)255, _service.GetValidColorIndex(255));
        }

        [Test]
        public void GetValidColorIndex_OutOfRange_ReturnsDefault()
        {
            Assert.AreEqual(CadStyleConstants.Colors.White, _service.GetValidColorIndex(-1));
            Assert.AreEqual(CadStyleConstants.Colors.White, _service.GetValidColorIndex(256));
        }

        [Test]
        public void GetValidColorIndex_OutOfRangeWithCustomDefault_ReturnsCustomDefault()
        {
            Assert.AreEqual(CadStyleConstants.Colors.Red, _service.GetValidColorIndex(-1, CadStyleConstants.Colors.Red));
        }

        [Test]
        public void GetValidColorIndex_InvalidDefault_FallsBackToWhite()
        {
            Assert.AreEqual(CadStyleConstants.Colors.White, _service.GetValidColorIndex(-1, 300));
        }

        [Test]
        public void IsValidAciColorIndex_BoundaryValues()
        {
            Assert.IsTrue(_service.IsValidAciColorIndex(0));
            Assert.IsTrue(_service.IsValidAciColorIndex(255));
            Assert.IsFalse(_service.IsValidAciColorIndex(-1));
            Assert.IsFalse(_service.IsValidAciColorIndex(256));
        }

        [Test]
        public void NormalizeLineTypeName_Empty_ReturnsContinuous()
        {
            Assert.AreEqual(CadStyleConstants.Linetypes.Continuous, _service.NormalizeLineTypeName(null));
            Assert.AreEqual(CadStyleConstants.Linetypes.Continuous, _service.NormalizeLineTypeName(string.Empty));
            Assert.AreEqual(CadStyleConstants.Linetypes.Continuous, _service.NormalizeLineTypeName("   "));
        }

        [Test]
        public void NormalizeLineTypeName_ValidName_TrimsWhitespace()
        {
            Assert.AreEqual("DASHED", _service.NormalizeLineTypeName("  DASHED  "));
            Assert.AreEqual(CadStyleConstants.Linetypes.ByBlock, _service.NormalizeLineTypeName("BYBLOCK"));
        }
    }
}
