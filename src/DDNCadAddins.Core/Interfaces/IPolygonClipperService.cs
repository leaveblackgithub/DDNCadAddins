using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     多边形裁剪服务接口 — Sutherland-Hodgman 算法.
    ///     纯几何运算，无 CAD 依赖.
    /// </summary>
    public interface IPolygonClipperService
    {
        /// <summary>
        ///     用裁剪多边形对 subject 多边形进行裁剪.
        /// </summary>
        /// <param name="subjectPolygon">被裁剪多边形顶点列表（闭合，首尾不重复）.</param>
        /// <param name="clipPolygon">裁剪多边形顶点列表（闭合，首尾不重复）.</param>
        /// <param name="keepInside">true=保留内部（交集），false=保留外部（差集）.</param>
        /// <returns>裁剪结果多边形列表（0个或多个不相交的闭合多边形）.</returns>
        IReadOnlyList<IReadOnlyList<Point2D>> ClipPolygon(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon,
            bool keepInside = true);

        /// <summary>
        ///     用裁剪多边形对 subject 多边形进行裁剪，返回带来源标记的结果.
        ///     用于混合绘制：曲线段用 CurveFit，折线段保持折线.
        /// </summary>
        /// <param name="subjectPolygon">被裁剪多边形顶点列表（闭合，首尾不重复）.</param>
        /// <param name="clipPolygon">裁剪多边形顶点列表（闭合，首尾不重复）.</param>
        /// <param name="keepInside">true=保留内部（交集），false=保留外部（差集）.</param>
        /// <returns>带来源标记的裁剪结果多边形列表.</returns>
        IReadOnlyList<ClippedPolygonWithSources> ClipPolygonWithSources(
            IReadOnlyList<Point2D> subjectPolygon,
            IReadOnlyList<Point2D> clipPolygon,
            bool keepInside = true);
    }
}
