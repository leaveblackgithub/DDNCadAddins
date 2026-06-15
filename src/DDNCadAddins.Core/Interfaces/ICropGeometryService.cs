using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     裁剪几何计算服务接口 - 纯逻辑，无 CAD 依赖.
    ///     提供射线法判断、线段与多边形求交、拆分段分类等核心几何算法。
    /// </summary>
    public interface ICropGeometryService
    {
        /// <summary>
        ///     判断一个点是否在闭合多边形内部（含边界线上）.
        ///     使用射线法（偶数=外部，奇数=内部）.
        /// </summary>
        /// <param name="point">测试点.</param>
        /// <param name="polygonVertices">多边形的顶点列表（WCS，按顺序闭合）.</param>
        /// <returns>true 在内部或边界上；false 在外部.</returns>
        bool IsPointInPolygon(Point2D point, IReadOnlyList<Point2D> polygonVertices);

        /// <summary>
        ///     判断一个包围盒与多边形的关系.
        /// </summary>
        /// <param name="minPoint">包围盒最小点.</param>
        /// <param name="maxPoint">包围盒最大点.</param>
        /// <param name="polygonVertices">多边形的顶点列表.</param>
        /// <returns>包含关系枚举.</returns>
        ContainmentResult ClassifyBoundingBox(
            Point2D minPoint,
            Point2D maxPoint,
            IReadOnlyList<Point2D> polygonVertices);

        /// <summary>
        ///     计算一条线段与闭合多边形所有边的交点列表.
        ///     交点按距离线段起点排序.
        /// </summary>
        /// <param name="segStart">线段起点.</param>
        /// <param name="segEnd">线段终点.</param>
        /// <param name="polygonVertices">多边形的顶点列表.</param>
        /// <returns>排序后的交点列表，无交点时返回空列表.</returns>
        List<Point2D> FindLineSegmentIntersections(
            Point2D segStart,
            Point2D segEnd,
            IReadOnlyList<Point2D> polygonVertices);

        /// <summary>
        ///     对一组点进行排序（按距离第一个点的参数 t 值从小到大）.
        /// </summary>
        /// <param name="startPoint">排序参考起点.</param>
        /// <param name="points">待排序的点集合.</param>
        /// <returns>排序后的点列表.</returns>
        List<Point2D> SortPointsAlongLine(Point2D startPoint, List<Point2D> points);

        /// <summary>
        ///     计算两条线段的交点.
        /// </summary>
        /// <param name="p1">线段1起点.</param>
        /// <param name="p2">线段1终点.</param>
        /// <param name="p3">线段2起点.</param>
        /// <param name="p4">线段2终点.</param>
        /// <param name="intersection">输出交点.</param>
        /// <returns>true 如果有交点；false 如果平行或不相交.</returns>
        bool TryGetSegmentIntersection(
            Point2D p1, Point2D p2,
            Point2D p3, Point2D p4,
            out Point2D intersection);
    }
}