namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     二维点（与 CAD API 无关）
    /// </summary>
    public struct Point2D
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        ///     X 坐标
        /// </summary>
        public double X { get; }

        /// <summary>
        ///     Y 坐标
        /// </summary>
        public double Y { get; }
    }
}
