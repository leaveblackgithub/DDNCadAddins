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
        public void CropArcsInside_ArcFullyInside_Kept()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateArcInTr(tr, new Point3d(50, 50, 0), 20, 0, Math.PI);
                var service = new CropArcService(new CropGeometryService());
                var op = service.CropArcsInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(0, op.Data.DeletedCount);
                Assert.AreEqual(1, op.Data.KeptCount);
            });
        }

        [Test]
        public void CropArcsInside_ArcFullyOutside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateArcInTr(tr, new Point3d(200, 200, 0), 20, 0, Math.PI);
                var service = new CropArcService(new CropGeometryService());
                var op = service.CropArcsInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        [Test]
        public void CropArcsInside_ArcCrossesBoundary_Split()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateArcInTr(tr, new Point3d(50, -50, 0), 80, 0, Math.PI);
                var service = new CropArcService(new CropGeometryService());
                var op = service.CropArcsInside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.SplitCount);
            });
        }

        [Test]
        public void CropArcsOutside_ArcInside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var ids = CreateArcInTr(tr, new Point3d(50, 50, 0), 20, 0, Math.PI);
                var service = new CropArcService(new CropGeometryService());
                var op = service.CropArcsOutside(RectBoundary, ids, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        [Test]
        public void CropArcsInside_NullBoundary_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropArcService(new CropGeometryService());
                var op = service.CropArcsInside(null, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("顶点不足", op.Message);
            });
        }

        [Test]
        public void CropArcsInside_EmptyList_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropArcService(new CropGeometryService());
                var op = service.CropArcsInside(RectBoundary, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("为空", op.Message);
            });
        }

        private static List<ObjectId> CreateArcInTr(ITransactionService tr, Point3d center, double radius, double startAngle, double endAngle)
        {
            var arc = new Arc(center, radius, startAngle, endAngle);
            var id = tr.AppendEntityToCurrentSpace(arc);
            return new List<ObjectId> { id };
        }
    }
}