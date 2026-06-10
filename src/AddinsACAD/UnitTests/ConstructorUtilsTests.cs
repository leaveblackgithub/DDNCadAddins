using System;
using System.Collections.Generic;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.UnitTests
{
    /// <summary>
    ///     ConstructorUtils 的纯单元测试，不依赖 AutoCAD 运行环境
    /// </summary>
    [TestFixture]
    public class ConstructorUtilsTests
    {
        // ────────────────────────────────────────────────────────────────
        // 测试辅助类
        // ────────────────────────────────────────────────────────────────

        private class NoArgClass
        {
            public string Value { get; } = "default";
        }

        private class SingleStringClass
        {
            public SingleStringClass(string name) { Name = name; }
            public string Name { get; }
        }

        private class MultiArgClass
        {
            public MultiArgClass(string name, int count, double ratio)
            {
                Name = name;
                Count = count;
                Ratio = ratio;
            }
            public string Name { get; }
            public int Count { get; }
            public double Ratio { get; }
        }

        private class InheritedArgClass : SingleStringClass
        {
            public InheritedArgClass(string name) : base(name) { }
        }

        // ────────────────────────────────────────────────────────────────
        // 成功创建实例
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void CreateWithParameters_NoArgs_ReturnsInstance()
        {
            var result = ConstructorUtils.CreateWithParameters(typeof(NoArgClass), new List<object>());

            Assert.IsNotNull(result);
            Assert.IsInstanceOf<NoArgClass>(result);
        }

        [Test]
        public void CreateWithParameters_SingleStringArg_SetsProperty()
        {
            var result = ConstructorUtils.CreateWithParameters(
                typeof(SingleStringClass), new List<object> { "TestName" });

            Assert.IsNotNull(result);
            var typed = result as SingleStringClass;
            Assert.IsNotNull(typed);
            Assert.AreEqual("TestName", typed.Name);
        }

        [Test]
        public void CreateWithParameters_MultipleArgs_SetsAllProperties()
        {
            var result = ConstructorUtils.CreateWithParameters(
                typeof(MultiArgClass), new List<object> { "Hello", 5, 3.14 });

            Assert.IsNotNull(result);
            var typed = result as MultiArgClass;
            Assert.IsNotNull(typed);
            Assert.AreEqual("Hello", typed.Name);
            Assert.AreEqual(5, typed.Count);
            Assert.AreEqual(3.14, typed.Ratio, 1e-10);
        }

        [Test]
        public void CreateWithParameters_InheritedClass_ReturnsCorrectType()
        {
            var result = ConstructorUtils.CreateWithParameters(
                typeof(InheritedArgClass), new List<object> { "Derived" });

            Assert.IsNotNull(result);
            Assert.IsInstanceOf<InheritedArgClass>(result);
            Assert.IsInstanceOf<SingleStringClass>(result);
        }

        // ────────────────────────────────────────────────────────────────
        // 参数不匹配 → 返回 null
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void CreateWithParameters_TooManyArgs_ReturnsNull()
        {
            var result = ConstructorUtils.CreateWithParameters(
                typeof(NoArgClass), new List<object> { "extra" });

            Assert.IsNull(result);
        }

        [Test]
        public void CreateWithParameters_TooFewArgs_ReturnsNull()
        {
            var result = ConstructorUtils.CreateWithParameters(
                typeof(MultiArgClass), new List<object> { "only one" });

            Assert.IsNull(result);
        }

        [Test]
        public void CreateWithParameters_IncompatibleArgType_ReturnsNull()
        {
            // SingleStringClass 要求 string，传入 int
            var result = ConstructorUtils.CreateWithParameters(
                typeof(SingleStringClass), new List<object> { 42 });

            Assert.IsNull(result);
        }

        // ────────────────────────────────────────────────────────────────
        // 边界情况
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void CreateWithParameters_NullInList_WhenArgIsReferenceType_ReturnsInstance()
        {
            // string 是引用类型，null 可被接受
            var result = ConstructorUtils.CreateWithParameters(
                typeof(SingleStringClass), new List<object> { null });

            // null 值传入 string 参数应当成功创建实例
            Assert.IsNotNull(result);
        }

        [Test]
        public void CreateWithParameters_EmptyListForDefaultConstructor_Succeeds()
        {
            var result = ConstructorUtils.CreateWithParameters(
                typeof(NoArgClass), new List<object>());

            Assert.IsNotNull(result);
        }
    }
}
