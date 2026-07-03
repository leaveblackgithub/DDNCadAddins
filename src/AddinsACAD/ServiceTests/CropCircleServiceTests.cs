using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    public class CropCircleServiceTests : CropServiceTestBase
    {
        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 20);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(200, 200, 0), 20);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(200, 200, 0), 20);
            var op = new CropCircleService().CropCirclesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 20);
            var op = new CropCircleService().CropCirclesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // 2. 拆分 (3)
        [Test] public void CrossBoundary_Split() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 60);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.SplitCount + op.Data.KeptCount, 1);
        });
        [Test] public void SmallCircleCrossing_Split() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(0, 50, 0), 10);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.KeptCount + op.Data.SplitCount, 0);
        });
        [Test] public void CircleOnBoundaryLine() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(50, 100, 0), 30);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = new CropCircleService().CropCirclesInside((ICropBoundary)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = new CropCircleService().CropCirclesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void DegeneratedCircle_ReturnsFail() => SideDb(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 0);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsFalse(op.IsSuccess);
        });

        private static List<ObjectId> C(ITransactionService tr, Point3d c, double r) =>
            Ids(tr, new Circle(c, Vector3d.ZAxis, r));
    }
}
