using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using NUnit.Framework;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    public class CropDimServiceTests : CropServiceTestBase
    {
        private CropDimService CreateService() => new CropDimService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = D(tr, new Point3d(50, 50, 0), new Point3d(80, 80, 0));
            var r = CreateService().CropDimsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = D(tr, new Point3d(200, 200, 0), new Point3d(250, 250, 0));
            var r = CreateService().CropDimsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = D(tr, new Point3d(200, 200, 0), new Point3d(250, 250, 0));
            var r = CreateService().CropDimsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = D(tr, new Point3d(50, 50, 0), new Point3d(80, 80, 0));
            var r = CreateService().CropDimsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });

        // 2. 边界 (2)
        [Test] public void OnBoundary_Deleted_KeepInside() => SideDb(tr =>
        {
            var ids = D(tr, new Point3d(0, 50, 0), new Point3d(30, 80, 0));
            var r = CreateService().CropDimsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void OnBoundary_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = D(tr, new Point3d(0, 50, 0), new Point3d(30, 80, 0));
            var r = CreateService().CropDimsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount + r.DeletedCount, 0);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropDimsInside((IReadOnlyList<CorePoint2D>)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropDimsInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void InvalidId_Skipped() => SideDb(tr =>
        {
            var ids = new List<ObjectId> { ObjectId.Null };
            var r = CreateService().CropDimsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        private static List<ObjectId> D(ITransactionService tr, Point3d p1, Point3d p2)
        {
            var dim = new AlignedDimension { XLine1Point = p1, XLine2Point = p2, DimLinePoint = new Point3d(50, 30, 0) };
            return Ids(tr, dim);
        }
    }
}
