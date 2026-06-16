using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    public class CropPolylineServiceTests : CropServiceTestBase
    {
        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = R(tr, 20, 20, 80, 80);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = R(tr, 200, 200, 250, 250);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = R(tr, 200, 200, 250, 250);
            var op = new CropPolylineService().CropPolylinesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = R(tr, 20, 20, 80, 80);
            var op = new CropPolylineService().CropPolylinesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // 2. 拆分 (3)
        [Test] public void Cross_Split() => SideDb(tr =>
        {
            var ids = R(tr, -50, 25, 150, 75);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SplitCount);
        });
        [Test] public void OpenPolyline_Cross_Split() => SideDb(tr =>
        {
            var ids = O(tr, new Point2d(-20, 50), new Point2d(50, 50), new Point2d(120, 50));
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SplitCount);
        });
        [Test] public void StraightPolyline_CrossBoundary() => SideDb(tr =>
        {
            var ids = R(tr, -20, 50, 120, 60);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = new CropPolylineService().CropPolylinesInside(null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = new CropPolylineService().CropPolylinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void SingleVertexPoly_Skipped() => SideDb(tr =>
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(50, 50), 0, 0, 0);
            var ids = Ids(tr, p);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SkippedCount + op.Data.DeletedCount + op.Data.KeptCount);
        });

        private static List<ObjectId> R(ITransactionService tr, double x1, double y1, double x2, double y2)
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            p.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            p.Closed = true;
            return Ids(tr, p);
        }
        private static List<ObjectId> O(ITransactionService tr, params Point2d[] pts)
        {
            var p = new Polyline();
            for (var i = 0; i < pts.Length; i++)
                p.AddVertexAt(i, pts[i], 0, 0, 0);
            return Ids(tr, p);
        }
    }
}
