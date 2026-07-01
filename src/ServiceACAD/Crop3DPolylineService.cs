using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    public class Crop3DPolylineResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     3DPolyline 裁剪服务 — 使用 AutoCAD API 精确交点 + GetSplitCurves 拆分.
    ///     通过 IntersectWith 获取精确交点，再用 GetSplitCurves 在交点处拆分，
    ///     逐段中点判断保留/删除.
    /// </summary>
    public class Crop3DPolylineService
    {
        private readonly ICropGeometryService _geometry;
        public Crop3DPolylineService(ICropGeometryService geometry) { this._geometry = geometry ?? new CropGeometryService(); }

        public OpResult<Crop3DPolylineResult> Crop3DPolylinesInside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, true);
        public OpResult<Crop3DPolylineResult> Crop3DPolylinesOutside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, false);

        private OpResult<Crop3DPolylineResult> Crop(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts, bool keepInside)
        {
            var r = new Crop3DPolylineResult();
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased) { r.SkippedCount++; continue; }
                var e = ts.GetObject<Polyline3d>(id);
                if (e == null || e.IsErased) { r.SkippedCount++; continue; }
                this.Process3DPolyline(e, bp, keepInside, ts, r);
            }
            return OpResult<Crop3DPolylineResult>.Success(r);
        }

        private void Process3DPolyline(Polyline3d poly3d, IReadOnlyList<CorePoint2D> bpts, bool keepInside, ITransactionService ts, Crop3DPolylineResult result)
        {
            try
            {
                // 1. 包围盒快速分类
                var ext = poly3d.GeometricExtents;
                if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9) { result.SkippedCount++; return; }
                var containment = this._geometry.ClassifyBoundingBox(
                    new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y),
                    new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y), bpts);

                bool shouldDelete = keepInside
                    ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                    : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                       containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);
                if (shouldDelete) { DeleteEntity(poly3d, result); return; }
                if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }

                // 2. 构建边界多段线
                using (var boundaryCurve = BuildBoundaryPolyline2d(bpts))
                {
                    if (boundaryCurve == null) { result.SkippedCount++; return; }

                    // 3. 精确求交
                    var intersectPts = new Point3dCollection();
                    try
                    {
                        poly3d.IntersectWith(boundaryCurve, Intersect.OnBothOperands, intersectPts, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch
                    {
                        // 求交失败，回退到中点判断
                        var midPt = poly3d.GetPointAtParameter((poly3d.StartParam + poly3d.EndParam) / 2.0);
                        var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                        if ((keepInside && inside) || (!keepInside && !inside))
                            result.KeptCount++;
                        else
                            DeleteEntity(poly3d, result);
                        return;
                    }

                    // 4. 无交点 — 中点判断全保留或全删除
                    if (intersectPts.Count == 0)
                    {
                        var midPt = poly3d.GetPointAtParameter((poly3d.StartParam + poly3d.EndParam) / 2.0);
                        var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                        if ((keepInside && inside) || (!keepInside && !inside))
                            result.KeptCount++;
                        else
                            DeleteEntity(poly3d, result);
                        return;
                    }

                    // 5. 在交点处拆分
                    DBObjectCollection splitCurves = null;
                    try
                    {
                        splitCurves = poly3d.GetSplitCurves(intersectPts);
                    }
                    catch
                    {
                        DeleteEntity(poly3d, result);
                        return;
                    }

                    if (splitCurves == null || splitCurves.Count == 0)
                    {
                        DeleteEntity(poly3d, result);
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
                        DeleteEntity(poly3d, result);
                    }
                    else
                    {
                        if (!poly3d.IsWriteEnabled) poly3d.UpgradeOpen();
                        poly3d.Erase();
                        foreach (var seg in keptCurves)
                        {
                            seg.Layer = poly3d.Layer;
                            seg.Color = poly3d.Color;
                            seg.Linetype = poly3d.Linetype;
                            ts.AppendEntityToCurrentSpace(seg);
                        }
                        result.DeletedCount++;
                        result.SplitCount++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"裁剪 3DPolyline 失败 (ID={poly3d.ObjectId}): {ex.Message}");
                DeleteEntity(poly3d, result);
            }
        }

        /// <summary>
        ///     将边界多边形顶点列表构建为闭合 Polyline.
        /// </summary>
        private static Polyline BuildBoundaryPolyline2d(IReadOnlyList<CorePoint2D> bpts)
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

        private static void DeleteEntity(Entity entity, Crop3DPolylineResult result)
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