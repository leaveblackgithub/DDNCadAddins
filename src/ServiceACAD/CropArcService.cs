using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
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
    ///     圆弧裁剪服务 — 精确交点拆分.
    ///     <para>支持精确边界（ICropBoundary）和折线边界（IReadOnlyList<CorePoint2D>）两种模式.</para>
    /// </summary>
    public class CropArcService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropArcService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        // ──────────────────────────────────────────────────────────────
        //  新签名：精确边界（ICropBoundary）— 主方法
        // ──────────────────────────────────────────────────────────────

        public OpResultOfCropArcResult CropArcsInside(
            ICropBoundary boundary, List<ObjectId> arcIds, ITransactionService ts)
            => this.CropArcs(boundary, arcIds, ts, keepInside: true);

        public OpResultOfCropArcResult CropArcsOutside(
            ICropBoundary boundary, List<ObjectId> arcIds, ITransactionService ts)
            => this.CropArcs(boundary, arcIds, ts, keepInside: false);

        // ──────────────────────────────────────────────────────────────
        //  旧签名：折线边界（IReadOnlyList<CorePoint2D>）— 兼容包装
        // ──────────────────────────────────────────────────────────────

        public OpResultOfCropArcResult CropArcsInside(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> arcIds, ITransactionService ts)
            => this.CropArcsInside(new PolygonCropBoundary(bpts), arcIds, ts);

        public OpResultOfCropArcResult CropArcsOutside(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> arcIds, ITransactionService ts)
            => this.CropArcsOutside(new PolygonCropBoundary(bpts), arcIds, ts);

        public OpResultOfCropArcResult CropAllArcsInside(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts)
            => this.CropAllArcs(bpts, ts, keepInside: true);

        public OpResultOfCropArcResult CropAllArcsOutside(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts)
            => this.CropAllArcs(bpts, ts, keepInside: false);

        // ──────────────────────────────────────────────────────────────
        //  私有核心逻辑
        // ──────────────────────────────────────────────────────────────

        private OpResultOfCropArcResult CropAllArcs(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (bpts == null || bpts.Count < 3) return OpResultOfCropArcResult.Fail("裁剪边界顶点不足");
                if (ts == null) return OpResultOfCropArcResult.Fail("事务服务引用为空");
                var all = ts.GetChildObjectsFromModelspace<Arc>();
                if (all == null || all.Count == 0) return OpResultOfCropArcResult.Fail("没有圆弧");
                return this.CropArcsInside(new PolygonCropBoundary(bpts), all, ts);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllArcs 失败: {ex.Message}", ex);
                return OpResultOfCropArcResult.Fail($"自动裁剪圆弧失败: {ex.Message}");
            }
        }

        private OpResultOfCropArcResult CropArcs(
            ICropBoundary boundary, List<ObjectId> arcIds, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (boundary == null) return OpResultOfCropArcResult.Fail("裁剪边界为空");
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
                        this.ProcessArc(arc, boundary, keepInside, ts, result);
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

        private void ProcessArc(Arc arc, ICropBoundary boundary, bool keepInside, ITransactionService ts, CropArcResult result)
        {
            var ext = arc.GeometricExtents;
            if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9) { result.SkippedCount++; return; }
            // ★ 用 ICropBoundary.ClassifyBoundingBox() 替代 CropGeometryService.ClassifyBoundingBox()
            var containment = boundary.ClassifyBoundingBox(
                new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y), new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y));

            bool del = keepInside
                ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                   containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);
            if (del) { DeleteArc(arc, result); return; }
            if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }
            SplitArcAndKeep(arc, boundary, keepInside, ts, result);
        }

        /// <summary>
        ///     圆弧拆分 — 弧段采样为弦段序列，对每条弦调用 boundary.FindLineIntersections().
        ///     当边界为 Circle/Ellipse 时使用解析解，精度远高于折线化边界.
        /// </summary>
        private void SplitArcAndKeep(Arc arc, ICropBoundary boundary, bool keepInside, ITransactionService ts, CropArcResult result)
        {
            try
            {
                var cx = arc.Center.X;
                var cy = arc.Center.Y;
                var r = arc.Radius;
                var sa = arc.StartAngle;
                var ea = arc.EndAngle;

                // ★ 弧段采样为弦段序列，对每条弦调用 boundary.FindLineIntersections()
                const int arcSamples = 64;
                var angles = new List<double>();
                for (int i = 0; i < arcSamples; i++)
                {
                    double t1 = (double)i / arcSamples;
                    double t2 = (double)(i + 1) / arcSamples;
                    double a1 = sa + (ea - sa) * t1;
                    double a2 = sa + (ea - sa) * t2;
                    var p1 = new CorePoint2D(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));
                    var p2 = new CorePoint2D(cx + r * Math.Cos(a2), cy + r * Math.Sin(a2));

                    var intersections = boundary.FindLineIntersections(p1, p2);
                    foreach (var pt in intersections)
                    {
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
                    // ★ 用 ICropBoundary.IsPointInside() 替代 CropGeometryService.IsPointInPolygon()
                    var inside = boundary.IsPointInside(new CorePoint2D(midX, midY));
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
