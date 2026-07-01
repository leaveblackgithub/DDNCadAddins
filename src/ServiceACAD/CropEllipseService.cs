using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    public class CropEllipseResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     Ellipse 裁剪服务 — 使用 AutoCAD API 精确交点 + GetSplitCurves 拆分.
    ///     不再使用采样法，而是通过 IntersectWith 获取精确交点，
    ///     再用 GetSplitCurves 在交点处拆分，逐段中点判断保留/删除.
    /// </summary>
    public class CropEllipseService
    {
        private readonly ICropGeometryService _geometry;
        public CropEllipseService(ICropGeometryService geometry) { this._geometry = geometry ?? new CropGeometryService(); }

        public OpResult<CropEllipseResult> CropEllipsesInside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, true);
        public OpResult<CropEllipseResult> CropEllipsesOutside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, false);

        private OpResult<CropEllipseResult> Crop(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts, bool keepInside)
        {
            var r = new CropEllipseResult();
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased) { r.SkippedCount++; continue; }
                var e = ts.GetObject<Ellipse>(id);
                if (e == null || e.IsErased) { r.SkippedCount++; continue; }
                this.ProcessEllipse(e, bp, keepInside, ts, r);
            }
            return OpResult<CropEllipseResult>.Success(r);
        }

        private void ProcessEllipse(Ellipse ellipse, IReadOnlyList<CorePoint2D> bpts, bool keepInside, ITransactionService ts, CropEllipseResult result)
        {
            try
            {
                // 1. 包围盒快速分类
                var ext = ellipse.GeometricExtents;
                if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9) { result.SkippedCount++; return; }
                var containment = this._geometry.ClassifyBoundingBox(
                    new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y),
                    new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y), bpts);

                bool shouldDelete = keepInside
                    ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                    : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                       containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);
                if (shouldDelete) { DeleteEntity(ellipse, result); return; }
                if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }

                // 2. 构建边界多段线用于求交
                using (var boundaryCurve = BuildBoundaryPolyline(bpts))
                {
                    if (boundaryCurve == null) { result.SkippedCount++; return; }

                    // 3. 精确求交
                    var intersectPts = new Point3dCollection();
                    try
                    {
                        ellipse.IntersectWith(boundaryCurve, Intersect.OnBothOperands, intersectPts, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch
                    {
                        DeleteEntity(ellipse, result);
                        return;
                    }

                    // 4. 无交点 — 中点判断全保留或全删除
                    if (intersectPts.Count == 0)
                    {
                        var midPt = ellipse.GetPointAtParameter((ellipse.StartParam + ellipse.EndParam) / 2.0);
                        var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                        if ((keepInside && inside) || (!keepInside && !inside))
                            result.KeptCount++;
                        else
                            DeleteEntity(ellipse, result);
                        return;
                    }

                    // 5. 在交点处拆分
                    DBObjectCollection splitCurves = null;
                    try
                    {
                        splitCurves = ellipse.GetSplitCurves(intersectPts);
                    }
                    catch
                    {
                        DeleteEntity(ellipse, result);
                        return;
                    }

                    if (splitCurves == null || splitCurves.Count == 0)
                    {
                        DeleteEntity(ellipse, result);
                        return;
                    }

                    // 6. 逐段中点判断
                    var keptCurves = new List<Curve>();
                    foreach (DBObject obj in splitCurves)
                    {
                        if (!(obj is Curve seg)) { obj.Dispose(); continue; }
                        var midParam = (seg.StartParam + seg.EndParam) / 2.0;
                        Point3d midPt;
                        try { midPt = seg.GetPointAtParameter(midParam); }
                        catch { seg.Dispose(); continue; }

                        var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                        if ((keepInside && inside) || (!keepInside && !inside))
                            keptCurves.Add(seg);
                        else
                            seg.Dispose();
                    }

                    // 7. 替换原实体
                    if (keptCurves.Count == 0)
                    {
                        DeleteEntity(ellipse, result);
                    }
                    else
                    {
                        if (!ellipse.IsWriteEnabled) ellipse.UpgradeOpen();
                        ellipse.Erase();
                        foreach (var seg in keptCurves)
                        {
                            seg.Layer = ellipse.Layer;
                            seg.Color = ellipse.Color;
                            seg.Linetype = ellipse.Linetype;
                            ts.AppendEntityToCurrentSpace(seg);
                        }
                        result.DeletedCount++;
                        result.SplitCount++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"裁剪 Ellipse 失败 (ID={ellipse.ObjectId}): {ex.Message}");
                DeleteEntity(ellipse, result);
            }
        }

        /// <summary>
        ///     将边界多边形顶点列表构建为闭合 Polyline（用于 IntersectWith）.
        /// </summary>
        private static Polyline BuildBoundaryPolyline(IReadOnlyList<CorePoint2D> bpts)
        {
            if (bpts == null || bpts.Count < 3) return null;
            var pl = new Polyline(bpts.Count);
            for (int i = 0; i < bpts.Count; i++)
            {
                pl.AddVertexAt(i, new Point2d(bpts[i].X, bpts[i].Y), 0, 0, 0);
            }
            pl.Closed = true;
            return pl;
        }

        private static void DeleteEntity(Entity entity, CropEllipseResult result)
        {
            try
            {
                if (!entity.IsWriteEnabled) entity.UpgradeOpen();
                entity.Erase();
                result.DeletedCount++;
            }
            catch { result.SkippedCount++; }
        }
    }
}