using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Services;
using NUnit.Framework;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace AddinsACAD.ServiceTests
{
    /// <summary>
    ///     裁剪服务集成测试共享基类。
    ///     提供统一的边界、侧数据库执行、实体创建辅助。
    ///     子类只需定义自己的 Crop*Service 调用和实体创建方法。
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public abstract class CropServiceTestBase
    {
        /// <summary>边界正方形的边长</summary>
        protected const double BS = 100.0;

        /// <summary>100x100 矩形裁剪边界（左下角原点）</summary>
        protected static readonly List<CorePoint2D> Rect = new List<CorePoint2D>
        {
            new CorePoint2D(0, 0),
            new CorePoint2D(BS, 0),
            new CorePoint2D(BS, BS),
            new CorePoint2D(0, BS),
        };

        /// <summary>CropGeometryService 实例（纯逻辑）</summary>
        protected CropGeometryService Geometry { get; } = new CropGeometryService();

        /// <summary>在侧数据库中执行测试动作（不影响当前图纸）</summary>
        protected static void SideDb(Action<ITransactionService> action) =>
            CadServiceManager._.ExecuteInSideDatabase(action);

        // ── 公共辅助：创建单实体 ID 列表 ──

        /// <summary>在事务中创建一条直线并返回其 ObjectId 列表</summary>
        protected static List<ObjectId> Ids(ITransactionService tr, Entity entity)
        {
            return new List<ObjectId> { tr.AppendEntityToCurrentSpace(entity) };
        }

        // ── 公共边界/异常测试 ──

        /// <summary>null 边界应返回失败 — 子类实现</summary>
        protected abstract void NullBoundary_Fail();

        /// <summary>空实体列表应返回失败 — 子类实现</summary>
        protected abstract void EmptyList_Fail();
    }
}
