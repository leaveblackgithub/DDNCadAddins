using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class CalculatorServiceTests
    {
        private ICalculatorService _calculator;

        [SetUp]
        public void SetUp()
        {
            _calculator = new CalculatorService();
        }

        // --- Add 正常路径 ---

        [Test]
        public void Add_PositiveNumbers_ReturnsSum()
        {
            var result = _calculator.Add(2.0, 3.0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(5.0, result.Value, 1e-10);
        }

        [Test]
        public void Add_NegativeNumbers_ReturnsSum()
        {
            var result = _calculator.Add(-4.0, -6.0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(-10.0, result.Value, 1e-10);
        }

        [Test]
        public void Add_MixedSign_ReturnsSum()
        {
            var result = _calculator.Add(10.0, -3.0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(7.0, result.Value, 1e-10);
        }

        [Test]
        public void Add_Zeros_ReturnsZero()
        {
            var result = _calculator.Add(0.0, 0.0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0.0, result.Value, 1e-10);
        }

        // --- Add 边界情况 ---

        [Test]
        public void Add_NaNFirstArg_ReturnsFail()
        {
            var result = _calculator.Add(double.NaN, 3.0);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotEmpty(result.Message);
        }

        [Test]
        public void Add_NaNSecondArg_ReturnsFail()
        {
            var result = _calculator.Add(2.0, double.NaN);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void Add_PositiveInfinity_ReturnsFail()
        {
            var result = _calculator.Add(double.PositiveInfinity, 1.0);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void Add_MaxValueOverflow_ReturnsFail()
        {
            var result = _calculator.Add(double.MaxValue, double.MaxValue);

            Assert.IsFalse(result.IsSuccess);
        }

        // --- Subtract 正常路径 ---

        [Test]
        public void Subtract_PositiveNumbers_ReturnsDifference()
        {
            var result = _calculator.Subtract(10.0, 3.0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(7.0, result.Value, 1e-10);
        }

        [Test]
        public void Subtract_NaN_ReturnsFail()
        {
            var result = _calculator.Subtract(double.NaN, 3.0);

            Assert.IsFalse(result.IsSuccess);
        }

        // --- 验证 CalculationResult 工厂方法 ---

        [Test]
        public void CalculationResult_Success_SetsPropertiesCorrectly()
        {
            var result = CalculationResult.Success(42.0, "测试成功");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(42.0, result.Value, 1e-10);
            Assert.AreEqual("测试成功", result.Message);
        }

        [Test]
        public void CalculationResult_Fail_SetsPropertiesCorrectly()
        {
            var result = CalculationResult.Fail("错误信息");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(0.0, result.Value, 1e-10);
            Assert.AreEqual("错误信息", result.Message);
        }
    }
}
