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
    public class CropPolylineServiceTests
    {
        private const double BS = 100.0;
        private static List<CorePoint2D> Rect = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0), new CorePoint2D(BS, 0), new CorePoint2D(BS, BS), new CorePoint2D(0, BS)
        };

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => Sd(tr =>
        {
            var ids = R(tr, 20, 20, 80, 80);
            var r = new CropPolylineService().CropPolylinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount); Assert.AreEqual(0, r.DeletedCount);
        });
        [Test] public void Outside_Deleted() => Sd(tr =>
        {
            var ids = R(tr, 200, 200, 250, 250);
            var r = new CropPolylineService().CropPolylinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });
        [Test] public void Outside_Kept_KeepOutside() => Sd(tr =>
        {
            var ids = R(tr, 200, 200, 250, 250);
            var r = new CropPolylineService().CropPolylinesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Inside_Deleted_KeepOutside() => Sd(tr =>
        {
            var ids = R(tr, 20, 20, 80, 80);
            var r = new CropPolylineService().CropPolylinesOutside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        // 2. 拆分 (4)
        [Test] public void Cross_Split() => Sd(tr =>
        {
            var ids = R(tr, -50, 25, 150, 75);
            var r = new CropPolylineService().CropPolylinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void OpenPolyline_Cross_Split() => Sd(tr =>
        {
            var ids = O(tr, new Point2d(-20, 50), new Point2d(50, 50), new Point2d(120, 50));
            var r = new CropPolylineService().CropPolylinesInside(Rect, ids, tr).Data;
            Assert.AreEqual(1, r.SplitCount);
        });
        [Test] public void ArcPolyline_Cross_Split() => Sd(tr =>
        {
            var ids = ArcPoly(tr);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });
        [Test] public void ArcPolyline_WithBulge_Preserved() => Sd(tr =>
        {
            var ids = ArcPoly(tr);
            var op = new CropPolylineService().CropPolylinesInside(Rect, ids, tr);
            Assert.IsTrue(op.IsSuccess);
        });

        // 3. 边界/异常 (3)
        [Test] public void NullBoundary_Fail() => Sd(tr =>
        {
            var op = new CropPolylineService().CropPolylinesInside(null, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void EmptyList_Fail() => Sd(tr =>
        {
            var op = new CropPolylineService().CropPolylinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void SingleVertexPoly_Skipped() => Sd(tr =>
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(50, 50), 0, 0, 0);
            var ids = new List<ObjectId> { tr.AppendEntityToCurrentSpace(p) };
            var r = new CropPolylineService().CropPolylinesInside(Rect, ids, tr).Data;
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
            var ids = RC(tr, Concave, 75, 45, 85, 55);
            var r = new CropPolylineService().CropPolylinesInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.KeptCount);
        });
        [Test] public void Concave_Niche_Deleted() => Sd(tr =>
        {
            var ids = RC(tr, Concave, 75, 35, 80, 40);
            var r = new CropPolylineService().CropPolylinesInside(Concave, ids, tr).Data;
            Assert.AreEqual(1, r.DeletedCount);
        });

        private static void Sd(Action<ITransactionService> a) => CadServiceManager._.ExecuteInSideDatabase(a);

        private static List<ObjectId> R(ITransactionService tr, double x1, double y1, double x2, double y2)
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            p.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            p.Closed = true;
            return new List<ObjectId> { tr.AppendEntityToCurrentSpace(p) };
        }

        private static List<ObjectId> RC(ITransactionService tr, List<CorePoint2D> boundary, double x1, double y1, double x2, double y2)
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            p.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            p.Closed = true;
            return new List<ObjectId> { tr.AppendEntityToCurrentSpace(p) };
        }

        private static List<ObjectId> O(ITransactionService tr, params Point2d[] pts)
        {
            var p = new Polyline();
            for (var i = 0; i < pts.Length; i++)
                p.AddVertexAt(i, pts[i], 0, 0, 0);
            return new List<ObjectId> { tr.AppendEntityToCurrentSpace(p) };
        }

        private static List<ObjectId> ArcPoly(ITransactionService tr)
        {
            // 创建含弧段的多段线（半圆 + 直线）
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(50, 50), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(120, 50), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(120, 120), 0, 0, 0);
            p.Closed = true;
            return new List<ObjectId> { tr.AppendEntityToCurrentSpace(p) };
        }
    }
}