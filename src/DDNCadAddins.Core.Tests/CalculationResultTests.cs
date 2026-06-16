using DDNCadAddins.Core.Models;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     CalculationResult 工厂方法的纯单元测试.
    /// </summary>
    [TestFixture]
    public class CalculationResultTests
    {
        [Test]
        public void Success_IsSuccess_IsTrue()
        {
            var r = CalculationResult.Success(42);
            Assert.IsTrue(r.IsSuccess);
        }

        [Test]
        public void Success_Value_EqualsProvided()
        {
            var r = CalculationResult.Success(3.14);
            Assert.AreEqual(3.14, r.Value, 1e-12);
        }

        [Test]
        public void Success_DefaultMessage_IsEmpty()
        {
            var r = CalculationResult.Success(1);
            Assert.AreEqual("", r.Message);
        }

        [Test]
        public void Success_CustomMessage_Preserved()
        {
            var r = CalculationResult.Success(0, "计算完成");
            Assert.AreEqual("计算完成", r.Message);
        }

        [Test]
        public void Success_NegativeValue_Preserved()
        {
            var r = CalculationResult.Success(-99.5);
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(-99.5, r.Value, 1e-12);
        }

        [Test]
        public void Fail_IsSuccess_IsFalse()
        {
            var r = CalculationResult.Fail("出错了");
            Assert.IsFalse(r.IsSuccess);
        }

        [Test]
        public void Fail_Message_Preserved()
        {
            var r = CalculationResult.Fail("参数无效");
            Assert.AreEqual("参数无效", r.Message);
        }

        [Test]
        public void Fail_Value_IsZero()
        {
            var r = CalculationResult.Fail("失败");
            Assert.AreEqual(0, r.Value, 1e-12);
        }

        [Test]
        public void Fail_EmptyMessage_Allowed()
        {
            var r = CalculationResult.Fail("");
            Assert.IsFalse(r.IsSuccess);
            Assert.AreEqual("", r.Message);
        }
    }
}
