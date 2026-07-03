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
    public class CropLineServiceTests : CropServiceTestBase
    {
        // 1. 基本保留/删除 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
            var op = new CropLineService(Geometry).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
            var op = new CropLineService(Geometry).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
            var op = new CropLineService(Geometry).CropLinesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
            var op = new CropLineService(Geometry).CropLinesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // 2. 拆分 (3)
        [Test] public void Cross_Split() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(-50, 50, 0), new Point3d(150, 50, 0));
            var op = new CropLineService(Geometry).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SplitCount);
        });
        [Test] public void Diagonal_Split() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(-50, -50, 0), new Point3d(150, 150, 0));
            var op = new CropLineService(Geometry).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SplitCount);
        });
        [Test] public void EndpointOnBoundary() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(0, 50, 0), new Point3d(100, 50, 0));
            var op = new CropLineService(Geometry).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.KeptCount + op.Data.SplitCount, 1);
        });

        // 3. 边界/异常 (4)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = new CropLineService(Geometry).CropLinesInside((ICropBoundary)null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = new CropLineService(Geometry).CropLinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void ErasedId_Skipped() => SideDb(tr =>
        {
            var id = tr.AppendEntityToCurrentSpace(new Line(new Point3d(200, 200, 0), new Point3d(300, 300, 0)));
            tr.GetObject<Entity>(id, OpenMode.ForWrite).Erase();
            var op = new CropLineService(Geometry).CropLinesInside(Rect, new List<ObjectId> { id }, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SkippedCount);
        });
        [Test] public void ZeroLength_Skipped() => SideDb(tr =>
        {
            var ids = L(tr, new Point3d(50, 50, 0), new Point3d(50, 50, 0));
            var op = new CropLineService(Geometry).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SkippedCount + op.Data.KeptCount);
        });

        private static List<ObjectId> L(ITransactionService tr, Point3d s, Point3d e) =>
            Ids(tr, new Line(s, e));
    }
}
