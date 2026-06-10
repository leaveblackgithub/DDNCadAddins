using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.UnitTests
{
    /// <summary>
    ///     OpResult 和 OpResult&lt;T&gt; 的纯单元测试，不依赖 AutoCAD 运行环境
    /// </summary>
    [TestFixture]
    public class OpResultTests
    {
        // ────────────────────────────────────────────────────────────────
        // OpResult<T> — Success
        // ────────────────────────────────────────────────────────────────

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
        public void GenericSuccess_Message_IsEmpty()
        {
            var result = OpResult<int>.Success(0);

            Assert.AreEqual(string.Empty, result.Message);
        }

        [Test]
        public void GenericSuccess_WithNull_DataIsNull()
        {
            var result = OpResult<string>.Success(null);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Data);
        }

        // ────────────────────────────────────────────────────────────────
        // OpResult<T> — Fail
        // ────────────────────────────────────────────────────────────────

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
        public void GenericFail_Data_IsDefaultForValueType()
        {
            var result = OpResult<int>.Fail("失败");

            Assert.AreEqual(default(int), result.Data);
        }

        [Test]
        public void GenericFail_Data_IsNullForReferenceType()
        {
            var result = OpResult<string>.Fail("失败");

            Assert.IsNull(result.Data);
        }

        // ────────────────────────────────────────────────────────────────
        // OpResult<T> — 构造函数直接赋值
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void GenericConstructor_SetsAllProperties()
        {
            var result = new OpResult<double>(true, "ok", 3.14);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("ok", result.Message);
            Assert.AreEqual(3.14, result.Data, 1e-10);
        }

        [Test]
        public void GenericDefaultConstructor_AllPropertiesAreDefault()
        {
            var result = new OpResult<int>();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Message);
            Assert.AreEqual(0, result.Data);
        }

        // ────────────────────────────────────────────────────────────────
        // OpResult (非泛型) — Success
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void NonGenericSuccess_IsSuccess_IsTrue()
        {
            var result = OpResult.Success();

            Assert.IsTrue(result.IsSuccess);
        }

        [Test]
        public void NonGenericSuccess_Message_IsEmpty()
        {
            var result = OpResult.Success();

            Assert.AreEqual(string.Empty, result.Message);
        }

        // ────────────────────────────────────────────────────────────────
        // OpResult (非泛型) — Fail
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void NonGenericFail_IsSuccess_IsFalse()
        {
            var result = OpResult.Fail("错误");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void NonGenericFail_Message_EqualsProvidedMessage()
        {
            const string msg = "发生了一个错误";
            var result = OpResult.Fail(msg);

            Assert.AreEqual(msg, result.Message);
        }

        [Test]
        public void NonGenericConstructor_SetsAllProperties()
        {
            var result = new OpResult(false, "失败消息");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("失败消息", result.Message);
        }

        [Test]
        public void NonGenericDefaultConstructor_AllPropertiesAreDefault()
        {
            var result = new OpResult();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Message);
        }

        // ────────────────────────────────────────────────────────────────
        // 属性可写性（支持直接赋值场景）
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void GenericResult_PropertiesAreMutable()
        {
            var result = OpResult<string>.Success("原始");
            result.IsSuccess = false;
            result.Message = "已修改";
            result.Data = "新值";

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("已修改", result.Message);
            Assert.AreEqual("新值", result.Data);
        }

        [Test]
        public void NonGenericResult_PropertiesAreMutable()
        {
            var result = OpResult.Success();
            result.IsSuccess = false;
            result.Message = "变更";

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("变更", result.Message);
        }
    }
}
