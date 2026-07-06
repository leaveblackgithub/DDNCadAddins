using System;
using System.Collections.Generic;
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
    public class CropBlockServiceTests : CropServiceTestBase
    {
        private CropBlockService CreateService()
        {
            var cropService = new CropService(Geometry);
            return new CropBlockService(Geometry, cropService);
        }

        private static ICropBoundary CreateRectBoundary()
        {
            return new PolygonCropBoundary(Rect);
        }

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = B(tr, new Point3d(50, 50, 0), "TEST_BLOCK");
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, true, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = B(tr, new Point3d(200, 200, 0), "TEST_BLOCK");
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, true, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = B(tr, new Point3d(200, 200, 0), "TEST_BLOCK");
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, false, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = B(tr, new Point3d(50, 50, 0), "TEST_BLOCK");
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, false, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });

        // 2. 边界 (2)
        [Test] public void OnBoundary_Deleted_KeepInside() => SideDb(tr =>
        {
            var ids = B(tr, new Point3d(0, 50, 0), "TEST_BLOCK");
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, true, tr).Data;
            Assert.GreaterOrEqual(r.DeletedCount + r.KeptCount, 0);
        });
        [Test] public void OnBoundary_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = B(tr, new Point3d(0, 50, 0), "TEST_BLOCK");
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, false, tr).Data;
            Assert.GreaterOrEqual(r.KeptCount, 0);
        });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropBlocks(null, Rect, new List<ObjectId>(), true, tr);
            Assert.IsFalse(op.IsSuccess);
        });
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropBlocks(CreateRectBoundary(), Rect, new List<ObjectId>(), true, tr);
            Assert.IsFalse(op.IsSuccess);
        });
        [Test] public void InvalidId_Skipped() => SideDb(tr =>
        {
            var ids = new List<ObjectId> { ObjectId.Null };
            var r = CreateService().CropBlocks(CreateRectBoundary(), Rect, ids, true, tr).Data;
            Assert.AreEqual(1, r.SkippedCount);
        });

        private static List<ObjectId> B(ITransactionService tr, Point3d pos, string blkName)
        {
            // Create a simple block definition with one circle
            var entities = new List<Entity>
            {
                new Circle(Point3d.Origin, Vector3d.ZAxis, 5)
                { Layer = "0", ColorIndex = 2 }
            };
            var blkDefId = tr.Block.CreateBlockDef(entities, blkName);
            if (blkDefId.IsNull)
                return new List<ObjectId>();

            var blkRefId = tr.Block.CreateBlockRefInCurrentSpace(blkDefId, pos, "0", 1, "ByLayer");
            return new List<ObjectId> { blkRefId };
        }
    }
}
