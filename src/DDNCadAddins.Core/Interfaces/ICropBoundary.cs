using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     裁剪边界抽象接口 — 统一多边形/圆/椭圆等不同边界类型的几何判断.
    ///     <para>
    ///         实现类：
    ///         - <see cref="PolygonCropBoundary"/>：多段线边界（精确）
    ///         - <see cref="CircleCropBoundary"/>：圆边界（精确解析）
    ///         - <see cref="EllipseCropBoundary"/>：椭圆边界（精确解析）
    ///         - <see cref="SplineCropBoundary"/>：样条线边界（采样代理）
    ///     </para>
    ///     纯数学接口，无 AutoCAD 依赖.
    /// </summary>
    public interface ICropBoundary
    {
        /// <summary>
        ///     判断点是否在边界内部（含边界线）.
        /// </summary>
        /// <param name="point">测试点（WCS）.</param>
        /// <returns>true 在内部或边界上；false 在外部.</returns>
        bool IsPointInside(Point2D point);

        /// <summary>
        ///     计算线段与边界的所有交点，按距离线段起点排序.
        /// </summary>
        /// <param name="segStart">线段起点.</param>
        /// <param name="segEnd">线段终点.</param>
        /// <returns>排序后的交点列表，无交点时返回空列表.</returns>
        List<Point2D> FindLineIntersections(Point2D segStart, Point2D segEnd);

        /// <summary>
        ///     快速包围盒分类（用于实体快速筛选）.
        /// </summary>
        /// <param name="minPoint">包围盒最小点.</param>
        /// <param name="maxPoint">包围盒最大点.</param>
        /// <returns>包含关系枚举.</returns>
        ContainmentResult ClassifyBoundingBox(Point2D minPoint, Point2D maxPoint);

        /// <summary>
        ///     边界包围盒最小点（WCS）.
        /// </summary>
        Point2D BoundingBoxMin { get; }

        /// <summary>
        ///     边界包围盒最大点（WCS）.
        /// </summary>
        Point2D BoundingBoxMax { get; }

        /// <summary>
        ///     获取近似多边形顶点列表（用于兼容/兜底场景）.
        ///     对于多段线边界返回原始顶点；对于圆/椭圆返回采样顶点.
        /// </summary>
        /// <returns>多边形顶点列表（WCS）.</returns>
        IReadOnlyList<Point2D> GetApproximatePolygon();
    }
}
