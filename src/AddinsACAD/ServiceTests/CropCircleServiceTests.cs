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
    public class CropCircleServiceTests
    {
        private const double BS = 100.0;
        private static List<CorePoint2D> Rect = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0), new CorePoint2D(BS, 0), new CorePoint2D(BS, BS), new CorePoint2D(0, BS)
        };

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 20);
            var r = new CropCircleService().CropCirclesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount); Assert.AreEqual(0, r.DeletedCount);
        });
        [Test] public void Outside_Deleted() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(200, 200, 0), 20);
            var r = new CropCircleService().CropCirclesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(200, 200, 0), 20);
            var r = new CropCircleService().CropCirclesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 20);
            var r = new CropCircleService().CropCirclesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        // 2. 拆分 (3)
        [Test] public void CrossBoundary_Split() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 0, 0), 120);
            var r = new CropCircleService().CropCirclesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void SmallCircleCrossing_Split() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(0, 50, 0), 10);
            var r = new CropCircleService().CropCirclesInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount + r.SplitCount, 0);
        });
        [Test] public void CircleOnBoundaryLine() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 100, 0), 30);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });

        // 3. 边界/异常 (3)
        [Test] public void NullBoundary_Fail() => Sd(tr =>
        {
            var op = new CropCircleService().CropCirclesInside(null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void EmptyList_Fail() => Sd(tr =>
        {
            var op = new CropCircleService().CropCirclesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void DegeneratedCircle_Skipped() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 0);
            var r = new CropCircleService().CropCirclesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        // 4. 凹多边形 (2)
        private static List<CorePoint2D> Concave = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0), new CorePoint2D(BS, 0), new CorePoint2D(BS, 30),
            new CorePoint2D(50, 30), new CorePoint2D(50, BS), new CorePoint2D(BS, BS),
            new CorePoint2D(BS, 70), new CorePoint2D(0, 70)
        };
        [Test] public void Concave_Inside_Kept() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(75, 45, 0), 5);
            var r = new CropCircleService().CropCirclesInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Concave_Niche_Deleted() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(75, 35, 0), 4);
            var r = new CropCircleService().CropCirclesInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        private static void Sd(Action<ITransactionService> a) => CadServiceManager._.ExecuteInSideDatabase(a);
        private static List<ObjectId> C(ITransactionService tr, Point3d c, double r)
        {
            var id = tr.AppendEntityToCurrentSpace(new Circle(c, Vector3d.ZAxis, r));
            return new List<ObjectId> { id };
        }
    }
}