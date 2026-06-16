using System;
using DDNCadAddins.Core.Models;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    /// <summary>
    ///     Matrix3D 4x4 变换矩阵的纯单元测试.
    ///     覆盖构造、单位矩阵、矩阵乘法、平面点变换.
    /// </summary>
    [TestFixture]
    public class Matrix3DTests
    {
        private const double Tol = 1e-12;

        // ========== 构造与 FromArray ==========

        [Test]
        public void FromArray_Valid16Elements_ReturnsMatrix()
        {
            var arr = new double[16];
            arr[0] = arr[5] = arr[10] = arr[15] = 1.0;
            var m = Matrix3D.FromArray(arr);
            Assert.IsNotNull(m);
        }

        [Test]
        public void FromArray_NullArray_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Matrix3D.FromArray(null));
        }

        [Test]
        public void FromArray_LessThan16_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => Matrix3D.FromArray(new double[15]));
            Assert.That(ex.Message, Does.Contain("16"));
        }

        [Test]
        public void FromArray_MoreThan16_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Matrix3D.FromArray(new double[17]));
        }

        // ========== Identity ==========

        [Test]
        public void Identity_IsNotNull()
        {
            var id = Matrix3D.Identity;
            Assert.IsNotNull(id);
        }

        [Test]
        public void Identity_TransformPlanarPoint_ReturnsSamePoint()
        {
            var pt = new Point2D(3.14, -2.72);
            var result = Matrix3D.Identity.TransformPlanarPoint(pt);
            Assert.AreEqual(3.14, result.X, Tol);
            Assert.AreEqual(-2.72, result.Y, Tol);
        }

        [Test]
        public void Identity_PreMultiplyByIdentity_ReturnsIdentity()
        {
            var result = Matrix3D.Identity.PreMultiplyBy(Matrix3D.Identity);
            var pt = new Point2D(7, 7);
            var transformed = result.TransformPlanarPoint(pt);
            Assert.AreEqual(7, transformed.X, Tol);
            Assert.AreEqual(7, transformed.Y, Tol);
        }

        // ========== TransformPlanarPoint — 平移 ==========

        [Test]
        public void TransformPlanarPoint_PureTranslation_MovesPoint()
        {
            // 平移 (10, 20)：e3=10, e7=20
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 20d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = trans.TransformPlanarPoint(new Point2D(5, 5));
            Assert.AreEqual(15, result.X, Tol);
            Assert.AreEqual(25, result.Y, Tol);
        }

        [Test]
        public void TransformPlanarPoint_NegativeTranslation_MovesPoint()
        {
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, -10d,
                0d, 1d, 0d, -30d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = trans.TransformPlanarPoint(new Point2D(100, 50));
            Assert.AreEqual(90, result.X, Tol);
            Assert.AreEqual(20, result.Y, Tol);
        }

        [Test]
        public void TransformPlanarPoint_OriginTranslated_ReturnsTranslationVector()
        {
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 3d,
                0d, 1d, 0d, -7d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = trans.TransformPlanarPoint(new Point2D(0, 0));
            Assert.AreEqual(3, result.X, Tol);
            Assert.AreEqual(-7, result.Y, Tol);
        }

        // ========== TransformPlanarPoint — 缩放 ==========

        [Test]
        public void TransformPlanarPoint_UniformScale2x_ScalesPoint()
        {
            var scale = Matrix3D.FromArray(new[]
            {
                2d, 0d, 0d, 0d,
                0d, 2d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = scale.TransformPlanarPoint(new Point2D(3, 4));
            Assert.AreEqual(6, result.X, Tol);
            Assert.AreEqual(8, result.Y, Tol);
        }

        [Test]
        public void TransformPlanarPoint_NonUniformScale_ScalesDifferently()
        {
            var scale = Matrix3D.FromArray(new[]
            {
                0.5d, 0d, 0d, 0d,
                0d, 3d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = scale.TransformPlanarPoint(new Point2D(10, 2));
            Assert.AreEqual(5, result.X, Tol);
            Assert.AreEqual(6, result.Y, Tol);
        }

        // ========== TransformPlanarPoint — 旋转 ==========

        [Test]
        public void TransformPlanarPoint_Rotate90Degrees_RotatesPoint()
        {
            // 绕原点逆时针旋转 90°: cos=0, sin=1
            var rot = Matrix3D.FromArray(new[]
            {
                0d, -1d, 0d, 0d,
                1d, 0d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = rot.TransformPlanarPoint(new Point2D(1, 0));
            Assert.AreEqual(0, result.X, Tol);
            Assert.AreEqual(1, result.Y, Tol);
        }

        [Test]
        public void TransformPlanarPoint_Rotate180Degrees_FlipsPoint()
        {
            var rot = Matrix3D.FromArray(new[]
            {
                -1d, 0d, 0d, 0d,
                0d, -1d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = rot.TransformPlanarPoint(new Point2D(3, 4));
            Assert.AreEqual(-3, result.X, Tol);
            Assert.AreEqual(-4, result.Y, Tol);
        }

        [Test]
        public void TransformPlanarPoint_Rotate45Degrees_PointsOnCircle()
        {
            var cos = Math.Cos(Math.PI / 4);
            var sin = Math.Sin(Math.PI / 4);
            var rot = Matrix3D.FromArray(new[]
            {
                cos, -sin, 0d, 0d,
                sin, cos, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            // (1, 0) 旋转 45° → (1/√2, 1/√2)
            var result = rot.TransformPlanarPoint(new Point2D(1, 0));
            Assert.AreEqual(cos, result.X, Tol);
            Assert.AreEqual(sin, result.Y, Tol);
        }

        // ========== TransformPlanarPoint — 组合变换 (旋转+平移) ==========

        [Test]
        public void TransformPlanarPoint_RotateThenTranslate_AppliesBoth()
        {
            // 先绕原点旋转 90°，再平移 (5, 5)
            // 旋转 90° (z轴)： [0 -1 0 0; 1 0 0 0; ...] → 平移 +5：e3=5, e7=5
            var xform = Matrix3D.FromArray(new[]
            {
                0d, -1d, 0d, 5d,
                1d, 0d, 0d, 5d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            // (1, 0) 旋转 → (0, 1)，再平移 → (5, 6)
            var result = xform.TransformPlanarPoint(new Point2D(1, 0));
            Assert.AreEqual(5, result.X, Tol);
            Assert.AreEqual(6, result.Y, Tol);
        }

        // ========== PreMultiplyBy ==========

        [Test]
        public void PreMultiplyBy_Identity_ReturnsSameMatrix()
        {
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 20d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = trans.PreMultiplyBy(Matrix3D.Identity);
            var pt = result.TransformPlanarPoint(new Point2D(5, 5));
            Assert.AreEqual(15, pt.X, Tol);
            Assert.AreEqual(25, pt.Y, Tol);
        }

        [Test]
        public void PreMultiplyBy_IdentityMatrix_DoesNotChange()
        {
            // PreMultiplyBy(other) = other * this
            // Identity.PreMultiplyBy(trans) = trans * Identity = trans (actually PreMultiplyBy is other * this, so other=trans, this=Identity → trans * Identity = trans)
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 20d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var result = Matrix3D.Identity.PreMultiplyBy(trans);
            var pt = result.TransformPlanarPoint(new Point2D(5, 5));
            Assert.AreEqual(15, pt.X, Tol);
            Assert.AreEqual(25, pt.Y, Tol);
        }

        [Test]
        public void PreMultiplyBy_TwoTranslations_AddsOffsets()
        {
            // 平移 (10, 0) 再平移 (0, 20) = 平移 (10, 20)
            var t1 = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var t2 = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 0d,
                0d, 1d, 0d, 20d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            // result = t2 * t1：先 t1 平移 (10,0)，再 t2 平移 (0,20) → (10,20)
            var result = t1.PreMultiplyBy(t2);
            var pt = result.TransformPlanarPoint(new Point2D(0, 0));
            Assert.AreEqual(10, pt.X, Tol);
            Assert.AreEqual(20, pt.Y, Tol);
        }

        [Test]
        public void PreMultiplyBy_ScaleThenTranslate_AppliesCorrectly()
        {
            // 先缩放 2x，再平移 (10, 0)
            var scale = Matrix3D.FromArray(new[]
            {
                2d, 0d, 0d, 0d,
                0d, 2d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            // result = trans * scale：先 scale 缩放 (3,3)→(6,6)，再 trans 平移 (6+10, 6+0) = (16,6)
            var result = scale.PreMultiplyBy(trans);
            var pt = result.TransformPlanarPoint(new Point2D(3, 3));
            Assert.AreEqual(16, pt.X, Tol);
            Assert.AreEqual(6, pt.Y, Tol);
        }

        [Test]
        public void PreMultiplyBy_TranslateThenScale_AppliesCorrectly()
        {
            // 先平移，再缩放：result = scale * trans
            // 平移 (10,0) 再缩放 2x：原点 (0,0) → 平移 (10,0) → 缩放 (20,0)
            var trans = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var scale = Matrix3D.FromArray(new[]
            {
                2d, 0d, 0d, 0d,
                0d, 2d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            // result = scale * trans
            var result = trans.PreMultiplyBy(scale);
            var pt = result.TransformPlanarPoint(new Point2D(0, 0));
            Assert.AreEqual(20, pt.X, Tol);
            Assert.AreEqual(0, pt.Y, Tol);
        }

        // ========== 边界情况 ==========

        [Test]
        public void TransformPlanarPoint_AllZeros_PointUnchanged()
        {
            // e0=e5=0 等等，但 e3 和 e7 也是 0
            var zeroish = Matrix3D.FromArray(new double[16]);
            var pt = zeroish.TransformPlanarPoint(new Point2D(5, 5));
            Assert.AreEqual(0, pt.X, Tol);
            Assert.AreEqual(0, pt.Y, Tol);
        }

        [Test]
        public void TransformPlanarPoint_ShearEffect()
        {
            // 剪切：e1=2, e4=0.5
            var shear = Matrix3D.FromArray(new[]
            {
                1d, 2d, 0d, 0d,
                0.5d, 1d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            // (1, 0) → (1*1+0*2, 1*0.5+0*1) = (1, 0.5)
            var result = shear.TransformPlanarPoint(new Point2D(1, 0));
            Assert.AreEqual(1, result.X, Tol);
            Assert.AreEqual(0.5, result.Y, Tol);
        }
    }
}
