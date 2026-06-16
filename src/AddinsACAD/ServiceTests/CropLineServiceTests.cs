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
    ///     CropLineService 自动化测试 — 使用当前文档内存侧数据.
    ///     创建→测试→清理三步分离，避免同事务内死锁.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CropLineServiceTests
    {
        private const double BoundarySize = 100.0;

        /// <summary>
        ///     获取一个 100x100 的矩形边界顶点.
        /// </summary>
        private static List<CorePoint2D> RectBoundary =>
            new List<CorePoint2D>
            {
                new CorePoint2D(0, 0),
                new CorePoint2D(BoundarySize, 0),
                new CorePoint2D(BoundarySize, BoundarySize),
                new CorePoint2D(0, BoundarySize),
            };

        /// <summary>
        ///     生成唯一图层名.
        /// </summary>
        private static string UniqueLayer() => "TestCropLine_" + Guid.NewGuid().ToString("N");

        // ── 1. 正常路径 ──

        /// <summary>
        ///     直线完全在边界内部时，KeepInside 应保留.
        /// </summary>
        [Test]
        public void CropLinesInside_LineFullyInside_Kept()
        {
            var layer = UniqueLayer();
            var lineIds = this.CreateLine(layer, new Point3d(20, 20, 0), new Point3d(80, 80, 0));

            CropLineResult result = null;
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                result = op.Data;
            });

            Assert.AreEqual(0, result.DeletedCount);
            Assert.AreEqual(0, result.SplitCount);
            Assert.AreEqual(1, result.KeptCount);
            Assert.AreEqual(0, result.SkippedCount);

            this.EraseRemaining(lineIds);
        }

        /// <summary>
        ///     直线完全在边界外部时，KeepInside 应删除.
        /// </summary>
        [Test]
        public void CropLinesInside_LineFullyOutside_Deleted()
        {
            var layer = UniqueLayer();
            var lineIds = this.CreateLine(layer, new Point3d(200, 200, 0), new Point3d(300, 300, 0));

            CropLineResult result = null;
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                result = op.Data;
            });

            Assert.AreEqual(1, result.DeletedCount);
            Assert.AreEqual(0, result.SplitCount);
            Assert.AreEqual(0, result.KeptCount);
            Assert.AreEqual(0, result.SkippedCount);
        }

        /// <summary>
        ///     直线跨越边界时，KeepInside 应拆分并保留内部段.
        /// </summary>
        [Test]
        public void CropLinesInside_LineCrossesBoundary_Split()
        {
            var layer = UniqueLayer();
            var lineIds = this.CreateLine(layer, new Point3d(-50, 50, 0), new Point3d(150, 50, 0));

            CropLineResult result = null;
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                result = op.Data;
            });

            Assert.AreEqual(0, result.DeletedCount);
            Assert.AreEqual(1, result.SplitCount);
            Assert.AreEqual(0, result.KeptCount);
            Assert.AreEqual(0, result.SkippedCount);
        }

        /// <summary>
        ///     KeepOutside 模式下，完全在内部的直线应删除.
        /// </summary>
        [Test]
        public void CropLinesOutside_LineInside_Deleted()
        {
            var layer = UniqueLayer();
            var lineIds = this.CreateLine(layer, new Point3d(20, 20, 0), new Point3d(80, 80, 0));

            CropLineResult result = null;
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesOutside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                result = op.Data;
            });

            Assert.AreEqual(1, result.DeletedCount);
            Assert.AreEqual(0, result.SplitCount);
            Assert.AreEqual(0, result.KeptCount);
            Assert.AreEqual(0, result.SkippedCount);
        }

        // ── 2. 边界 / 异常路径 ──

        /// <summary>
        ///     空边界顶点应返回失败.
        /// </summary>
        [Test]
        public void CropLinesInside_NullBoundary_ReturnsFail()
        {
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(null, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("顶点不足", op.Message);
            });
        }

        /// <summary>
        ///     空 lineIds 列表应返回失败.
        /// </summary>
        [Test]
        public void CropLinesInside_EmptyLineIds_ReturnsFail()
        {
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, new List<ObjectId>(), tr);
                Assert.IsFalse(op.IsSuccess);
                StringAssert.Contains("为空", op.Message);
            });
        }

        /// <summary>
        ///     包含已擦除/无效 ID 时应跳过.
        /// </summary>
        [Test]
        public void CropLinesInside_ErasedId_Skipped()
        {
            var layer = UniqueLayer();
            var lineIds = this.CreateLine(layer, new Point3d(200, 200, 0), new Point3d(300, 300, 0));

            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var ent = tr.GetObject<Entity>(lineIds[0], OpenMode.ForWrite);
                ent.Erase();
            });

            CropLineResult result = null;
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var service = new CropLineService(new CropGeometryService());
                var op = service.CropLinesInside(RectBoundary, lineIds, tr);
                Assert.IsTrue(op.IsSuccess, op.Message);
                result = op.Data;
            });

            Assert.AreEqual(1, result.SkippedCount);
            Assert.AreEqual(0, result.DeletedCount);
            Assert.AreEqual(0, result.SplitCount);
            Assert.AreEqual(0, result.KeptCount);
        }

        // ── 辅助方法 ──

        /// <summary>
        ///     在指定图层上创建一条直线，返回其 ObjectId 列表.
        ///     创建与操作分不同事务，避免混用.
        /// </summary>
        private List<ObjectId> CreateLine(string layerName, Point3d start, Point3d end)
        {
            var ids = new List<ObjectId>();

            // 事务 A: 创建图层
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                tr.Style.CreateLayer(layerName);
            });

            // 事务 B: 创建直线
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                var line = new Line(start, end) { Layer = layerName };
                var id = tr.AppendEntityToCurrentSpace(line);
                ids.Add(id);
            });

            return ids;
        }

        /// <summary>
        ///     清除残留实体；仅尝试擦除未被服务删掉的实体.
        /// </summary>
        private void EraseRemaining(List<ObjectId> ids)
        {
            CadServiceManager._.ExecuteInTransactions("", tr =>
            {
                foreach (var id in ids)
                {
                    if (id.IsValid && !id.IsErased)
                    {
                        var ent = tr.GetObject<Entity>(id, OpenMode.ForWrite);
                        ent?.Erase();
                    }
                }
            });
        }
    }
}