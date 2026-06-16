using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropArcResult = ServiceACAD.OpResult<ServiceACAD.CropArcResult>;

namespace ServiceACAD
{
    public class CropArcResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     圆弧裁剪服务 — 精确交点拆分（几何法，无采样）.
    /// </summary>
    public class CropArcService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropArcService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        public OpResultOfCropArcResult CropArcsInside(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> arcIds, ITransactionService ts)
            => this.CropArcs(bpts, arcIds, ts, keepInside: true);

        public OpResultOfCropArcResult CropArcsOutside(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> arcIds, ITransactionService ts)
            => this.CropArcs(bpts, arcIds, ts, keepInside: false);

        public OpResultOfCropArcResult CropAllArcsInside(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts)
            => this.CropAllArcs(bpts, ts, keepInside: true);

        public OpResultOfCropArcResult CropAllArcsOutside(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts)
            => this.CropAllArcs(bpts, ts, keepInside: false);

        private OpResultOfCropArcResult CropAllArcs(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (bpts == null || bpts.Count < 3) return OpResultOfCropArcResult.Fail("裁剪边界顶点不足");
                if (ts == null) return OpResultOfCropArcResult.Fail("事务服务引用为空");
                var all = ts.GetChildObjectsFromModelspace<Arc>();
                if (all == null || all.Count == 0) return OpResultOfCropArcResult.Fail("没有圆弧");
                return this.CropArcs(bpts, all, ts, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllArcs 失败: {ex.Message}", ex);
                return OpResultOfCropArcResult.Fail($"自动裁剪圆弧失败: {ex.Message}");
            }
        }

        private OpResultOfCropArcResult CropArcs(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> arcIds, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (bpts == null || bpts.Count < 3) return OpResultOfCropArcResult.Fail("裁剪边界顶点不足");
                if (arcIds == null || arcIds.Count == 0) return OpResultOfCropArcResult.Fail("待裁剪圆弧列表为空");
                if (ts == null) return OpResultOfCropArcResult.Fail("事务服务引用为空");

                var result = new CropArcResult();
                foreach (var id in arcIds)
                {
                    try
                    {
                        if (!id.IsValid || id.IsErased) { result.SkippedCount++; continue; }
                        var ent = ts.GetObject<Entity>(id);
                        if (ent == null || ent.IsErased) { result.SkippedCount++; continue; }
                        if (!(ent is Arc arc)) { result.SkippedCount++; continue; }
                        this.ProcessArc(arc, bpts, keepInside, ts, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理圆弧 {id} 异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                var total = result.DeletedCount + result.SplitCount + result.KeptCount;
                return total == 0 ? OpResultOfCropArcResult.Fail("没有圆弧被处理") : OpResultOfCropArcResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropArcs 失败: {ex.Message}", ex);
                return OpResultOfCropArcResult.Fail($"圆弧裁剪失败: {ex.Message}");
            }
        }

        private void ProcessArc(Arc arc, IReadOnlyList<CorePoint2D> bpts, bool keepInside, ITransactionService ts, CropArcResult result)
        {
            var ext = arc.GeometricExtents;
            if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9) { result.SkippedCount++; return; }
            var containment = this._cropGeometry.ClassifyBoundingBox(
                new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y), new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y), bpts);

            bool del = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);
            if (del) { DeleteArc(arc, result); return; }
            if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }
            SplitArcAndKeep(arc, bpts, keepInside, ts, result);
        }

        private void SplitArcAndKeep(Arc arc, IReadOnlyList<CorePoint2D> bpts, bool keepInside, ITransactionService ts, CropArcResult result)
        {
            try
            {
                var cx = arc.Center.X;
                var cy = arc.Center.Y;
                var r = arc.Radius;
                var sa = arc.StartAngle;
                var ea = arc.EndAngle;

                // 精确求圆与多边形各边的交点，过滤在弧段范围内的
                var angles = new List<double>();
                for (int i = 0, j = bpts.Count - 1; i < bpts.Count; j = i++)
                {
                    var segIx = GeometryHelper.LineCircleIntersection(bpts[j].X, bpts[j].Y, bpts[i].X, bpts[i].Y, cx, cy, r);
                    foreach (var pt in segIx)
                    {
                        if (!GeometryHelper.PointOnSegment(pt, bpts[j], bpts[i])) continue;
                        var ang = Math.Atan2(pt.Y - cy, pt.X - cx);
                        if (GeometryHelper.AngleInRange(ang, sa, ea))
                            angles.Add(GeometryHelper.NormalizeAngle(ang, sa, ea));
                    }
                }
                angles.Sort();

                // 节点序列
                var nodes = new List<double> { sa };
                nodes.AddRange(angles);
                nodes.Add(ea);

                // 逐段中点判断
                var kept = new List<Tuple<double, double>>();
                for (var i = 0; i < nodes.Count - 1; i++)
                {
                    var a = nodes[i];
                    var b = nodes[i + 1];
                    if (Math.Abs(b - a) < 1e-9) continue;
                    var midAng = (a + b) / 2.0;
                    var midX = cx + r * Math.Cos(midAng);
                    var midY = cy + r * Math.Sin(midAng);
                    var inside = this._cropGeometry.IsPointInPolygon(new CorePoint2D(midX, midY), bpts);
                    if ((keepInside && inside) || (!keepInside && !inside))
                        kept.Add(Tuple.Create(a, b));
                }

                if (kept.Count == 0) { DeleteArc(arc, result); return; }
                if (angles.Count == 0 && kept.Count == 1) { result.KeptCount++; return; }

                if (!arc.IsWriteEnabled) arc.UpgradeOpen();
                arc.Erase();

                foreach (var s in kept)
                {
                    if (Math.Abs(s.Item2 - s.Item1) < 1e-9) continue;
                    var na = new Arc(arc.Center, arc.Normal, r, s.Item1, s.Item2);
                    na.Layer = arc.Layer;
                    na.Color = arc.Color;
                    na.Linetype = arc.Linetype;
                    na.LineWeight = arc.LineWeight;
                    ts.AppendEntityToCurrentSpace(na);
                }
                result.SplitCount++;
                result.DeletedCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"拆分圆弧失败 (ID={arc.ObjectId}): {ex.Message}");
                DeleteArc(arc, result);
            }
        }

        private static void DeleteArc(Arc arc, CropArcResult result)
        {
            try
            {
                if (!arc.IsWriteEnabled) arc.UpgradeOpen();
                arc.Erase();
                result.DeletedCount++;
            }
            catch (System.Exception ex) { Logger._.Warn($"删除圆弧失败: {ex.Message}"); result.SkippedCount++; }
        }
    }
}