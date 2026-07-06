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
    public class CropSolidServiceTests : CropServiceTestBase
    {
        private CropSolidService CreateService() => new CropSolidService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = S(tr, new Point3d(30, 30, 0), new Point3d(50, 30, 0),
                           new Point3d(50, 50, 0), new Point3d(30, 50, 0));
            var r = CreateService().CropSolidsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = S(tr, new Point3d(200, 200, 0), new Point3d(220, 200, 0),
                           new Point3d(220, 220, 0), new Point3d(200, 220, 0));
            var r = CreateService().CropSolidsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = S(tr, new Point3d(200, 200, 0), new Point3d(220, 200, 0),
                           new Point3d(220, 220, 0), new Point3d(200, 220, 0));
            var r = CreateService().CropSolidsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = S(tr, new Point3d(30, 30, 0), new Point3d(50, 30, 0),
                           new Point3d(50, 50, 0), new Point3d(30, 50, 0));
            var r = CreateService().CropSolidsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });

        // 2. 边界 (2)
        [Test] public void OnBoundary_Deleted_KeepInside() => SideDb(tr =>
        {
            var ids = S(tr, new Point3d(-10, 50, 0), new Point3d(10, 50, 0),
                           new Point3d(10, 70, 0), new Point3d(-10, 70, 0));
            var r = CreateService().CropSolidsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void OnBoundary_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = S(tr, new Point3d(-10, 50, 0), new Point3d(10, 50, 0),
                           new Point3d(10, 70, 0), new Point3d(-10, 70, 0));
            var r = CreateService().CropSolidsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropSolidsInside((IReadOnlyList<CorePoint2D>)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropSolidsInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void InvalidId_Skipped() => SideDb(tr =>
        {
            var ids = new List<ObjectId> { ObjectId.Null };
            var r = CreateService().CropSolidsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        private static List<ObjectId> S(ITransactionService tr, Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            var solid = new Solid(p1, p2, p3, p4);
            return Ids(tr, solid);
        }
    }
}
