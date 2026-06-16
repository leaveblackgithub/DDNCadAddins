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
        private static List<CorePoint2D> Concave = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0), new CorePoint2D(BS, 0), new CorePoint2D(BS, 30),
            new CorePoint2D(50, 30), new CorePoint2D(50, BS), new CorePoint2D(BS, BS),
            new CorePoint2D(BS, 70), new CorePoint2D(0, 70)
        };

        // 1. 基本保留/删除 (4)
        [Test] public void Inside_Kept() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount); Assert.AreEqual(0, r.DeletedCount);
        });
        [Test] public void Outside_Deleted() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount); Assert.AreEqual(0, r.KeptCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount); Assert.AreEqual(0, r.DeletedCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount); Assert.AreEqual(0, r.KeptCount);
        });

        // 2. 拆分 (3)
        [Test] public void Cross_Split() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(-50, 50, 0), new Point3d(150, 50, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void Diagonal_Split() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(-50, -50, 0), new Point3d(150, 150, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void EndpointOnBoundary() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(0, 10, 0), new Point3d(100, 10, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount + r.SplitCount, 1);
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
        [Test] public void ErasedId_Skipped()
        {
            var ids = new List<ObjectId>();
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                ids.Add(tr.AppendEntityToCurrentSpace(new Line(new Point3d(200, 200, 0), new Point3d(300, 300, 0))));
            });
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ent = tr.GetObject<Entity>(ids[0], OpenMode.ForWrite);
                ent.Erase();
            });
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
                Assert.AreEqual(1, r.SkippedCount);
            });
        }
        [Test] public void ZeroLength_Skipped() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(50, 50, 0), new Point3d(50, 50, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        // 4. 凹多边形 (2)
        [Test] public void Concave_Inside_Kept() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(75, 45, 0), new Point3d(75, 55, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Concave_Outside_Deleted() => Sd(tr =>
        {
            var ids = L(tr, new Point3d(75, 35, 0), new Point3d(75, 40, 0));
            var r = new CropLineService(new CropGeometryService()).CropLinesInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        private static void Sd(Action<ITransactionService> a) => CadServiceManager._.ExecuteInSideDatabase(a);
        private static List<ObjectId> L(ITransactionService tr, Point3d s, Point3d e)
        {
            var id = tr.AppendEntityToCurrentSpace(new Line(s, e));
            return new List<ObjectId> { id };
        }
    }
}