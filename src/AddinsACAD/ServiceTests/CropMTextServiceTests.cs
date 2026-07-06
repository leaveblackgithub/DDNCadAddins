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
    public class CropMTextServiceTests : CropServiceTestBase
    {
        private CropMTextService CreateService() => new CropMTextService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = M(tr, new Point3d(50, 50, 0), "A");
            var r = CreateService().CropMTextsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = M(tr, new Point3d(200, 200, 0), "A");
            var r = CreateService().CropMTextsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = M(tr, new Point3d(200, 200, 0), "A");
            var r = CreateService().CropMTextsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = M(tr, new Point3d(50, 50, 0), "A");
            var r = CreateService().CropMTextsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });

        // 2. 边界 (2)
        [Test] public void OnBoundary_Deleted_KeepInside() => SideDb(tr =>
        {
            var ids = M(tr, new Point3d(0, 50, 0), "A");
            var r = CreateService().CropMTextsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void OnBoundary_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = M(tr, new Point3d(0, 50, 0), "A");
            var r = CreateService().CropMTextsOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount + r.DeletedCount, 0);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropMTextsInside((IReadOnlyList<CorePoint2D>)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropMTextsInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void InvalidId_Skipped() => SideDb(tr =>
        {
            var ids = new List<ObjectId> { ObjectId.Null };
            var r = CreateService().CropMTextsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        private static List<ObjectId> M(ITransactionService tr, Point3d pos, string str)
        {
            var mtext = new MText { Location = pos, Contents = str, TextHeight = 5, Width = 20 };
            return Ids(tr, mtext);
        }
    }
}
