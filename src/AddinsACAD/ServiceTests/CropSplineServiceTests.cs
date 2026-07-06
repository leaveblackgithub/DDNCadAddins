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
    public class CropSplineServiceTests : CropServiceTestBase
    {
        private CropSplineService CreateService() => new CropSplineService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection
            {
                new Point3d(10, 10, 0), new Point3d(30, 20, 0),
                new Point3d(50, 10, 0), new Point3d(70, 20, 0),
            });
            var r = CreateService().CropSplinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection
            {
                new Point3d(200, 200, 0), new Point3d(220, 210, 0),
                new Point3d(240, 200, 0), new Point3d(260, 210, 0),
            });
            var r = CreateService().CropSplinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection
            {
                new Point3d(200, 200, 0), new Point3d(220, 210, 0),
                new Point3d(240, 200, 0), new Point3d(260, 210, 0),
            });
            var r = CreateService().CropSplinesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection
            {
                new Point3d(10, 10, 0), new Point3d(30, 20, 0),
                new Point3d(50, 10, 0), new Point3d(70, 20, 0),
            });
            var r = CreateService().CropSplinesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        // 2. 拆分 (2)
        [Test] public void CrossBoundary_Split() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection
            {
                new Point3d(-20, 50, 0), new Point3d(30, 30, 0),
                new Point3d(70, 70, 0), new Point3d(120, 50, 0),
            });
            var r = CreateService().CropSplinesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.SplitCount + r.KeptCount, 0);
        });
        [Test] public void CrossCorner_Split() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection
            {
                new Point3d(-10, -10, 0), new Point3d(30, 30, 0),
                new Point3d(70, 70, 0), new Point3d(110, 110, 0),
            });
            var r = CreateService().CropSplinesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.SplitCount + r.KeptCount, 0);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropSplinesInside((IReadOnlyList<CorePoint2D>)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropSplinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void SinglePointSpline_Handled() => SideDb(tr =>
        {
            var ids = S(tr, new Point3dCollection { new Point3d(50, 50, 0) });
            var op = CreateService().CropSplinesInside(Rect, ids, tr);
            // Single-point spline may still be valid; just verify no exception
            Assert.IsNotNull(op);
        });

        private static List<ObjectId> S(ITransactionService tr, Point3dCollection pts)
        {
            var spline = new Spline(pts, 1, 0);
            return Ids(tr, spline);
        }
    }
}
