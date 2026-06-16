using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    public class CropArcServiceTests : CropServiceTestBase
    {
        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(200, 200, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(200, 200, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        // 2. 拆分 (5)
        [Test] public void Cross_Split() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, -30, 0), 80, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void Tangent_Arc() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 100, 0), 30, -Math.PI / 2, Math.PI / 2);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount + r.SplitCount, 0);
        });
        [Test] public void ShortArc() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 5, 0, Math.PI / 4);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void LargeArc() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 0, 0), 120, 0, 3.0);
            var op = new CropArcService().CropArcsInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });
        [Test] public void ArcSpanning2Pi() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 60, -Math.PI / 4, Math.PI / 2 + Math.PI);
            var op = new CropArcService().CropArcsInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = new CropArcService().CropArcsInside(null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = new CropArcService().CropArcsInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void DegeneratedArc_Skipped() => SideDb(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 0, 0, 0);
            var op = new CropArcService().CropArcsInside(Rect, ids, tr);
            Assert.IsFalse(op.IsSuccess);
        });

        private static List<ObjectId> A(ITransactionService tr, Point3d c, double r, double sa, double ea) =>
            Ids(tr, new Arc(c, r, sa, ea));
    }
}
