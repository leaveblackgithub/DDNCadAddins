using System;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.UnitTests
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
            var obj = new SampleEntity();
            var result = PropertyUtils.SetPropertyValue(obj, "Name", "World");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("World", obj.Name);
        }

        [Test]
        public void SetPropertyValue_IntProperty_SetsCorrectly()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.SetPropertyValue(obj, "Age", 30);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(30, obj.Age);
        }

        [Test]
        public void SetPropertyValue_NullObject_ReturnsFail()
        {
            var result = PropertyUtils.SetPropertyValue(null, "Name", "x");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void SetPropertyValue_NonExistentProperty_ReturnsFail()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.SetPropertyValue(obj, "Ghost", "x");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void SetPropertyValue_ReadOnlyProperty_ReturnsFail()
        {
            var obj = new SampleEntity();
            var result = PropertyUtils.SetPropertyValue(obj, "ReadOnlyProp", "x");

            Assert.IsFalse(result.IsSuccess);
        }

        // ────────────────────────────────────────────────────────────────
        // MatchPropValue
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void MatchPropValue_CopiesPropertyFromSourceToTarget()
        {
            var source = new SampleEntity { Name = "SourceName" };
            var target = new SampleEntity { Name = "TargetName" };

            var result = PropertyUtils.MatchPropValue(target, source, "Name");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("SourceName", target.Name);
        }

        [Test]
        public void MatchPropValue_WithFilterReturnsFalse_DoesNotCopy()
        {
            var source = new SampleEntity { Age = 99 };
            var target = new SampleEntity { Age = 10 };

            var result = PropertyUtils.MatchPropValue(target, source, "Age",
                (t, f) => false);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(10, target.Age);
        }

        [Test]
        public void MatchPropValue_WithFilterReturnsTrue_Copies()
        {
            var source = new SampleEntity { Age = 99 };
            var target = new SampleEntity { Age = 10 };

            var result = PropertyUtils.MatchPropValue(target, source, "Age",
                (t, f) => true);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(99, target.Age);
        }

        [Test]
        public void MatchPropValue_NullSource_ReturnsFail()
        {
            var target = new SampleEntity();
            var result = PropertyUtils.MatchPropValue(target, null, "Name");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void MatchPropValue_NullTarget_ReturnsFail()
        {
            var source = new SampleEntity { Name = "x" };
            var result = PropertyUtils.MatchPropValue(null, source, "Name");

            Assert.IsFalse(result.IsSuccess);
        }

        // ────────────────────────────────────────────────────────────────
        // MatchPropValues（批量复制）
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void MatchPropValues_CopiesAllMatchingProperties()
        {
            var source = new SampleEntity { Name = "S", Age = 5, Score = 9.5, Active = true };
            var target = new SampleEntity();

            var result = PropertyUtils.MatchPropValues(target, source);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("S", target.Name);
            Assert.AreEqual(5, target.Age);
            Assert.AreEqual(9.5, target.Score, 1e-10);
            Assert.IsTrue(target.Active);
        }

        [Test]
        public void MatchPropValues_IgnoresSpecifiedProperties()
        {
            var source = new SampleEntity { Name = "S", Age = 99 };
            var target = new SampleEntity { Name = "T", Age = 0 };

            PropertyUtils.MatchPropValues(target, source, "Name");

            Assert.AreEqual("T", target.Name, "Name should be ignored");
            Assert.AreEqual(99, target.Age, "Age should be copied");
        }
    }
}
