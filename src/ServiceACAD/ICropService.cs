using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;

namespace ServiceACAD
{
    /// <summary>
    ///     裁剪服务输入参数.
    /// </summary>
    public class CropInput
    {
        /// <summary>
        ///     WCS 裁剪边界顶点列表（闭合多边形）.
        ///     <para>兼容字段：当 <see cref="Boundary"/> 为 null 时使用.</para>
        /// </summary>
        public IReadOnlyList<Point2D> BoundaryPoints { get; set; }

        /// <summary>
        ///     裁剪边界抽象（优先使用，支持圆/椭圆精确边界）.
        ///     <para>如果设置了此字段，将优先于 <see cref="BoundaryPoints"/> 使用.</para>
        /// </summary>
        public ICropBoundary Boundary { get; set; }

        /// <summary>
        ///     待裁剪的实体 ID 集合.
        /// </summary>
        public List<ObjectId> EntityIds { get; set; }

        /// <summary>
        ///     事务服务引用.
        /// </summary>
        public ITransactionService TransactionService { get; set; }

        /// <summary>
        ///     获取有效的裁剪边界：优先返回 <see cref="Boundary"/>，
        ///     否则用 <see cref="BoundaryPoints"/> 构造 <see cref="PolygonCropBoundary"/>.
        /// </summary>
        public ICropBoundary GetEffectiveBoundary()
        {
            if (this.Boundary != null)
                return this.Boundary;
            if (this.BoundaryPoints != null && this.BoundaryPoints.Count >= 3)
                return new PolygonCropBoundary(this.BoundaryPoints);
            return null;
        }
    }

    /// <summary>
    ///     裁剪操作结果，包含统计信息.
    /// </summary>
    public class CropResult
    {
        /// <summary>
        ///     被删除的实体数量.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        ///     被拆分的实体数量（Curve 类型）.
        /// </summary>
        public int SplitCount { get; set; }

        /// <summary>
        ///     保留在原位置的实体数量（完全在内部/外部无需处理）.
        /// </summary>
        public int KeptCount { get; set; }

        /// <summary>
        ///     跳过的实体数量（未识别的类型）.
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        ///     被 ExplodeAsShown 炸开的块参照数量.
        /// </summary>
        public int ExplodedCount { get; set; }

        /// <summary>
        ///     BlockReference 占位处理（完全在内侧=保留；完全在外侧=删除；相交=删除）.
        /// </summary>
        public int BlockRefHandledCount { get; set; }
    }

    /// <summary>
    ///     裁剪服务接口 - 根据 WCS 边界裁剪实体.
    /// </summary>
    public interface ICropService
    {
        /// <summary>
        ///     裁剪操作：保留边界内部的实体，删除或拆分边界外部的实体.
        /// </summary>
        /// <param name="input">裁剪输入参数.</param>
        /// <returns>裁剪结果.</returns>
        OpResult<CropResult> CropInside(CropInput input);

        /// <summary>
        ///     裁剪操作：保留边界外部的实体，删除或拆分边界内部的实体.
        /// </summary>
        /// <param name="input">裁剪输入参数.</param>
        /// <returns>裁剪结果.</returns>
        OpResult<CropResult> CropOutside(CropInput input);
    }
}