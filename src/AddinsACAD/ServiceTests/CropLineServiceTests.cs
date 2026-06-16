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
    /// <summary>
    ///     CropLineService 自动测试 — 使用内存侧数据库，完全隔离活动文档.
    ///     新建 Database(true, true) → 创建实体 → 执行裁剪 → 断言结果 → 释放.
    ///     不修改任何图纸文件，不与活动文档事务竞争，彻底避免死锁.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CropLineServiceTests
    {
        private const double BoundarySize = 100.0;

        /// <summary>
        ///     100x100 矩形边界顶点.
        /// </summary>
        private static List<CorePoint2D> RectBoundary =>
            new List<CorePoint2D>
            {
                new CorePoint2D(0, 0),
                new CorePoint2D(BoundarySize, 0),
                new CorePoint2D(BoundarySize, BoundarySize),
                new CorePoint2D(0, BoundarySize),
            };

        // ── 1. 正常路径 ──

        /// <summary>
        ///     直线完全在内部 → KeepInside 保留.
        /// </summary>
        [Test]
        public void CropLinesInside_LineFullyInside_Kept()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var lineIds = CreateLineInTr(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(0, op.Data.DeletedCount);
                Assert.AreEqual(0, op.Data.SplitCount);
                Assert.AreEqual(1, op.Data.KeptCount);
            });
        }

        /// <summary>
        ///     直线完全在外部 → KeepInside 删除.
        /// </summary>
        [Test]
        public void CropLinesInside_LineFullyOutside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var lineIds = CreateLineInTr(tr, new Point3d(200, 200, 0), new Point3d(300, 300, 0));
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
                Assert.AreEqual(0, op.Data.KeptCount);
            });
        }

        /// <summary>
        ///     直线跨越边界 → KeepInside 拆分保留内部段.
        /// </summary>
        [Test]
        public void CropLinesInside_LineCrossesBoundary_Split()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var lineIds = CreateLineInTr(tr, new Point3d(-50, 50, 0), new Point3d(150, 50, 0));
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.SplitCount);
                Assert.AreEqual(0, op.Data.DeletedCount);
                Assert.AreEqual(0, op.Data.KeptCount);
            });
        }

        /// <summary>
        ///     KeepOutside 模式 → 内部线删除.
        /// </summary>
        [Test]
        public void CropLinesOutside_LineInside_Deleted()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var lineIds = CreateLineInTr(tr, new Point3d(20, 20, 0), new Point3d(80, 80, 0));
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesOutside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                Assert.AreEqual(1, op.Data.DeletedCount);
            });
        }

        // ── 2. 边界 / 异常路径 ──

        /// <summary>
        ///     null 边界 → 返回失败.
        /// </summary>
        [Test]
        public void CropLinesInside_NullBoundary_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(null, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("顶点不足", op.Message);
            });
        }

        /// <summary>
        ///     空列表 → 返回失败.
        /// </summary>
        [Test]
        public void CropLinesInside_EmptyLineIds_ReturnsFail()
        {
            CadServiceManager._.ExecuteInSideDatabase(tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("为空", op.Message);
            });
        }

        // ── 辅助方法 ──

        /// <summary>
        ///     在事务中创建一条直线，返回 [ObjectId].
        /// </summary>
        private static List<ObjectId> CreateLineInTr(ITransactionService tr, Point3d start, Point3d end)
        {
            var line = new Line(start, end);
            var id = tr.AppendEntityToCurrentSpace(line);
            return new List<ObjectId> { id };
        }
    }
}