using System;
using System.Collections.Generic;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     XClip 边界几何计算服务 - 纯逻辑，无 CAD 依赖
    /// </summary>
    public class XClipBoundaryGeometryService : IXClipBoundaryGeometryService
    {
        /// <inheritdoc />
        public OpResult<IReadOnlyList<Point2D>> BuildWcsBoundaryPoints(
            IReadOnlyList<Point2D> localPoints,
            Matrix3D clipSpaceToWcs,
            Matrix3D originalInverseBlockTransform,
            Matrix3D blockTransform)
        {
            try
            {
                if (localPoints == null || localPoints.Count == 0)
                {
                    return OpResult<IReadOnlyList<Point2D>>.Fail("XClip边界点为空");
                }

                var clipToWcs = clipSpaceToWcs
                    .PreMultiplyBy(originalInverseBlockTransform)
                    .PreMultiplyBy(blockTransform);

                var expandedPoints = ExpandLocalBoundaryPoints(localPoints);
                var wcsPoints = new List<Point2D>(expandedPoints.Count);

                foreach (var localPoint in expandedPoints)
                {
                    wcsPoints.Add(clipToWcs.TransformPlanarPoint(localPoint));
                }

                if (wcsPoints.Count < 3)
                {
                    return OpResult<IReadOnlyList<Point2D>>.Fail("XClip边界顶点不足");
                }

                return OpResult<IReadOnlyList<Point2D>>.Success(wcsPoints.AsReadOnly());
            }
            catch (Exception ex)
            {
                return OpResult<IReadOnlyList<Point2D>>.Fail($"计算XClip边界失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     将 2 点矩形对角点扩展为 4 点闭合边界
        /// </summary>
        /// <param name="localPoints">局部边界点</param>
        /// <returns>扩展后的局部边界点</returns>
        public static IList<Point2D> ExpandLocalBoundaryPoints(IReadOnlyList<Point2D> localPoints)
        {
            if (localPoints.Count > 2)
            {
                var copy = new List<Point2D>(localPoints.Count);
                for (var i = 0; i < localPoints.Count; i++)
                {
                    copy.Add(localPoints[i]);
                }

                return copy;
            }

            var p1 = localPoints[0];
            var p2 = localPoints[1];
            return new List<Point2D>
            {
                p1,
                new Point2D(p1.X, p2.Y),
                p2,
                new Point2D(p2.X, p1.Y)
            };
        }
    }
}
