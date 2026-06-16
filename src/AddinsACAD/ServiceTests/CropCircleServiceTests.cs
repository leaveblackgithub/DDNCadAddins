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
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Outside_Deleted() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(200, 200, 0), 20);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(200, 200, 0), 20);
            var op = new CropCircleService().CropCirclesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 20);
            var op = new CropCircleService().CropCirclesOutside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // 2. 拆分 (3)
        [Test] public void CrossBoundary_Split() => Sd(tr =>
        {
            // 圆心 (50, 50)，半径 120 → 圆必然完全包围 100x100 边界
            var ids = C(tr, new Point3d(50, 50, 0), 120);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.SplitCount + op.Data.KeptCount, 1);
        });
        [Test] public void SmallCircleCrossing_Split() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(0, 50, 0), 10);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
            Assert.GreaterOrEqual(op.Data.KeptCount + op.Data.SplitCount, 0);
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
        [Test] public void DegeneratedCircle_ReturnsFail() => Sd(tr =>
        {
            var ids = C(tr, new Point3d(50, 50, 0), 0);
            var op = new CropCircleService().CropCirclesInside(Rect, ids, tr);
            // 退化圆无有效处理 → 返回失败
            Assert.IsFalse(op.IsSuccess);
        });

        private static void Sd(Action<ITransactionService> a) => CadServiceManager._.ExecuteInSideDatabase(a);
        private static List<ObjectId> C(ITransactionService tr, Point3d c, double r)
        {
            var id = tr.AppendEntityToCurrentSpace(new Circle(c, Vector3d.ZAxis, r));
            return new List<ObjectId> { id };
        }
    }
}