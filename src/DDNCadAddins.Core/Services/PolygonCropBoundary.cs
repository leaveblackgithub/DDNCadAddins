using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     多段线裁剪边界 — 基于多边形射线法的精确实现.
    ///     <para>
    ///         从 <see cref="CropGeometryService"/> 提取逻辑，作为 <see cref="ICropBoundary"/> 的多边形实现.
    ///         适用于 Polyline / 矩形 / 任意闭合多段线边界.
    ///     </para>
    ///     纯数学运算，无 AutoCAD 依赖.
    /// </summary>
    public class PolygonCropBoundary : ICropBoundary
    {
        private readonly CropGeometryService _geometry;
        private readonly IReadOnlyList<Point2D> _polygon;
        private readonly Point2D _bboxMin;
        private readonly Point2D _bboxMax;

        /// <summary>
        ///     构造多段线裁剪边界.
        /// </summary>
        /// <param name="polygonVertices">多边形顶点列表（WCS，按顺序闭合，不重复首尾点）.</param>
        public PolygonCropBoundary(IReadOnlyList<Point2D> polygonVertices)
        {
            this._geometry = new CropGeometryService();
            this._polygon = polygonVertices;

            // 预计算包围盒
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var pt in polygonVertices)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y > maxY) maxY = pt.Y;
            }
            this._bboxMin = new Point2D(minX, minY);
            this._bboxMax = new Point2D(maxX, maxY);
        }

        /// <inheritdoc />
        public bool IsPointInside(Point2D point)
        {
            return this._geometry.IsPointInPolygon(point, this._polygon);
        }

        /// <inheritdoc />
        public List<Point2D> FindLineIntersections(Point2D segStart, Point2D segEnd)
        {
            return this._geometry.FindLineSegmentIntersections(segStart, segEnd, this._polygon);
        }

        /// <inheritdoc />
        public ContainmentResult ClassifyBoundingBox(Point2D minPoint, Point2D maxPoint)
        {
            return this._geometry.ClassifyBoundingBox(minPoint, maxPoint, this._polygon);
        }

        /// <inheritdoc />
        public Point2D BoundingBoxMin => this._bboxMin;

        /// <inheritdoc />
        public Point2D BoundingBoxMax => this._bboxMax;

        /// <inheritdoc />
        public IReadOnlyList<Point2D> GetApproximatePolygon()
        {
            return this._polygon;
        }
    }
}
