using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    public class CropSplineResult
    {
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     Spline 裁剪服务 — 使用 AutoCAD API 精确交点 + GetSplitCurves 拆分.
    ///     通过 IntersectWith 获取精确交点，再用 GetSplitCurves 在交点处拆分，
    ///     逐段中点判断保留/删除。边界 Polyline 与 Spline 在同一平面（使用 Spline 的法线）.
    /// </summary>
    public class CropSplineService
    {
        private readonly ICropGeometryService _geometry;
        public CropSplineService(ICropGeometryService geometry) { this._geometry = geometry ?? new CropGeometryService(); }

        public OpResult<CropSplineResult> CropSplinesInside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, true);
        public OpResult<CropSplineResult> CropSplinesOutside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, false);

        private OpResult<CropSplineResult> Crop(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts, bool keepInside)
        {
            var r = new CropSplineResult();
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased) { r.SkippedCount++; continue; }
                var e = ts.GetObject<Spline>(id);
                if (e == null || e.IsErased) { r.SkippedCount++; continue; }
                this.ProcessSpline(e, bp, keepInside, ts, r);
            }
            return OpResult<CropSplineResult>.Success(r);
        }

        private void ProcessSpline(Spline spline, IReadOnlyList<CorePoint2D> bpts, bool keepInside, ITransactionService ts, CropSplineResult result)
        {
            try
            {
                // 1. 包围盒快速分类
                var ext = spline.GeometricExtents;
                if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9) { result.SkippedCount++; return; }
                var containment = this._geometry.ClassifyBoundingBox(
                    new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y),
                    new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y), bpts);

                bool shouldDelete = keepInside
                    ? containment == DDNCadAddins.Core.Models.ContainmentResult.Outside
                    : (containment == DDNCadAddins.Core.Models.ContainmentResult.Inside ||
                       containment == DDNCadAddins.Core.Models.ContainmentResult.OnBoundary);
                if (shouldDelete) { DeleteEntity(spline, result); return; }
                if (containment != DDNCadAddins.Core.Models.ContainmentResult.Intersects) { result.KeptCount++; return; }

                // 2. 构建边界多段线用于求交
                using (var boundaryCurve = BuildBoundaryPolyline(bpts))
                {
                    if (boundaryCurve == null) { result.SkippedCount++; return; }

                    // 3. 精确求交
                    var intersectPts = new Point3dCollection();
                    try
                    {
                        spline.IntersectWith(boundaryCurve, Intersect.OnBothOperands, intersectPts, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch
                    {
                        // 求交失败，回退到中点判断 + 删除
                        var midPt = spline.GetPointAtParameter((spline.StartParam + spline.EndParam) / 2.0);
                        var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                        if ((keepInside && inside) || (!keepInside && !inside))
                            result.KeptCount++;
                        else
                            DeleteEntity(spline, result);
                        return;
                    }

                    // 4. 无交点 — 中点判断全保留或全删除
                    if (intersectPts.Count == 0)
                    {
                        var midPt = spline.GetPointAtParameter((spline.StartParam + spline.EndParam) / 2.0);
                        var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                        if ((keepInside && inside) || (!keepInside && !inside))
                            result.KeptCount++;
                        else
                            DeleteEntity(spline, result);
                        return;
                    }

                    // 5. 在交点处拆分
                    DBObjectCollection splitCurves = null;
                    try
                    {
                        splitCurves = spline.GetSplitCurves(intersectPts);
                    }
                    catch
                    {
                        DeleteEntity(spline, result);
                        return;
                    }

                    if (splitCurves == null || splitCurves.Count == 0)
                    {
                        DeleteEntity(spline, result);
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
                        DeleteEntity(spline, result);
                    }
                    else
                    {
                        if (!spline.IsWriteEnabled) spline.UpgradeOpen();
                        spline.Erase();
                        foreach (var seg in keptCurves)
                        {
                            seg.Layer = spline.Layer;
                            seg.Color = spline.Color;
                            seg.Linetype = spline.Linetype;
                            ts.AppendEntityToCurrentSpace(seg);
                        }
                        result.DeletedCount++;
                        result.SplitCount++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"裁剪 Spline 失败 (ID={spline.ObjectId}): {ex.Message}");
                DeleteEntity(spline, result);
            }
        }

        /// <summary>
        ///     将边界多边形顶点列表构建为闭合 Polyline.
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

        private static void DeleteEntity(Entity entity, CropSplineResult result)
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