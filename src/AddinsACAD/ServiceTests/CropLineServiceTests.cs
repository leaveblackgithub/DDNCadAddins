using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Services;
using NUnit.Framework;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CropLineServiceTests
    {
        private const double BS = 100.0;
        private static List<CorePoint2D> Rect = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0), new CorePoint2D(BS, 0), new CorePoint2D(BS, BS), new CorePoint2D(0, BS)
        };

        // 1. 基本保留/删除 (4)
        [Test] public void Inside_Kept() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Outside_Deleted() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // 2. 拆分 (3)
        [Test] public void Cross_Split() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(-50, 50, 0), new Point3d(150, 50, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SplitCount);
        });
        [Test] public void Diagonal_Split() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(-50, -50, 0), new Point3d(150, 150, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SplitCount);
        });
        [Test] public void EndpointOnBoundary() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(0, 50, 0), new Point3d(100, 50, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.KeptCount + op.Data.SplitCount, 1);
        });

        // 3. 边界/异常 (4)
        [Test] public void NullBoundary_Fail() => Sd(tr =>
        {
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void EmptyList_Fail() => Sd(tr =>
        {
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void ErasedId_Skipped() => Sd(tr =>
        {
            var id = tr.AppendEntityToCurrentSpace(new Line(new Point3d(200, 200, 0), new Point3d(300, 300, 0)));
            tr.GetObject<Entity>(id, OpenMode.ForWrite).Erase();
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, new List<ObjectId> { id }, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SkippedCount);
        });
        [Test] public void ZeroLength_Skipped() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(50, 50, 0), new Point3d(50, 50, 0));
            var op = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.SkippedCount + op.Data.KeptCount);
        });

        private static void Sd(Action<ITransactionService> a) => CadServiceManager._.ExecuteInSideDatabase(a);
        private static List<ObjectId> L(ITransactionService tr, Point3d s, Point3d e)
        {
            var id = tr.AppendEntityToCurrentSpace(new Line(s, e));
            return new List<ObjectId> { id };
        }
    }
}