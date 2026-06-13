using DDNCadAddins.Core.Models;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     OpResult 和 OpResult&lt;T&gt; 的纯单元测试
    /// </summary>
    [TestFixture]
    public class OpResultTests
    {
        [Test]
        public void GenericSuccess_IsSuccess_IsTrue()
        {
            var result = OpResult<int>.Success(42);
            Assert.IsTrue(result.IsSuccess);
        }

        [Test]
        public void GenericSuccess_Data_EqualsProvidedValue()
        {
            var result = OpResult<string>.Success("hello");
            Assert.AreEqual("hello", result.Data);
        }

        [Test]
        public void GenericFail_IsSuccess_IsFalse()
        {
            var result = OpResult<int>.Fail("出错了");
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void GenericFail_Message_EqualsProvidedMessage()
        {
            const string msg = "操作失败：参数无效";
            var result = OpResult<int>.Fail(msg);
            Assert.AreEqual(msg, result.Message);
        }

        [Test]
        public void NonGenericSuccess_IsSuccess_IsTrue()
        {
            var result = OpResult.Success();
            Assert.IsTrue(result.IsSuccess);
        }

        [Test]
        public void NonGenericFail_Message_EqualsProvidedMessage()
        {
            const string msg = "发生了一个错误";
            var result = OpResult.Fail(msg);
            Assert.AreEqual(msg, result.Message);
        }
    }
}
