using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    public class CropTextResult
    {
        public int DeletedCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>DBText 裁剪 placeholder — 边界框分类 + 保留/删除.</summary>
    public class CropTextService
    {
        private readonly ICropGeometryService _geometry;
        public CropTextService(ICropGeometryService geometry) { this._geometry = geometry ?? new CropGeometryService(); }

        public OpResult<CropTextResult> CropTextsInside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, true);
        public OpResult<CropTextResult> CropTextsOutside(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts) => this.Crop(bp, ids, ts, false);

        private OpResult<CropTextResult> Crop(IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts, bool keepInside)
        {
            var r = new CropTextResult();
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased) { r.SkippedCount++; continue; }
                var e = ts.GetObject<DBText>(id);
                if (e == null || e.IsErased) { r.SkippedCount++; continue; }
                var cr = CropUtils.ProcessNonCurve(e, bp, keepInside, this._geometry);
                r.DeletedCount += cr.DeletedCount; r.KeptCount += cr.KeptCount;
            }
            return OpResult<CropTextResult>.Success(r);
        }
    }
}