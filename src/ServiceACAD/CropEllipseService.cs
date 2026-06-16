using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
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

    /// <summary>Ellipse 裁剪 placeholder — 采样 + 中点分类.</summary>
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
                var cr = CropUtils.ProcessCurve(e, bp, keepInside, ts, this._geometry);
                r.DeletedCount += cr.DeletedCount; r.SplitCount += cr.SplitCount; r.KeptCount += cr.KeptCount;
            }
            return OpResult<CropEllipseResult>.Success(r);
        }
    }
}