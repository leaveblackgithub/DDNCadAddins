using System;
using NUnit.Framework;
using ServiceACAD;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     PropertyUtils 的纯单元测试，不依赖 AutoCAD 运行环境
    /// </summary>
    [TestFixture]
    public class PropertyUtilsTests
    {
        // ────────────────────────────────────────────────────────────────
        // 测试辅助类
        // ────────────────────────────────────────────────────────────────

        private class SampleEntity
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public double Score { get; set; }
            public bool Active { get; set; }
            public string ReadOnlyProp => "readonly";
        }

        private class DerivedEntity : SampleEntity
        {
            public string Extra { get; set; }
        }

        private interface ISampleInterface
        {
            void DoSomething();
        }

        private class ImplementsSample : ISampleInterface
        {
            public void DoSomething() { }
        }

        // ────────────────────────────────────────────────────────────────
        // HasProperty
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void HasProperty_ExistingProperty_ReturnsTrue()
        {
            var obj = new SampleEntity { Name = "Test" };
            Assert.IsTrue(PropertyUtils.HasProperty(obj, "Name"));
        }

        [Test]
        public void HasProperty_NonExistentProperty_ReturnsFalse()
        {
            var obj = new SampleEntity();
            Assert.IsFalse(PropertyUtils.HasProperty(obj, "NonExistentProp"));
        }

        [Test]
        public void HasProperty_NullObject_ReturnsFalse()
        {
            Assert.IsFalse(PropertyUtils.HasProperty(null, "Name"));
        }

        [Test]
        public void HasProperty_NullPropertyName_ReturnsFalse()
        {
            var obj = new SampleEntity();
            Assert.IsFalse(PropertyUtils.HasProperty(obj, null));
        }

        [Test]
        public void HasProperty_EmptyPropertyName_ReturnsFalse()
        {
            var obj = new SampleEntity();
            Assert.IsFalse(PropertyUtils.HasProperty(obj, string.Empty));
        }

        [Test]
        public void HasProperty_NullPropertyValue_ReturnsFalse()
        {
            var obj = new SampleEntity { Name = null };
            Assert.IsFalse(PropertyUtils.HasProperty(obj, "Name"));
        }

        // ────────────────────────────────────────────────────────────────
        // GetPropertyValue
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void GetPropertyValue_StringProperty_ReturnsCorrectValue()
        {
            var obj = new SampleEntity { Name = "Hello" };
            var result = PropertyUtils.GetPropertyValue(obj, "Name");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Hello", result.Data);
        }

        [Test]
        public void GetPropertyValue_IntProperty_ReturnsCorrectValue()
        {
            var obj = new SampleEntity { Age = 25 };
            var result = PropertyUtils.GetPropertyValue(obj, "Age");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(25, result.Data);
        }

        [Test]
        public void GetPropertyValue_NullObject_ReturnsFail()
        {
            var result = PropertyUtils.GetPropertyValue(null, "Name");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotEmpty(result.Message);
        }

        [Test]
        public void GetPropertyValue_EmptyPropertyName_ReturnsFail()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.GetPropertyValue(obj, string.Empty);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void GetPropertyValue_NonExistentProperty_ReturnsFail()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.GetPropertyValue(obj, "GhostProp");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotEmpty(result.Message);
        }

        [Test]
        public void GetPropertyValue_NullPropertyValue_ReturnsSuccessWithNull()
        {
            var obj = new SampleEntity { Name = null };
            var result = PropertyUtils.GetPropertyValue(obj, "Name");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Data);
        }

        // ────────────────────────────────────────────────────────────────
        // SetPropertyValue
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void SetPropertyValue_StringProperty_SetsCorrectly()
        {
            var obj = new SampleEntity { Name = "Old" };
            var result = PropertyUtils.SetPropertyValue(obj, "Name", "New");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("New", obj.Name);
        }

        [Test]
        public void SetPropertyValue_IntProperty_SetsCorrectly()
        {
            var obj = new SampleEntity { Age = 10 };
            var result = PropertyUtils.SetPropertyValue(obj, "Age", 20);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(20, obj.Age);
        }

        [Test]
        public void SetPropertyValue_NullObject_ReturnsFail()
        {
            var result = PropertyUtils.SetPropertyValue(null, "Name", "value");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void SetPropertyValue_NonExistentProperty_ReturnsFail()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.SetPropertyValue(obj, "GhostProp", "value");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void SetPropertyValue_ReadOnlyProperty_ReturnsFail()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.SetPropertyValue(obj, "ReadOnlyProp", "value");

            Assert.IsFalse(result.IsSuccess);
        }

        // ────────────────────────────────────────────────────────────────
        // CanBeConvertedFrom
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void CanBeConvertedFrom_SameType_ReturnsTrue()
        {
            Assert.IsTrue(PropertyUtils.CanBeConvertedFrom(typeof(int), typeof(int)));
        }

        [Test]
        public void CanBeConvertedFrom_ByteToInt_ReturnsTrue()
        {
            Assert.IsTrue(PropertyUtils.CanBeConvertedFrom(typeof(int), typeof(byte)));
        }

        [Test]
        public void CanBeConvertedFrom_IntToByte_ReturnsFalse()
        {
            Assert.IsFalse(PropertyUtils.CanBeConvertedFrom(typeof(byte), typeof(int)));
        }

        [Test]
        public void CanBeConvertedFrom_StringToEnum_ReturnsTrue()
        {
            Assert.IsTrue(PropertyUtils.CanBeConvertedFrom(typeof(DayOfWeek), typeof(string)));
        }

        [Test]
        public void CanBeConvertedFrom_InterfaceImplementation_ReturnsTrue()
        {
            Assert.IsTrue(PropertyUtils.CanBeConvertedFrom(typeof(ISampleInterface), typeof(ImplementsSample)));
        }

        // ────────────────────────────────────────────────────────────────
        // MatchPropValue
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void MatchPropValue_SameValue_ReturnsSuccess()
        {
            var to = new SampleEntity { Name = "Alice" };
            var fr = new SampleEntity { Name = "Bob" };
            var result = PropertyUtils.MatchPropValue(to, fr, "Name");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Bob", to.Name);
        }

        [Test]
        public void MatchPropValue_NullTarget_ReturnsFail()
        {
            var fr = new SampleEntity { Name = "Bob" };
            var result = PropertyUtils.MatchPropValue(null, fr, "Name");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void MatchPropValue_NullSource_ReturnsFail()
        {
            var to = new SampleEntity { Name = "Alice" };
            var result = PropertyUtils.MatchPropValue(to, null, "Name");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void MatchPropValue_WithFilterReturnsTrue_UpdatesValue()
        {
            var to = new SampleEntity { Name = "Alice", Age = 30 };
            var fr = new SampleEntity { Name = "Bob", Age = 30 };
            // 仅当 Age 相同时才匹配 Name
            var result = PropertyUtils.MatchPropValue(to, fr, "Name",
                (t, f) => ((SampleEntity)t).Age == ((SampleEntity)f).Age);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Bob", to.Name);
        }

        [Test]
        public void MatchPropValue_WithFilterReturnsFalse_SkipsUpdate()
        {
            var to = new SampleEntity { Name = "Alice", Age = 30 };
            var fr = new SampleEntity { Name = "Bob", Age = 25 };
            var result = PropertyUtils.MatchPropValue(to, fr, "Name",
                (t, f) => ((SampleEntity)t).Age == ((SampleEntity)f).Age);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Alice", to.Name);
        }

        // ────────────────────────────────────────────────────────────────
        // MatchPropValues
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void MatchPropValues_AllPropertiesMatched_ReturnsSuccess()
        {
            var to = new SampleEntity { Name = "Old", Age = 1, Score = 0.5 };
            var fr = new SampleEntity { Name = "New", Age = 99, Score = 99.9 };
            var result = PropertyUtils.MatchPropValues(to, fr);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("New", to.Name);
            Assert.AreEqual(99, to.Age);
            Assert.AreEqual(99.9, to.Score, 1e-9);
        }

        [Test]
        public void MatchPropValues_WithIgnoredProperties_KeepsOriginalValue()
        {
            var to = new SampleEntity { Name = "Keep", Age = 10 };
            var fr = new SampleEntity { Name = "Ignore", Age = 99 };
            var result = PropertyUtils.MatchPropValues(to, fr, "Name");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Keep", to.Name);
            Assert.AreEqual(99, to.Age);
        }

        [Test]
        public void MatchPropValues_NullTarget_ReturnsFail()
        {
            var fr = new SampleEntity { Name = "Bob" };
            var result = PropertyUtils.MatchPropValues(null, fr);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void MatchPropValues_NullSource_ReturnsFail()
        {
            var to = new SampleEntity { Name = "Alice" };
            var result = PropertyUtils.MatchPropValues(to, null);

            Assert.IsFalse(result.IsSuccess);
        }
    }
}
