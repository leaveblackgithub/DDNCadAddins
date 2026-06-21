using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     曲线采样器接口 — 将各种曲线段转换为多边形顶点列表（纯数学运算，无 AutoCAD 依赖）.
    /// </summary>
    public interface ICurveSampler
    {
        /// <summary>
        ///     将圆弧段采样为直线段顶点.
        /// </summary>
        IReadOnlyList<Point2D> SampleArc(
            double startX, double startY,
            double centerX, double centerY,
            double radius,
            double startAngleRad, double endAngleRad,
            bool isClockwise);

        /// <summary>
        ///     将椭圆弧段采样为直线段顶点.
        /// </summary>
        IReadOnlyList<Point2D> SampleEllipticalArc(
            double centerX, double centerY,
            double majorRadius, double minorRatio,
            double startAngleRad, double endAngleRad,
            bool isClockwise);

        /// <summary>
        ///     将通用曲线采样为多边形（通过 evaluator 回调计算参数空间中的点）.
        /// </summary>
        IReadOnlyList<Point2D> SampleGenericCurve(
            Point2D startPoint, Point2D endPoint,
            int samples,
            Func<double, Point2D> evaluator);

        /// <summary>
        ///     去除相邻重复点（距离小于 1e-20 视为重复）.
        /// </summary>
        IReadOnlyList<Point2D> RemoveAdjacentDuplicates(IReadOnlyList<Point2D> polygon);

        /// <summary>
        ///     闭合多边形：如果首尾点不重合，添加首点作为终点.
        /// </summary>
        IReadOnlyList<Point2D> ClosePolygon(IReadOnlyList<Point2D> polygon);
    }
}
