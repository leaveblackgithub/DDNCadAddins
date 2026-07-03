using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;
using OpResult = ServiceACAD.OpResult;
using OpResultOfCropPolylineResult = ServiceACAD.OpResult<ServiceACAD.CropPolylineResult>;

namespace ServiceACAD
{
    public class CropPolylineResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     多段线裁剪服务 — 直线段和弧段精确交点拆分.
    ///     <para>支持精确边界（ICropBoundary）和折线边界（IReadOnlyList<CorePoint2D>）两种模式.</para>
    /// </summary>
    public struct PolySegment
    {
        public Point2d Start;
        public Point2d End;
        public double Bulge;
        public int SourceIndex;
        public bool IsArc;
    }

    public class CropPolylineService
    {
        private readonly ICropGeometryService _cropGeometry;

        public CropPolylineService(ICropGeometryService cropGeometry = null)
        {
            this._cropGeometry = cropGeometry ?? new CropGeometryService();
        }

        // ──────────────────────────────────────────────────────────────
        //  新签名：精确边界（ICropBoundary）— 主方法
        // ──────────────────────────────────────────────────────────────

        public OpResultOfCropPolylineResult CropPolylinesInside(
            ICropBoundary boundary, List<ObjectId> ids, ITransactionService ts)
            => this.CropPolylines(boundary, ids, ts, keepInside: true);

        public OpResultOfCropPolylineResult CropPolylinesOutside(
            ICropBoundary boundary, List<ObjectId> ids, ITransactionService ts)
            => this.CropPolylines(boundary, ids, ts, keepInside: false);

        // ──────────────────────────────────────────────────────────────
        //  旧签名：折线边界（IReadOnlyList<CorePoint2D>）— 兼容包装
        // ──────────────────────────────────────────────────────────────

        public OpResultOfCropPolylineResult CropPolylinesInside(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> ids, ITransactionService ts)
            => this.CropPolylinesInside(new PolygonCropBoundary(bpts), ids, ts);

        public OpResultOfCropPolylineResult CropPolylinesOutside(
            IReadOnlyList<CorePoint2D> bpts, List<ObjectId> ids, ITransactionService ts)
            => this.CropPolylinesOutside(new PolygonCropBoundary(bpts), ids, ts);

        public OpResultOfCropPolylineResult CropAllPolylinesInside(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts)
            => this.CropAllPolylines(bpts, ts, keepInside: true);

        public OpResultOfCropPolylineResult CropAllPolylinesOutside(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts)
            => this.CropAllPolylines(bpts, ts, keepInside: false);

        // ──────────────────────────────────────────────────────────────
        //  私有核心逻辑
        // ──────────────────────────────────────────────────────────────

        private OpResultOfCropPolylineResult CropAllPolylines(
            IReadOnlyList<CorePoint2D> bpts, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (bpts == null || bpts.Count < 3) return OpResultOfCropPolylineResult.Fail("裁剪边界顶点不足");
                if (ts == null) return OpResultOfCropPolylineResult.Fail("事务服务引用为空");
                var all = ts.GetChildObjectsFromModelspace<Polyline>();
                if (all == null || all.Count == 0) return OpResultOfCropPolylineResult.Fail("没有多段线");
                return this.CropPolylinesInside(new PolygonCropBoundary(bpts), all, ts);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropAllPolylines 失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"自动裁剪多段线失败: {ex.Message}");
            }
        }

        private OpResultOfCropPolylineResult CropPolylines(
            ICropBoundary boundary, List<ObjectId> ids, ITransactionService ts, bool keepInside)
        {
            try
            {
                if (boundary == null) return OpResultOfCropPolylineResult.Fail("裁剪边界为空");
                if (ids == null || ids.Count == 0) return OpResultOfCropPolylineResult.Fail("待裁剪多段线列表为空");
                if (ts == null) return OpResultOfCropPolylineResult.Fail("事务服务引用为空");

                var result = new CropPolylineResult();
                foreach (var id in ids)
                {
                    try
                    {
                        if (!id.IsValid || id.IsErased) { result.SkippedCount++; continue; }
                        var ent = ts.GetObject<Entity>(id);
                        if (ent == null || ent.IsErased) { result.SkippedCount++; continue; }
                        if (!(ent is Polyline poly)) { result.SkippedCount++; continue; }
                        this.ProcessPolyline(poly, boundary, keepInside, ts, result);
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Warn($"处理多段线 {id} 异常: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                var total = result.DeletedCount + result.SplitCount + result.KeptCount;
                return total == 0 ? OpResultOfCropPolylineResult.Fail("没有多段线被处理") : OpResultOfCropPolylineResult.Success(result);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropPolylines 失败: {ex.Message}", ex);
                return OpResultOfCropPolylineResult.Fail($"多段线裁剪失败: {ex.Message}");
            }
        }

        private void ProcessPolyline(
            Polyline poly, ICropBoundary boundary, bool keepInside, ITransactionService ts, CropPolylineResult result)
        {
            if (!poly.Closed)
            {
                ProcessOpenPolyline(poly, boundary, keepInside, ts, result);
                return;
            }

            var ext = poly.GeometricExtents;
            // ★ 用 ICropBoundary.ClassifyBoundingBox() 替代 CropGeometryService.ClassifyBoundingBox()
            var containment = boundary.ClassifyBoundingBox(
                new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y), new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y));

            bool del = keepInside
                ? containment == ContainmentResult.Outside
                : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);
            if (del) { DeletePoly(poly, result); return; }
            if (containment != ContainmentResult.Intersects) { result.KeptCount++; return; }
            ProcessOpenPolyline(poly, boundary, keepInside, ts, result);
        }

        private void ProcessOpenPolyline(
            Polyline poly, ICropBoundary boundary, bool keepInside, ITransactionService ts, CropPolylineResult result)
        {
            try
            {
                var n = poly.NumberOfVertices;
                if (n < 2) { DeletePoly(poly, result); return; }

                var keptSubs = new List<PolySegment>();
                var totalSegCount = poly.Closed ? n : n - 1;

                for (var i = 0; i < totalSegCount; i++)
                {
                    var segType = poly.GetSegmentType(i);
                    if (segType == SegmentType.Line)
                    {
                        var ls = poly.GetLineSegment2dAt(i);
                        var startP = new CorePoint2D(ls.StartPoint.X, ls.StartPoint.Y);
                        var endP = new CorePoint2D(ls.EndPoint.X, ls.EndPoint.Y);
                        // ★ 用 ICropBoundary.FindLineIntersections() 替代 CropGeometryService.FindLineSegmentIntersections()
                        var ix = boundary.FindLineIntersections(startP, endP);
                        CollectKeptSubSegments(startP, endP, 0.0, ix, keepInside, boundary, keptSubs, -1);
                    }
                    else if (segType == SegmentType.Arc)
                    {
                        var arcSeg = poly.GetArcSegment2dAt(i);
                        var bulge = poly.GetBulgeAt(i);
                        ProcessArcSegmentExact(arcSeg, bulge, keepInside, boundary, keptSubs);
                    }
                }

                if (keptSubs.Count == 0) { DeletePoly(poly, result); return; }

                var chains = ChainSubSegments(keptSubs);

                if (!poly.IsWriteEnabled) poly.UpgradeOpen();
                poly.Erase();

                foreach (var chain in chains)
                {
                    var np = new Polyline();
                    np.Layer = poly.Layer;
                    np.Color = poly.Color;
                    np.Linetype = poly.Linetype;
                    np.LineWeight = poly.LineWeight;
                    np.ConstantWidth = poly.ConstantWidth;

                    np.AddVertexAt(0, chain[0].Item1, chain[0].Item3, 0, 0);
                    for (var j = 0; j < chain.Count; j++)
                    {
                        var vIdx = j + 1;
                        var bulge = chain[j].Item3;
                        np.AddVertexAt(vIdx, chain[j].Item2, bulge, 0, 0);
                    }

                    ts.AppendEntityToCurrentSpace(np);
                }

                result.SplitCount++;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"拆分多段线失败 (ID={poly.ObjectId}): {ex.Message}");
                DeletePoly(poly, result);
            }
        }

        /// <summary>
        ///     弧段精确拆分 — 弧段采样为弦段序列，对每条弦调用 boundary.FindLineIntersections().
        /// </summary>
        private void ProcessArcSegmentExact(
            CircularArc2d arc, double bulge, bool keepInside,
            ICropBoundary boundary, List<PolySegment> segments)
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

            // 节点：start + 交点 + end
            var nodes = new List<double> { sa };
            nodes.AddRange(angles);
            nodes.Add(ea);

            // 逐子弧段中点判断保留/删除
            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var a = nodes[i];
                var b = nodes[i + 1];
                if (Math.Abs(b - a) < 1e-9) continue;

                var midAng = (a + b) / 2.0;
                var mx = cx + r * Math.Cos(midAng);
                var my = cy + r * Math.Sin(midAng);
                // ★ 用 ICropBoundary.IsPointInside() 替代 CropGeometryService.IsPointInPolygon()
                var inside = boundary.IsPointInside(new CorePoint2D(mx, my));

                if ((keepInside && inside) || (!keepInside && !inside))
                {
                    var sPt = new Point2d(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
                    var ePt = new Point2d(cx + r * Math.Cos(b), cy + r * Math.Sin(b));
                    var subBulge = bulge * (b - a) / (ea - sa);
                    segments.Add(new PolySegment
                    {
                        Start = sPt, End = ePt, Bulge = subBulge, IsArc = true,
                    });
                }
            }
        }

        private void CollectKeptSubSegments(
            CorePoint2D start, CorePoint2D end, double bulge,
            List<CorePoint2D> intersections, bool keepInside,
            ICropBoundary boundary,
            List<PolySegment> segments, int sourceIndex)
        {
            var nodes = new List<CorePoint2D> { start };
            nodes.AddRange(intersections);
            nodes.Add(end);

            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var a = nodes[i];
                var b = nodes[i + 1];
                var d = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
                if (d < 1e-12) continue;

                var midPt = new CorePoint2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                // ★ 用 ICropBoundary.IsPointInside() 替代 CropGeometryService.IsPointInPolygon()
                var inside = boundary.IsPointInside(midPt);
                if ((keepInside && inside) || (!keepInside && !inside))
                {
                    segments.Add(new PolySegment
                    {
                        Start = new Point2d(a.X, a.Y),
                        End = new Point2d(b.X, b.Y),
                        Bulge = bulge,
                        SourceIndex = sourceIndex,
                    });
                }
            }
        }

        /// <summary>
        ///     将相邻子段合并为连续链.
        /// </summary>
        private static List<List<Tuple<Point2d, Point2d, double>>> ChainSubSegments(List<PolySegment> subs)
        {
            var chains = new List<List<Tuple<Point2d, Point2d, double>>>();
            foreach (var s in subs)
            {
                if (chains.Count == 0 || (chains[chains.Count - 1][chains[chains.Count - 1].Count - 1].Item2 - s.Start).Length > 1e-8)
                {
                    chains.Add(new List<Tuple<Point2d, Point2d, double>>());
                }
                chains[chains.Count - 1].Add(Tuple.Create(s.Start, s.End, s.Bulge));
            }
            return chains;
        }

        private static void DeletePoly(Polyline poly, CropPolylineResult result)
        {
            try
            {
                if (!poly.IsWriteEnabled) poly.UpgradeOpen();
                poly.Erase();
                result.DeletedCount++;
            }
            catch (System.Exception ex) { Logger._.Warn($"删除多段线失败: {ex.Message}"); result.SkippedCount++; }
        }
    }
}
