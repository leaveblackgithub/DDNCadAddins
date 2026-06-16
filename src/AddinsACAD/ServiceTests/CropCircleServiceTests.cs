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
        public void CropCirclesInside_CircleFullyInside_Kept()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateCircle(tr, new Point3d(50, 50, 0), 20);
                var service = new CropCircleService(new CropGeometryService());
                var op = service.CropCirclesInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(0, op.Data.DeletedCount);
                Assert.AreEqual(1, op.Data.KeptCount);
            });
        }

        [Test]
        public void CropCirclesInside_CircleFullyOutside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateCircle(tr, new Point3d(200, 200, 0), 20);
                var service = new CropCircleService(new CropGeometryService());
                var op = service.CropCirclesInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        [Test]
        public void CropCirclesOutside_CircleInside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateCircle(tr, new Point3d(50, 50, 0), 20);
                var service = new CropCircleService(new CropGeometryService());
                var op = service.CropCirclesOutside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        [Test]
        public void CropCirclesInside_NullBoundary_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropCircleService(new CropGeometryService());
                var op = service.CropCirclesInside(null, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("顶点不足", op.Message);
            });
        }

        [Test]
        public void CropCirclesInside_EmptyList_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropCircleService(new CropGeometryService());
                var op = service.CropCirclesInside(RectBoundary, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("为空", op.Message);
            });
        }

        private static List<ObjectId> CreateCircle(ITransactionService tr, Point3d center, double radius)
        {
            var circle = new Circle(center, Vector3d.ZAxis, radius);
            var id = tr.AppendEntityToCurrentSpace(circle);
            return new List<ObjectId> { id };
        }
    }
}