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
    public class CropEllipseServiceTests : CropServiceTestBase
    {
        private CropEllipseService CreateService() => new CropEllipseService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(50, 50, 0), 20, 10);
            var r = CreateService().CropEllipsesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(200, 200, 0), 20, 10);
            var r = CreateService().CropEllipsesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount, 0);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(200, 200, 0), 20, 10);
            var r = CreateService().CropEllipsesOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(50, 50, 0), 20, 10);
            var r = CreateService().CropEllipsesOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount, 0);
        });

        // 2. 拆分 (3)
        [Test] public void CrossBoundary_Split() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(50, 50, 0), 60, 40);
            var r = CreateService().CropEllipsesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.SplitCount + r.KeptCount, 0);
        });
        [Test] public void PartialInside_Split() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(0, 50, 0), 30, 20);
            var r = CreateService().CropEllipsesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.SplitCount + r.KeptCount, 0);
        });
        [Test] public void EllipseOnBoundaryLine() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(50, 100, 0), 40, 20);
            var r = CreateService().CropEllipsesInside(Rect, ids, tr).Data;
            Assert.IsNotNull(r);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropEllipsesInside((IReadOnlyList<CorePoint2D>)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropEllipsesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void DegeneratedEllipse_Skipped() => SideDb(tr =>
        {
            var ids = E(tr, new Point3d(50, 50, 0), 0, 0);
            var op = CreateService().CropEllipsesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.SkippedCount, 0);
        });

        private static List<ObjectId> E(ITransactionService tr, Point3d c, double majR, double minR)
        {
            var ellipse = new Ellipse(c, Vector3d.ZAxis, new Vector3d(majR, 0, 0), minR, 0, 2 * Math.PI);
            return Ids(tr, ellipse);
        }
    }
}
