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
    ///     CropMLineService 集成测试 — 包围盒分类（CropUtils.ProcessNonCurve）.
    ///     使用侧数据库（不影响当前图纸）.
    ///     边界: 100x100 矩形 (0,0)-(100,100).
    ///     MLine 走非曲线路径：包围盒分类 → 保留或删除，不支持拆分.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CropMLineServiceTests : CropServiceTestBase
    {
        // ── 辅助方法 ──

        /// <summary>
        ///     创建一条 MLine 并返回其 ObjectId 列表.
        ///     MLine 需要先创建 MlineStyle，然后通过 AppendEntity 添加.
        /// </summary>
        private static List<ObjectId> Ml(ITransactionService tr, params Point3d[] vertices)
        {
            // 使用 STANDARD 样式（默认存在）
            var mline = new Mline();
            mline.SetElevation(0.0);
            mline.SetScale(1.0);

            // 添加顶点
            for (var i = 0; i < vertices.Length; i++)
            {
                if (i == 0)
                    mline.AddVertex(i, vertices[i], Vector3d.XAxis, Vector3d.YAxis, MlineJustification.Top);
                else
                    mline.AddVertex(i, vertices[i], Vector3d.XAxis, Vector3d.YAxis, MlineJustification.Top);
            }

            // 设置闭合状态
            mline.Closed = false;

            return Ids(tr, mline);
        }

        /// <summary>
        ///     调用 CropMLinesInside（多边形边界）.
        /// </summary>
        private static OpResult<CropMLineResult> CropIn(
            ITransactionService tr, List<ObjectId> ids)
        {
            var service = new CropMLineService(new CropGeometryService());
            return service.CropMLinesInside(Rect, ids, tr);
        }

        /// <summary>
        ///     调用 CropMLinesOutside（多边形边界）.
        /// </summary>
        private static OpResult<CropMLineResult> CropOut(
            ITransactionService tr, List<ObjectId> ids)
        {
            var service = new CropMLineService(new CropGeometryService());
            return service.CropMLinesOutside(Rect, ids, tr);
        }

        // ── 1. 基本保留/删除 (4) ──

        /// <summary>MLine 完全在边界内 → KeptCount=1</summary>
        [Test]
        public void Inside_Kept() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(20, 50, 0), new Point3d(80, 50, 0));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
            Assert.AreEqual(0, op.Data.DeletedCount);
        });

        /// <summary>MLine 完全在边界外 → DeletedCount=1</summary>
        [Test]
        public void Outside_Deleted() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(20, 200, 0), new Point3d(80, 200, 0));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        /// <summary>KeepOutside: 边界外的 MLine 保留</summary>
        [Test]
        public void Outside_Kept_KeepOutside() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(20, 200, 0), new Point3d(80, 200, 0));
            var op = CropOut(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
        });

        /// <summary>KeepOutside: 边界内的 MLine 删除</summary>
        [Test]
        public void Inside_Deleted_KeepOutside() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(20, 50, 0), new Point3d(80, 50, 0));
            var op = CropOut(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.DeletedCount);
        });

        // ── 2. 相交（包围盒分类→ 保留/删除） (2) ──

        /// <summary>MLine 穿越边界 → 包围盒分类为 Intersects → 保留（非曲线不走删除）</summary>
        [Test]
        public void Cross_Kept() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(50, -20, 0), new Point3d(50, 120, 0));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            // MLine 使用 ProcessNonCurve: Inside/OnBoundary/Intersects → Kept
            Assert.AreEqual(1, op.Data.KeptCount, "MLine 穿越边界应保留（非曲线不走删除）");
        });

        /// <summary>MLine 对角线穿越 → 保留</summary>
        [Test]
        public void Diagonal_Kept() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(-50, -50, 0), new Point3d(150, 150, 0));
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.KeptCount);
        });

        // ── 3. 边界/异常 (3) ──

        /// <summary>null 边界 → 抛出异常（服务未做 null 检查）</summary>
        protected override void NullBoundary_Fail() => SideDb(tr =>
        {
            var service = new CropMLineService(new CropGeometryService());
            Assert.Throws<NullReferenceException>(() =>
                service.CropMLinesInside(null, new List<ObjectId>(), tr));
        });

        /// <summary>空实体列表 → 返回成功，各项计数为 0</summary>
        protected override void EmptyList_Fail() => SideDb(tr =>
        {
            var service = new CropMLineService(new CropGeometryService());
            var op = service.CropMLinesInside(Rect, new List<ObjectId>(), tr);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(0, op.Data.DeletedCount);
            Assert.AreEqual(0, op.Data.KeptCount);
            Assert.AreEqual(0, op.Data.SkippedCount);
        });

        /// <summary>已删除的 ID → Skipped</summary>
        [Test]
        public void ErasedId_Skipped() => SideDb(tr =>
        {
            var ids = Ml(tr, new Point3d(20, 200, 0), new Point3d(80, 200, 0));
            tr.GetObject<Entity>(ids[0], OpenMode.ForWrite).Erase();
            var op = CropIn(tr, ids);
            Assert.IsTrue(op.IsSuccess, op.Message);
            Assert.AreEqual(1, op.Data.SkippedCount);
        });
    }
}
