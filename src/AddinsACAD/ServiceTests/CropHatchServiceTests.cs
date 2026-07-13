using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using NUnit.Framework;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CropHatchServiceTests : CropServiceTestBase
    {
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { ObjectId.Null }, HatchStyle.Normal, tr);
            Assert.IsNotNull(op);
        });

        protected override void EmptyList_Fail()
        {
            var op = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId>(), HatchStyle.Normal, null);
            Assert.IsNotNull(op);
            Assert.AreEqual(0, op.Count);
        }

        [Test] public void ComputeBoundaryArea_Circle_ReturnsPiRSquared()
        {
            var circle = new CircleCropBoundary(new CorePoint2D(0, 0), 10);
            double area = CropHatchService.ComputeBoundaryArea(circle);
            Assert.AreEqual(Math.PI * 100, area, 1e-6);
        }

        [Test] public void ComputeBoundaryArea_Ellipse_ReturnsPiAB()
        {
            var ellipse = new EllipseCropBoundary(new CorePoint2D(0, 0), 10, 5, 0);
            double area = CropHatchService.ComputeBoundaryArea(ellipse);
            Assert.AreEqual(Math.PI * 10 * 5, area, 1e-6);
        }

        [Test] public void ComputeBoundaryArea_Polygon_ReturnsShoelaceArea()
        {
            var points = new List<CorePoint2D> { new CorePoint2D(0,0), new CorePoint2D(10,0), new CorePoint2D(10,10), new CorePoint2D(0,10) };
            var poly = new PolygonCropBoundary(points);
            double area = CropHatchService.ComputeBoundaryArea(poly);
            Assert.AreEqual(100, area, 1e-6);
        }

        [Test] public void ComputeBoundaryArea_NullBoundary_ReturnsZero()
        {
            double area = CropHatchService.ComputeBoundaryArea(null);
            Assert.AreEqual(0, area, 1e-6);
        }

        [Test] public void ComputePolygonArea_3Points_ReturnsTriangleArea()
        {
            var pts = new List<CorePoint2D> { new CorePoint2D(0,0), new CorePoint2D(3,0), new CorePoint2D(0,4) };
            double area = CropHatchService.ComputePolygonArea(pts);
            Assert.AreEqual(6, area, 1e-6);
        }

        [Test] public void ComputePolygonArea_Null_ReturnsZero()
        {
            Assert.AreEqual(0, CropHatchService.ComputePolygonArea(null), 1e-6);
        }

        [Test] public void ComputePolygonArea_LessThan3Points_ReturnsZero()
        {
            var pts = new List<CorePoint2D> { new CorePoint2D(0,0), new CorePoint2D(1,0) };
            Assert.AreEqual(0, CropHatchService.ComputePolygonArea(pts), 1e-6);
        }

        [Test] public void IsPointInsidePolygon_PointInside_ReturnsTrue() => SideDb(tr =>
        {
            var pline = CreateRect(tr, 0, 0, 100, 100);
            Assert.IsTrue(CropHatchService.IsPointInsidePolygon(new Point3d(50, 50, 0), pline));
        });

        [Test] public void IsPointInsidePolygon_PointOutside_ReturnsFalse() => SideDb(tr =>
        {
            var pline = CreateRect(tr, 0, 0, 100, 100);
            Assert.IsFalse(CropHatchService.IsPointInsidePolygon(new Point3d(200, 200, 0), pline));
        });

        [Test] public void IsPointInsidePolygon_PointOnEdge_ReturnsTrue() => SideDb(tr =>
        {
            var pline = CreateRect(tr, 0, 0, 100, 100);
            Assert.IsTrue(CropHatchService.IsPointInsidePolygon(new Point3d(50, 0, 0), pline));
        });

        [Test] public void IsPointInsidePolygon_NullPolyline_ReturnsFalse()
        {
            Assert.IsFalse(CropHatchService.IsPointInsidePolygon(new Point3d(0, 0, 0), null));
        }

        [Test] public void IsPointInsidePolygon_NotClosed_ReturnsFalse() => SideDb(tr =>
        {
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(0, 0), 0
, 0, 0);
            pline.AddVertexAt(1, new Point2d(10, 0), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
            pline.Closed = false;
            var id = tr.AppendEntityToCurrentSpace(pline);
            var p = tr.GetObject<Polyline>(id);
            Assert.IsFalse(CropHatchService.IsPointInsidePolygon(new Point3d(5, 5, 0), p));
        });

        [Test] public void SortByContainmentHierarchy_Null_ReturnsEmpty()
        {
            var result = CropHatchService.SortByContainmentHierarchy(null, HatchStyle.Normal, null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test] public void SortByContainmentHierarchy_SingleCurve_ReturnsSame() => SideDb(tr =>
        {
            var id = CreateRectId(tr, 0, 0, 100, 100);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { id }, HatchStyle.Normal, tr);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(id, result[0]);
        });

        [Test] public void SortByContainmentHierarchy_TwoRings_OuterFirst() => SideDb(tr =>
        {
            var outerId = CreateRectId(tr, 0, 0, 100, 100);
            var innerId = CreateRectId(tr, 20, 20, 40, 40);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { outerId, innerId }, HatchStyle.Normal, tr);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(outerId, result[0]);
            Assert.AreEqual(innerId, result[1]);
        });

        [Test] public void SortByContainmentHierarchy_DuplicateArea_Deduplicates() => SideDb(tr =>
        {
            var id1 = CreateRectId(tr, 0, 0, 100, 100);
            var id2 = CreateRectId(tr, 0, 0, 100, 100);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { id1, id2 }, HatchStyle.Normal, tr);
            Assert.AreEqual(1, result.Count);
        });

        [Test] public void SortByContainmentHierarchy_NormalStyle_FiltersContainerCurve() => SideDb(tr =>
        {
            var outerId = CreateRectId(tr, 0, 0, 100, 100);
            var innerId = CreateRectId(tr, 20, 20, 40, 40);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { outerId, innerId }, HatchStyle.Normal, tr, clipArea: 10000);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(innerId, result[0]);
        });

        [Test] public void SortByContainmentHierarchy_OuterStyle_KeepsDepth0And1() => SideDb(tr =>
        {
            var outerId = CreateRectId(tr, 0, 0, 100, 100);
            var innerId = CreateRectId(tr, 20, 20, 40, 40);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { outerId, innerId }, HatchStyle.Outer, tr);
            Assert.AreEqual(2, result.Count);
        });

        [Test] public void SortByContainmentHierarchy_IgnoreStyle_OnlyDepth0() => SideDb(tr =>
        {
            var outerId = CreateRectId(tr, 0, 0, 100, 100);
            var innerId = CreateRectId(tr, 20, 20, 40, 40);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { outerId, innerId }, HatchStyle.Ignore, tr);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(outerId, result[0]);
        });

        [Test] public void SortByContainmentHierarchy_ThreeRings_OuterFiltersDepth2() => SideDb(tr =>
        {
            var outerId = CreateRectId(tr, 0, 0, 100, 100);
            var inner1Id = CreateRectId(tr, 20, 20, 60, 60);
            var inner2Id = CreateRectId(tr, 30, 30, 40, 40);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { outerId, inner1Id, inner2Id }, HatchStyle.Outer, tr);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(outerId, result[0]);
            Assert.AreEqual(inner1Id, result[1]);
        });

        [Test] public void SortByContainmentHierarchy_ThreeRings_NormalKeepsAll() => SideDb(tr =>
        {
            var outerId = CreateRectId(tr, 0, 0, 100, 100);
            var inner1Id = CreateRectId(tr, 20, 20, 60, 60);
            var inner2Id = CreateRectId(tr, 30, 30, 40, 40);
            var result = CropHatchService.SortByContainmentHierarchy(
                new List<ObjectId> { outerId, inner1Id, inner2Id }, HatchStyle.Normal, tr);
            Assert.AreEqual(3, result.Count);
        });

        private static ObjectId CreateRectId(ITransactionService tr, double x1, double y1, double x2, double y2)
        {
            var pline = CreateRect(tr, x1, y1, x2, y2);
            return pline.ObjectId;
        }

        private static Polyline CreateRect(ITransactionService tr, double x1, double y1, double x2, double y2)
        {
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            pline.Closed = true;
            tr.AppendEntityToCurrentSpace(pline);
            return pline;
        }
    }
}
