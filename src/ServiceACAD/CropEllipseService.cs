using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Models;
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
    ///     Ellipse 裁剪服务 — 精确参数搜索 + GetSplitCurves 拆分.
    ///     不依赖 IntersectWith（避免 3D 平面问题），而是通过参数二分搜索
    ///     找到曲线穿越边界的参数点，然后用 GetSplitCurves 精确拆分.
    /// </summary>
    public class CropEllipseService
    {
        private readonly ICropGeometryService _geometry;
        private const int SampleCount = 200;
        private const double ParamTolerance = 1e-6;

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
                    ? containment == ContainmentResult.Outside
                    : (containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary);
                if (shouldDelete) { DeleteEntity(ellipse, result); return; }
                if (containment != ContainmentResult.Intersects) { result.KeptCount++; return; }

                // 2. 通过参数采样找到曲线穿越边界的参数点
                var splitParams = FindBoundaryCrossingParams(ellipse, bpts);

                // 3. 无交点 — 中点判断全保留或全删除
                if (splitParams.Count == 0)
                {
                    var midPt = ellipse.GetPointAtParameter((ellipse.StartParam + ellipse.EndParam) / 2.0);
                    var inside = this._geometry.IsPointInPolygon(new CorePoint2D(midPt.X, midPt.Y), bpts);
                    if ((keepInside && inside) || (!keepInside && !inside))
                        result.KeptCount++;
                    else
                        DeleteEntity(ellipse, result);
                    return;
                }

                // 4. 在交点处拆分
                var splitPts = new Point3dCollection();
                foreach (var p in splitParams)
                {
                    splitPts.Add(ellipse.GetPointAtParameter(p));
                }

                DBObjectCollection splitCurves = null;
                try
                {
                    splitCurves = ellipse.GetSplitCurves(splitPts);
                }
                catch (System.Exception ex)
                {
                    Logger._.Warn($"GetSplitCurves 失败 (Ellipse): {ex.Message}");
                    DeleteEntity(ellipse, result);
                    return;
                }

                if (splitCurves == null || splitCurves.Count == 0)
                {
                    DeleteEntity(ellipse, result);
                    return;
                }

                // 5. 逐段中点判断
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

                // 6. 替换原实体
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
            catch (System.Exception ex)
            {
                Logger._.Warn($"裁剪 Ellipse 失败 (ID={ellipse.ObjectId}): {ex.Message}");
                DeleteEntity(ellipse, result);
            }
        }

        private List<double> FindBoundaryCrossingParams(Curve curve, IReadOnlyList<CorePoint2D> bpts)
        {
            var result = new List<double>();
            var startParam = curve.StartParam;
            var endParam = curve.EndParam;

            var prevInside = (bool?)null;
            var prevParam = startParam;

            for (int i = 0; i <= SampleCount; i++)
            {
                var t = startParam + (endParam - startParam) * i / SampleCount;
                Point3d pt;
                try { pt = curve.GetPointAtParameter(t); }
                catch { continue; }

                var inside = this._geometry.IsPointInPolygon(new CorePoint2D(pt.X, pt.Y), bpts);

                if (prevInside.HasValue && prevInside.Value != inside)
                {
                    var crossingParam = BinarySearchCrossing(curve, prevParam, t, prevInside.Value, bpts);
                    if (crossingParam.HasValue)
                        result.Add(crossingParam.Value);
                }

                prevInside = inside;
                prevParam = t;
            }

            return result;
        }

        private double? BinarySearchCrossing(Curve curve, double p1, double p2, bool insideAtP1, IReadOnlyList<CorePoint2D> bpts)
        {
            var lo = p1;
            var hi = p2;
            var insideAtLo = insideAtP1;

            for (int iter = 0; iter < 30; iter++)
            {
                var mid = (lo + hi) / 2.0;
                Point3d pt;
                try { pt = curve.GetPointAtParameter(mid); }
                catch { return null; }

                var midInside = this._geometry.IsPointInPolygon(new CorePoint2D(pt.X, pt.Y), bpts);

                if (midInside == insideAtLo)
                    lo = mid;
                else
                    hi = mid;

                if (Math.Abs(hi - lo) < ParamTolerance)
                    break;
            }

            return (lo + hi) / 2.0;
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