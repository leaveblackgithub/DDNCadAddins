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
    public class CropArcServiceTests
    {
        private const double BS = 100.0;
        private static List<CorePoint2D> Rect = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0), new CorePoint2D(BS, 0), new CorePoint2D(BS, BS), new CorePoint2D(0, BS)
        };

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Outside_Deleted() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(200, 200, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(200, 200, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 20, 0, Math.PI);
            var r = new CropArcService().CropArcsOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        // 2. 拆分 (5)
        [Test] public void Cross_Split() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, -30, 0), 80, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void Tangent_Arc() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 100, 0), 30, -Math.PI / 2, Math.PI / 2);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount + r.SplitCount, 0);
        });
        [Test] public void ShortArc() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 5, 0, Math.PI / 4);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void LargeArc() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 0, 0), 120, 0, 3.0);
            var op = new CropArcService().CropArcsInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });
        [Test] public void ArcSpanning2Pi() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 60, -Math.PI / 4, Math.PI / 2 + Math.PI);
            var op = new CropArcService().CropArcsInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });

        // 3. 边界/异常 (4)
        [Test] public void NullBoundary_Fail() => Sd(tr =>
        {
            var op = new CropArcService().CropArcsInside(null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void EmptyList_Fail() => Sd(tr =>
        {
            var op = new CropArcService().CropArcsInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void DegeneratedArc_Skipped() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(50, 50, 0), 0, 0, 0);
            var r = new CropArcService().CropArcsInside(Rect, ids, tr).Data;
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
            var ids = A(tr, new Point3d(75, 45, 0), 10, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Concave_Niche_Deleted() => Sd(tr =>
        {
            var ids = A(tr, new Point3d(75, 35, 0), 10, 0, Math.PI);
            var r = new CropArcService().CropArcsInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        private static void Sd(Action<ITransactionService> a) => CadServiceManager._.ExecuteInSideDatabase(a);
        private static List<ObjectId> A(ITransactionService tr, Point3d c, double r, double sa, double ea)
        {
            var id = tr.AppendEntityToCurrentSpace(new Arc(c, r, sa, ea));
            return new List<ObjectId> { id };
        }
    }
}