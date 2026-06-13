using System.Collections.Generic;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     XClip 边界几何计算服务接口 - 纯逻辑，无 CAD 依赖
    /// </summary>
    public interface IXClipBoundaryGeometryService
    {
        /// <summary>
        ///     将 XClip 局部边界点变换到 WCS，并在需要时将 2 点矩形扩展为 4 点
        /// </summary>
        /// <param name="localPoints">局部裁剪边界点</param>
        /// <param name="clipSpaceToWcs">裁剪空间到 WCS 的变换</param>
        /// <param name="originalInverseBlockTransform">创建裁剪时的逆块变换</param>
        /// <param name="blockTransform">当前块参照变换</param>
        /// <returns>WCS 边界顶点</returns>
        OpResult<IReadOnlyList<Point2D>> BuildWcsBoundaryPoints(
            IReadOnlyList<Point2D> localPoints,
            Matrix3D clipSpaceToWcs,
            Matrix3D originalInverseBlockTransform,
            Matrix3D blockTransform);
    }
}
