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
        private const double BoundarySize = 100.0;
        private static List<CorePoint2D> RectBoundary =>
            new List<CorePoint2D>
            {
                new CorePoint2D(0, 0),
                new CorePoint2D(BoundarySize, 0),
                new CorePoint2D(BoundarySize, BoundarySize),
                new CorePoint2D(0, BoundarySize),
            };

        [Test]
        public void CropPolylinesInside_PolyFullyInside_Kept()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateRectPoly(tr, 20, 20, 80, 80);
                var service = new CropPolylineService(new CropGeometryService());
                var op = service.CropPolylinesInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(0, op.Data.DeletedCount);
                Assert.AreEqual(1, op.Data.KeptCount);
            });
        }

        [Test]
        public void CropPolylinesInside_PolyFullyOutside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateRectPoly(tr, 200, 200, 250, 250);
                var service = new CropPolylineService(new CropGeometryService());
                var op = service.CropPolylinesInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        [Test]
        public void CropPolylinesInside_PolyCrossesBoundary_Split()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateRectPoly(tr, -50, 25, 150, 75);
                var service = new CropPolylineService(new CropGeometryService());
                var op = service.CropPolylinesInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.SplitCount);
            });
        }

        [Test]
        public void CropPolylinesOutside_PolyInside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateRectPoly(tr, 20, 20, 80, 80);
                var service = new CropPolylineService(new CropGeometryService());
                var op = service.CropPolylinesOutside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        [Test]
        public void CropPolylinesInside_NullBoundary_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropPolylineService(new CropGeometryService());
                var op = service.CropPolylinesInside(null, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("顶点不足", op.Message);
            });
        }

        [Test]
        public void CropPolylinesInside_EmptyList_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropPolylineService(new CropGeometryService());
                var op = service.CropPolylinesInside(RectBoundary, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("为空", op.Message);
            });
        }

        /// <summary>
        ///     在事务中创建矩形多段线，返回 [ObjectId].
        /// </summary>
        private static List<ObjectId> CreateRectPoly(ITransactionService tr, double x1, double y1, double x2, double y2)
        {
            var poly = new Polyline();
            poly.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            poly.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            poly.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            poly.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            poly.Closed = true;
            var id = tr.AppendEntityToCurrentSpace(poly);
            return new List<ObjectId> { id };
        }
    }
}