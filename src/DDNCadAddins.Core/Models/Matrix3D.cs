using System;

namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     4x4 变换矩阵（列主序，与 AutoCAD Matrix3d.ToArray() 布局一致）
    /// </summary>
    public struct Matrix3D
    {
        private readonly double _e0;
        private readonly double _e1;
        private readonly double _e2;
        private readonly double _e3;
        private readonly double _e4;
        private readonly double _e5;
        private readonly double _e6;
        private readonly double _e7;
        private readonly double _e8;
        private readonly double _e9;
        private readonly double _e10;
        private readonly double _e11;
        private readonly double _e12;
        private readonly double _e13;
        private readonly double _e14;
        private readonly double _e15;

        private Matrix3D(
            double e0, double e1, double e2, double e3,
            double e4, double e5, double e6, double e7,
            double e8, double e9, double e10, double e11,
            double e12, double e13, double e14, double e15)
        {
            _e0 = e0;
            _e1 = e1;
            _e2 = e2;
            _e3 = e3;
            _e4 = e4;
            _e5 = e5;
            _e6 = e6;
            _e7 = e7;
            _e8 = e8;
            _e9 = e9;
            _e10 = e10;
            _e11 = e11;
            _e12 = e12;
            _e13 = e13;
            _e14 = e14;
            _e15 = e15;
        }

        /// <summary>
        ///     单位矩阵
        /// </summary>
        public static Matrix3D Identity { get; } = FromArray(new[]
        {
            1d, 0d, 0d, 0d,
            0d, 1d, 0d, 0d,
            0d, 0d, 1d, 0d,
            0d, 0d, 0d, 1d
        });

        /// <summary>
        ///     从 AutoCAD Matrix3d.ToArray() 格式的 16 元素数组创建矩阵
        /// </summary>
        /// <param name="values">16 元素数组</param>
        /// <returns>矩阵</returns>
        public static Matrix3D FromArray(double[] values)
        {
            if (values == null || values.Length != 16)
            {
                throw new ArgumentException("矩阵数组必须包含 16 个元素", nameof(values));
            }

            return new Matrix3D(
                values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7],
                values[8], values[9], values[10], values[11],
                values[12], values[13], values[14], values[15]);
        }

        /// <summary>
        ///     左乘矩阵（与 AutoCAD Matrix3d.PreMultiplyBy 一致：result = other * this）
        /// </summary>
        /// <param name="other">左乘矩阵</param>
        /// <returns>乘积矩阵</returns>
        public Matrix3D PreMultiplyBy(Matrix3D other)
        {
            return Multiply(other, this);
        }

        /// <summary>
        ///     将局部平面点变换到 WCS 平面坐标
        /// </summary>
        /// <param name="localPoint">局部坐标点</param>
        /// <returns>WCS 坐标点</returns>
        public Point2D TransformPlanarPoint(Point2D localPoint)
        {
            var x = localPoint.X;
            var y = localPoint.Y;
            var transformedX = x * _e0 + y * _e4 + _e12;
            var transformedY = x * _e1 + y * _e5 + _e13;
            return new Point2D(transformedX, transformedY);
        }

        private static Matrix3D Multiply(Matrix3D left, Matrix3D right)
        {
            return new Matrix3D(
                left._e0 * right._e0 + left._e4 * right._e1 + left._e8 * right._e2 + left._e12 * right._e3,
                left._e1 * right._e0 + left._e5 * right._e1 + left._e9 * right._e2 + left._e13 * right._e3,
                left._e2 * right._e0 + left._e6 * right._e1 + left._e10 * right._e2 + left._e14 * right._e3,
                left._e3 * right._e0 + left._e7 * right._e1 + left._e11 * right._e2 + left._e15 * right._e3,

                left._e0 * right._e4 + left._e4 * right._e5 + left._e8 * right._e6 + left._e12 * right._e7,
                left._e1 * right._e4 + left._e5 * right._e5 + left._e9 * right._e6 + left._e13 * right._e7,
                left._e2 * right._e4 + left._e6 * right._e5 + left._e10 * right._e6 + left._e14 * right._e7,
                left._e3 * right._e4 + left._e7 * right._e5 + left._e11 * right._e6 + left._e15 * right._e7,

                left._e0 * right._e8 + left._e4 * right._e9 + left._e8 * right._e10 + left._e12 * right._e11,
                left._e1 * right._e8 + left._e5 * right._e9 + left._e9 * right._e10 + left._e13 * right._e11,
                left._e2 * right._e8 + left._e6 * right._e9 + left._e10 * right._e10 + left._e14 * right._e11,
                left._e3 * right._e8 + left._e7 * right._e9 + left._e11 * right._e10 + left._e15 * right._e11,

                left._e0 * right._e12 + left._e4 * right._e13 + left._e8 * right._e14 + left._e12 * right._e15,
                left._e1 * right._e12 + left._e5 * right._e13 + left._e9 * right._e14 + left._e13 * right._e15,
                left._e2 * right._e12 + left._e6 * right._e13 + left._e10 * right._e14 + left._e14 * right._e15,
                left._e3 * right._e12 + left._e7 * right._e13 + left._e11 * right._e14 + left._e15 * right._e15);
        }
    }
}
