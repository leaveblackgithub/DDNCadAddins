using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropLineResult = ServiceACAD.OpResult<ServiceACAD.CropLineResult>;

namespace ServiceACAD
{
    /// <summary>
    ///     直线裁剪结果.
    /// </summary>
    public class CropLineResult
    {
        /// <summary>
        ///     被删除的直线数量.
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        ///     被拆分的直线数量.
        /// </summary>
        public int SplitCount { get; set; }

        /// <summary>
        ///     保留的直线数量（完全在内部/外部无需处理）.
        /// </summary>
        public int KeptCount { get; set; }

        /// <summary>
        ///     跳过的直线数量（无效或错误）.
        /// </summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     直线裁剪服务 - 专门处理 Line 类型的裁剪操作.
    ///     支持保留边界内部或外部的直线，自动拆分跨越边界的直线.
    /// </summary>
    public class CropLineService
    {
        private readonly ICropGeometryService _cropGeometry;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="cropGeometry">几何计算服务，为空时使用默认实现.</param>
        public CropLineService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        /// <summary>
        ///     裁剪直线：保留边界内部的直线.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="lineIds">待裁剪直线的 ObjectId 列表.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        public OpResultOfCropLineResult CropLinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> lineIds,
            ITransactionService transactionService)
        {
            return this.CropLines(boundaryPoints, lineIds, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪直线：保留边界外部的直线.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="lineIds">待裁剪直线的 ObjectId 列表.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        public OpResultOfCropLineResult CropLinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> lineIds,
            ITransactionService transactionService)
        {
            return this.CropLines(boundaryPoints, lineIds, transactionService, keepInside: false);
        }

        /// <summary>
        ///     裁剪所有直线：保留边界内部的直线，自动选择图纸中所有 LINE 对象.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        public OpResultOfCropLineResult CropAllLinesInside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllLines(boundaryPoints, transactionService, keepInside: true);
        }

        /// <summary>
        ///     裁剪所有直线：保留边界外部的直线，自动选择图纸中所有 LINE 对象.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        public OpResultOfCropLineResult CropAllLinesOutside(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService)
        {
            return this.CropAllLines(boundaryPoints, transactionService, keepInside: false);
        }

        /// <summary>
        ///     自动选择图纸中所有 LINE 对象进行裁剪.
        /// </summary>
        /// <param name="boundaryPoints">边界多边形顶点列表（WCS，至少3个点）.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <param name="keepInside">true 保留内部直线，false 保留外部直线.</param>
        /// <returns>裁剪结果，包含删除/拆分/保留/跳过的数量.</returns>
        private OpResultOfCropLineResult CropAllLines(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return OpResultOfCropLineResult.Fail("裁剪边界顶点不足（至少需要3个点）");
                }

                if (transactionService == null)
                {
                    return OpResultOfCropLineResult.Fail("事务服务引用为空");
                }

                // 通过事务服务的查询接口获取模型空间中所有 LINE 对象
                var allLineIds = transactionService.GetChildObjectsFromModelspace<Line>();

                if (allLineIds == null || allLineIds.Count == 0)
                {
                    return OpResultOfCropLineResult.Fail("图纸中没有找到任何直线");
                }

                return this.CropLines(boundaryPoints, allLineIds, transactionService, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllLines 操作失败: {ex.Message}", ex);
                return OpResultOfCropLineResult.Fail($"自动裁剪直线失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     核心直线裁剪逻辑.
        /// </summary>
        private OpResultOfCropLineResult CropLines(
            IReadOnlyList<CorePoint2D> boundaryPoints,
            List<ObjectId> lineIds,
            ITransactionService transactionService,
            bool keepInside)
        {
            try
            {
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return OpResultOfCropLineResult.Fail("裁剪边界顶点不足（至少需要3个点）");
                }

                if (lineIds == null || lineIds.Count == 0)
                {
                    return OpResultOfCropLineResult.Fail("待裁剪的直线列表为空");
                }

                if (transactionService == null)
                {
                    return OpResultOfCropLineResult.Fail("事务服务引用为空");
                }

                var result = new CropLineResult();

                foreach (var lineId in lineIds)
                {
                    try
                    {
                        if (!lineId.IsValid || lineId.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        var entity = transactionService.GetObject<Entity>(lineId);
                        if (entity == null || entity.IsErased)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        if (!(entity is Line line))
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        this.ProcessLine(line, boundaryPoints, keepInside, transactionService, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理直线 {lineId} 时发生异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                if (result.DeletedCount == 0 && result.SplitCount == 0 && result.KeptCount == 0)
                {
                    return OpResultOfCropLineResult.Fail("没有直线被处理");
                }

                return OpResultOfCropLineResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropLines 操作失败: {ex.Message}", ex);
                return OpResultOfCropLineResult.Fail($"直线裁剪失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理单条直线的裁剪：判断端点位置，决定保留、删除或拆分.
        /// </summary>
        private void ProcessLine(
            Line line,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropLineResult result)
        {
            var startPt = new CorePoint2D(line.StartPoint.X, line.StartPoint.Y);
            var endPt = new CorePoint2D(line.EndPoint.X, line.EndPoint.Y);

            // 先求直线与边界的全部交点（已沿线排序）
            var intersections = this._cropGeometry.FindLineSegmentIntersections(startPt, endPt, boundaryPoints);

            // 无交点：整条直线在边界一侧，用中点判断保留或删除
            // （两端点可能都在内或都在外，对凹多边形不能仅凭端点同侧就误删穿越线）
            if (intersections.Count == 0)
            {
                var midPt = new CorePoint2D(
                    (line.StartPoint.X + line.EndPoint.X) / 2.0,
                    (line.StartPoint.Y + line.EndPoint.Y) / 2.0);
                var midInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);

                if ((keepInside && midInside) || (!keepInside && !midInside))
                {
                    result.KeptCount++;
                }
                else
                {
                    this.DeleteLine(line, result);
                }

                return;
            }

            // 有交点：无论端点是否同侧，都按交点拆分并逐段判断
            this.SplitLineAndKeep(line, intersections, boundaryPoints, keepInside, transactionService, result);
        }


        /// <summary>
        ///     按交点拆分直线，保留符合条件的段，删除其余段.
        ///     先判断所有段是否在同侧（全部保留或全部删除），避免不必要拆分.
        /// </summary>
        /// <param name="line">待拆分的直线.</param>
        /// <param name="intersections">直线与边界的交点列表.</param>
        /// <param name="boundaryPoints">边界多边形顶点，用于判断拆分段是否在内部.</param>
        /// <param name="keepInside">true 保留内部段，false 保留外部段.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <param name="result">统计结果.</param>
        private void SplitLineAndKeep(
            Line line,
            List<CorePoint2D> intersections,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService transactionService,
            CropLineResult result)
        {
            try
            {
                var start3d = line.StartPoint;
                var end3d = line.EndPoint;

                // 构造沿线节点序列：起点 + 已排序交点 + 终点，相邻节点构成一段
                var nodes = new List<Point3d> { start3d };
                foreach (var p in intersections)
                {
                    nodes.Add(new Point3d(p.X, p.Y, InterpolateZ(start3d, end3d, p)));
                }

                nodes.Add(end3d);

                // 第一步：检查所有段的中点是否在同侧，决定保留/删除/拆分
                var allInside = true;
                var allOutside = true;
                var segmentChecks = new bool[nodes.Count - 1];
                for (var i = 0; i < nodes.Count - 1; i++)
                {
                    var a = nodes[i];
                    var b = nodes[i + 1];
                    if (a.DistanceTo(b) < 1e-9)
                    {
                        segmentChecks[i] = false;
                        continue;
                    }

                    var midPt = new CorePoint2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                    var isInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);
                    segmentChecks[i] = isInside;

                    if (isInside)
                    {
                        allOutside = false;
                    }
                    else
                    {
                        allInside = false;
                    }
                }

                // 所有段都在目标侧：整条直线保留，无需拆分
                if ((keepInside && allInside) || (!keepInside && allOutside))
                {
                    result.KeptCount++;
                    return;
                }

                // 所有段都在非目标侧：整条直线删除
                if ((keepInside && allOutside) || (!keepInside && allInside))
                {
                    this.DeleteLine(line, result);
                    return;
                }

                // 第二步：混合情况，需要拆分
                var segmentsToKeep = new List<Line>();
                for (var i = 0; i < nodes.Count - 1; i++)
                {
                    var a = nodes[i];
                    var b = nodes[i + 1];
                    if (a.DistanceTo(b) < 1e-9)
                    {
                        continue;
                    }

                    var isInside = segmentChecks[i];
                    if ((keepInside && isInside) || (!keepInside && !isInside))
                    {
                        segmentsToKeep.Add(new Line(a, b)
                        {
                            Layer = line.Layer,
                            Color = line.Color,
                            Linetype = line.Linetype,
                        });
                    }
                }

                if (segmentsToKeep.Count == 0)
                {
                    this.DeleteLine(line, result);
                    return;
                }

                if (!line.IsWriteEnabled)
                {
                    line.UpgradeOpen();
                }

                line.Erase();

                foreach (var segment in segmentsToKeep)
                {
                    transactionService.AppendEntityToCurrentSpace(segment);
                }

                result.SplitCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"拆分直线失败 (ID={line.ObjectId}): {ex.Message}");
                this.DeleteLine(line, result);
            }
        }

        /// <summary>
        ///     按 2D 投影参数在直线起止点之间线性插值出交点的 Z 值.
        /// </summary>
        /// <param name="start">直线起点（含 Z）.</param>
        /// <param name="end">直线终点（含 Z）.</param>
        /// <param name="p">交点的 2D 坐标.</param>
        /// <returns>插值得到的 Z 值.</returns>
        private static double InterpolateZ(Point3d start, Point3d end, CorePoint2D p)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lenSq = (dx * dx) + (dy * dy);
            if (lenSq < 1e-12)
            {
                return start.Z;
            }

            var t = (((p.X - start.X) * dx) + ((p.Y - start.Y) * dy)) / lenSq;
            return start.Z + (t * (end.Z - start.Z));
        }


        /// <summary>
        ///     删除直线并更新统计.
        /// </summary>
        private void DeleteLine(Line line, CropLineResult result)
        {
            try
            {
                if (!line.IsWriteEnabled)
                {
                    line.UpgradeOpen();
                }

                line.Erase();
                result.DeletedCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"删除直线失败 (ID={line.ObjectId}): {ex.Message}");
                result.SkippedCount++;
            }
        }
    }
}
