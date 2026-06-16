using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    public class CropHatchResult
    {
        public int DeletedCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>Hatch 裁剪 placeholder — 边界框分类 + 保留/删除.</summary>
    public class CropHatchService
    {
        private readonly ICropGeometryService _geometry;
        public CropHatchService(ICropGeometryService geometry) { this._geometry = geometry ?? new CropGeometryService(); }

        public OpResult<CropHatchResult> CropHatchesInside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, true);
        public OpResult<CropHatchResult> CropHatchesOutside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, false);

        private OpResult<CropHatchResult> Crop(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts, bool keepInside)
        {
            var r = new CropHatchResult();
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased) { r.SkippedCount++; continue; }
                var e = ts.GetObject<Hatch>(id);
                if (e == null || e.IsErased) { r.SkippedCount++; continue; }
                var cr = CropUtils.ProcessNonCurve(e, bp, keepInside, this._geometry);
                r.DeletedCount += cr.DeletedCount; r.KeptCount += cr.KeptCount;
            }
            return OpResult<CropHatchResult>.Success(r);
        }
    }
}