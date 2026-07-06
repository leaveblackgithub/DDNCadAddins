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
    ///     Crop3DPolylineService 集成测试 — 参数搜索二分 + GetSplitCurves 拆分.
    ///     使用侧数据库（不影响当前图纸）.
    ///     边界: 100x100 矩形 (0,0)-(100,100).
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class Crop3DPolylineServiceTests : CropServiceTestBase
    {
        // ── 辅助方法 ──

        /// <summary>
        ///     创建一条 3D Polyline 并返回其 ObjectId 列表.
        /// </summary>
        private static List<ObjectId> P3d(ITransactionService tr, params Point3d[] pts)
        {
            var collection = new Point3dCollection();
            foreach (var pt in pts) collection.Add(pt);
            var poly = new Polyline3d(Poly3dType.SimplePoly, collection, false);
            return Ids(tr, poly);
        }

        /// <summary>
        ///     调用 Crop3DPolylinesInside（多边形边界）.
        /// </summary>
        private static ServiceACAD.OpResult<Crop3DPolylineResult> CropIn(
            ITransactionService tr, List<ObjectId> ids)
        {
            var service = new Crop3DPolylineService(new CropGeometryService());
            return service.Crop3DPolylinesInside(Rect, ids, tr);
        }

        /// <summary>
        ///     调用 Crop3DPolylinesOutside（多边形边界）.
        /// </summary>
        private static ServiceACAD.OpResult<Crop3DPolylineResult> CropOut(
            ITransactionService tr, List<ObjectId> ids)
        {
            var service = new Crop3DPolylineService(new CropGeometryService());
            return service.Crop3DPolylinesOutside(Rect, ids, tr);
        }

        // ── 1. 基本保留/删除 (4) ──

        [Test] public void Inside_Kept() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(20, 20, 0), new Point3d(50, 30, 5),
                new Point3d(80, 60, 10), new Point3d(60, 80, 5), new Point3d(30, 70, 0));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
            Assert.AreEqual(0, op.Data.DeletedCount);
        });

        [Test] public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(200, 200, 0), new Point3d(250, 220, 5),
                new Point3d(280, 250, 10), new Point3d(220, 300, 5));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(200, 200, 0), new Point3d(250, 220, 5),
                new Point3d(280, 250, 10), new Point3d(220, 300, 5));
            var op = CropOut(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
        });

        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(20, 20, 0), new Point3d(50, 30, 5),
                new Point3d(80, 60, 10), new Point3d(60, 80, 5), new Point3d(30, 70, 0));
            var op = CropOut(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // ── 2. 穿越拆分 (3) ──

        [Test] public void HorizontalCross_Split() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(-50, 50, 0), new Point3d(30, 50, 5),
                new Point3d(80, 50, 10), new Point3d(150, 50, 5));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.SplitCount);
        });

        [Test] public void DiagonalCross_Split() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(-50, -50, 0), new Point3d(40, 40, 5),
                new Point3d(120, 120, 10));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.SplitCount);
        });

        [Test] public void EndpointOnBoundary() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(0, 30, 0), new Point3d(30, 30, 5),
                new Point3d(60, 30, 10), new Point3d(100, 30, 5));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.GreaterOrEqual(op.Data.KeptCount + op.Data.SplitCount, 1);
        });

        // ── 3. 边界/异常 (3) ──

        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var service = new Crop3DPolylineService(new CropGeometryService());
            Assert.Throws<NullReferenceException>(() =>
                service.Crop3DPolylinesInside(null, new List<ObjectId>(), tr));
        });

        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var service = new Crop3DPolylineService(new CropGeometryService());
            var op = service.Crop3DPolylinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(0, op.Data.DeletedCount);
            Assert.AreEqual(0, op.Data.KeptCount);
            Assert.AreEqual(0, op.Data.SplitCount);
            Assert.AreEqual(0, op.Data.SkippedCount);
        });

        [Test] public void ErasedId_Skipped() => SideDb(tr =>
        {
            var ids = P3d(tr, new Point3d(200, 200, 0), new Point3d(250, 220, 5),
                new Point3d(280, 250, 10));
            tr.GetObject<Entity>(ids[0], OpenMode.ForWrite).Erase();
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.SkippedCount);
        });
    }
}
