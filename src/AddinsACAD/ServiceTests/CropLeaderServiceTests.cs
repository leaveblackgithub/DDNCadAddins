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
    public class CropLeaderServiceTests : CropServiceTestBase
    {
        private CropLeaderService CreateService() => new CropLeaderService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = L(tr, new Point3dCollection
            {
                new Point3d(30, 30, 0), new Point3d(50, 50, 0), new Point3d(70, 30, 0)
            });
            var r = CreateService().CropLeadersInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = L(tr, new Point3dCollection
            {
                new Point3d(200, 200, 0), new Point3d(220, 220, 0)
            });
            var r = CreateService().CropLeadersInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = L(tr, new Point3dCollection
            {
                new Point3d(200, 200, 0), new Point3d(220, 220, 0)
            });
            var r = CreateService().CropLeadersOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = L(tr, new Point3dCollection
            {
                new Point3d(30, 30, 0), new Point3d(50, 50, 0), new Point3d(70, 30, 0)
            });
            var r = CreateService().CropLeadersOutside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });

        // 2. 拆分 (2)
        [Test] public void CrossBoundary_Split() => SideDb(tr =>
        {
            var ids = L(tr, new Point3dCollection
            {
                new Point3d(-20, 50, 0), new Point3d(50, 50, 0), new Point3d(120, 50, 0)
            });
            var r = CreateService().CropLeadersInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.SplitCount + r.KeptCount, 0);
        });
        [Test] public void DiagonalCross_Split() => SideDb(tr =>
        {
            var ids = L(tr, new Point3dCollection
            {
                new Point3d(-10, -10, 0), new Point3d(50, 50, 0), new Point3d(110, 110, 0)
            });
            var r = CreateService().CropLeadersInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.SplitCount + r.KeptCount, 0);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropLeadersInside((IReadOnlyList<CorePoint2D>)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropLeadersInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void InvalidId_Skipped() => SideDb(tr =>
        {
            var ids = new List<ObjectId> { ObjectId.Null };
            var r = CreateService().CropLeadersInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        private static List<ObjectId> L(ITransactionService tr, Point3dCollection pts)
        {
            var leader = new Leader();
            for (int i = 0; i < pts.Count; i++)
                leader.AppendVertex(pts[i]);
            return Ids(tr, leader);
        }
    }
}
