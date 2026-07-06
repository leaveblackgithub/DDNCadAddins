using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Services;
using NUnit.Framework;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    /// <summary>
    ///     CropBlockService 集成测试 — 包围盒分类（不含 Explode 路径）.
    ///     使用侧数据库（不影响当前图纸）.
    ///     边界: 100x100 矩形 (0,0)-(100,100).
    ///     注意: Intersects 路径触发 BlockExploder.Explode() 在侧数据库中会卡死，
    ///     因此仅测试 Inside/Outside 包围盒分类.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CropBlockServiceTests : CropServiceTestBase
    {
        private const string TestBlockName = "TEST_BLOCK_CROP";

        /// <summary>
        ///     创建含 30x30 矩形（4 条线，中心原点）的块定义，并在指定点插入块参照.
        /// </summary>
        private static ObjectId CreateBlockRef(ITransactionService tr, double insX, double insY)
        {
            var entities = new List<Entity>
            {
                new Line(new Point3d(-15, -15, 0), new Point3d(15, -15, 0)),
                new Line(new Point3d(15, -15, 0), new Point3d(15, 15, 0)),
                new Line(new Point3d(15, 15, 0), new Point3d(-15, 15, 0)),
                new Line(new Point3d(-15, 15, 0), new Point3d(-15, -15, 0)),
            };
            var blkDefId = tr.Block.CreateBlockDef(entities, TestBlockName);
            var blockRef = new BlockReference(new Point3d(insX, insY, 0), blkDefId);
            return tr.AppendEntityToModelSpace(blockRef);
        }

        /// <summary>
        ///     创建 CropBlockService. CropService 仅用于 explode 路径（Intersects），
        ///     Inside/Outside 测试不会触发.
        /// </summary>
        private CropBlockService CreateService()
        {
            return new CropBlockService(Geometry, new CropService(Geometry));
        }

        // ── 基本保留/删除 (4) ──

        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var id = CreateBlockRef(tr, 50, 50);
            var boundary = new PolygonCropBoundary(Rect);
            var op = CreateService().CropBlocks(boundary, Rect, new List<ObjectId> { id }, keepInside: true, ts: tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
            Assert.AreEqual(0, op.Data.DeletedCount);
        });

        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var id = CreateBlockRef(tr, 200, 200);
            var boundary = new PolygonCropBoundary(Rect);
            var op = CreateService().CropBlocks(boundary, Rect, new List<ObjectId> { id }, keepInside: true, ts: tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var id = CreateBlockRef(tr, 200, 200);
            var boundary = new PolygonCropBoundary(Rect);
            var op = CreateService().CropBlocks(boundary, Rect, new List<ObjectId> { id }, keepInside: false, ts: tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
        });

        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var id = CreateBlockRef(tr, 50, 50);
            var boundary = new PolygonCropBoundary(Rect);
            var op = CreateService().CropBlocks(boundary, Rect, new List<ObjectId> { id }, keepInside: false, ts: tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // ── 边界/异常 (3) ──

        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var op = CreateService().CropBlocks(null, Rect, new List<ObjectId>(), keepInside: true, ts: tr);
            Assert.IsFalse(op.IsSuccess);
        });

        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var boundary = new PolygonCropBoundary(Rect);
            var op = CreateService().CropBlocks(boundary, Rect, new List<ObjectId>(), keepInside: true, ts: tr);
            Assert.IsFalse(op.IsSuccess);
        });

        [Test] public void ErasedId_Skipped() => SideDb(tr =>
        {
            var id = CreateBlockRef(tr, 200, 200);
            tr.GetObject<BlockReference>(id, OpenMode.ForWrite).Erase();
            var boundary = new PolygonCropBoundary(Rect);
            var op = CreateService().CropBlocks(boundary, Rect, new List<ObjectId> { id }, keepInside: true, ts: tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.SkippedCount);
        });
    }
}
