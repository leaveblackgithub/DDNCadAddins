using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CoreMatrix3D = DDNCadAddins.Core.Models.Matrix3D;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     图块服务 — 块参照查询（XClip/属性）、属性读写、爆炸委托、空定义删除、XClip边界生成。
    ///     爆炸及后处理逻辑已提取到 BlockExploder。
    /// </summary>
    public class BlockService : IBlockService
    {
        private readonly IXClipBoundaryGeometryService _xclipBoundaryGeometry = new XClipBoundaryGeometryService();

        public BlockService(ITransactionService serviceTrans, BlockReference blkRef)
        {
            ServiceTrans = serviceTrans;
            CadBlkRef = blkRef;
        }

        public ITransactionService ServiceTrans { get; }
        public BlockReference CadBlkRef { get; }
        public string Name => CadBlkRef.Name;
        public ObjectId ObjectId => CadBlkRef.ObjectId;

        public string Layer
        {
            get => CadBlkRef.Layer;
            set { UpgradeOpen(); CadBlkRef.Layer = value; }
        }

        public int ColorIndex
        {
            get => CadBlkRef.ColorIndex;
            set { UpgradeOpen(); CadBlkRef.ColorIndex = value; }
        }

        public string Linetype
        {
            get => CadBlkRef.Linetype;
            set { UpgradeOpen(); CadBlkRef.Linetype = value; }
        }

        // ── 查询 ──

        public bool IsXclipped()
        {
            if (CadBlkRef == null || CadBlkRef.IsErased)
                return false;
            if (!CadBlkRef.ExtensionDictionary.IsValid)
                return false;

            try
            {
                var extDict = GetDictionaryObject(CadBlkRef.ExtensionDictionary);
                if (extDict == null || !extDict.Contains("ACAD_FILTER"))
                    return false;

                var acadFilterId = extDict.GetAt("ACAD_FILTER");
                if (acadFilterId == ObjectId.Null)
                    return false;

                var filterDict = GetDictionaryObject(acadFilterId);
                if (filterDict == null || !filterDict.Contains("SPATIAL"))
                    return false;

                var hasSpatial = true;
                if (hasSpatial) Logger._.Info($"块参照 {CadBlkRef.Name} 已识别为XCLIP块");
                return filterDict.Contains("SPATIAL");
            }
            catch (Exception ex)
            {
                Logger._.Error($"检测块参照 {CadBlkRef.Name} 的XCLIP状态时发生异常: {ex.Message}");
                return false;
            }
        }

        public bool HasAttributes()
            => CadBlkRef != null && CadBlkRef.AttributeCollection.Count > 0;

        // ── 爆炸（委托给 BlockExploder）──

        public OpResult<ExplodeAsShownResult> ExplodeAsShown()
        {
            if (CadBlkRef == null)
                return OpResult<ExplodeAsShownResult>.Fail("CadBlkRef is null");
            if (IsXclipped())
                return OpResult<ExplodeAsShownResult>.Fail("XCLIP 图块不应被爆炸，需后续处理");

            var exploder = new BlockExploder(ServiceTrans);
            return exploder.Explode(CadBlkRef);
        }

        // ── 删除 ──

        public OpResult<bool> EraseIfEmptyDefinition()
        {
            try
            {
                if (CadBlkRef == null)
                    return OpResult<bool>.Fail("块参照为空");
                if (HasAttributes())
                    return OpResult<bool>.Fail("空定义图块仍包含属性引用");

                var blockDef = ServiceTrans.GetObject<BlockTableRecord>(CadBlkRef.BlockTableRecord);
                if (blockDef == null || !CadBlkRef.BlockTableRecord.IsValid)
                    return OpResult<bool>.Fail("块定义无效");

                var entityIds = ServiceTrans.GetChildObjects<DBObject>(blockDef);
                if (entityIds.Count > 0)
                    return OpResult<bool>.Fail("块定义包含实体");

                UpgradeOpen();
                var blockName = CadBlkRef.Name;
                CadBlkRef.Erase();
                Logger._.Info($"已删除空定义图块参照: {blockName}");
                return OpResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Logger._.Error($"删除空定义图块失败: {ex.Message}");
                return OpResult<bool>.Fail($"删除空定义图块失败: {ex.Message}");
            }
        }

        // ── Xclip 边界生成 ──

        public OpResult<ObjectId> GenerateXclipBoundary()
        {
            try
            {
                if (CadBlkRef == null)
                    return OpResult<ObjectId>.Fail("无法获取图块引用");
                if (!IsXclipped())
                    return OpResult<ObjectId>.Fail("图块没有Xclip信息");

                var spatialFilter = GetXClipFilter();
                if (spatialFilter == null)
                    return OpResult<ObjectId>.Fail("无法获取XClip过滤器");

                var boundaryResult = GetXClipBoundaryPointsWcs(spatialFilter, CadBlkRef);
                if (!boundaryResult.IsSuccess)
                    return OpResult<ObjectId>.Fail(boundaryResult.Message);

                var wcsPoints = boundaryResult.Data;
                if (wcsPoints == null || wcsPoints.Count < 3)
                    return OpResult<ObjectId>.Fail("XClip边界顶点不足");

                var pl = new Polyline();
                pl.SetDatabaseDefaults();
                pl.ColorIndex = 1;
                pl.Layer = ServiceTrans.Style.GetValidLayerName("_XCLIP_BOUNDARY");
                pl.Closed = true;
                pl.LineWeight = LineWeight.LineWeight100;

                for (var i = 0; i < wcsPoints.Count; i++)
                    pl.AddVertexAt(i, wcsPoints[i], 0, 0, 0);

                var polyId = ServiceTrans.AppendEntityToModelSpace(pl);
                return polyId == ObjectId.Null
                    ? OpResult<ObjectId>.Fail("无法将多段线添加到模型空间")
                    : OpResult<ObjectId>.Success(polyId);
            }
            catch (Exception ex)
            {
                Logger._.Error($"生成Xclip边界时发生错误: {ex.Message}");
                return OpResult<ObjectId>.Fail($"生成Xclip边界时发生错误: {ex.Message}");
            }
        }

        // ── TryZoomToBlock ──

        /// <summary>复制 XClip 状态（委托给 BlockExploder）</summary>
        public void CopyXclipState(BlockReference source, BlockReference target)
        {
            new BlockExploder(ServiceTrans).CopyXclipState(source, target);
        }

        public OpResult<bool> TryZoomToBlock()
        {
            try
            {
                if (CadBlkRef == null)
                    return OpResult<bool>.Fail("无法获取图块引用");

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return OpResult<bool>.Fail("无法获取当前文档");

                var blockPosition = CadBlkRef.Position;
                using (ViewTableRecord view = doc.Editor.GetCurrentView())
                {
                    view.CenterPoint = new Point2d(blockPosition.X, blockPosition.Y);
                    double ratio = view.Height / view.Width;
                    view.Width = 50.0;
                    view.Height = 50.0 * ratio;
                    doc.Editor.SetCurrentView(view);
                    doc.Editor.Regen();
                }

                Logger._.Info($"视图已缩放到图块位置: ({blockPosition.X}, {blockPosition.Y})");
                return OpResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                Logger._.Error($"设置视图失败: {ex.Message}");
                return OpResult<bool>.Fail($"设置视图失败: {ex.Message}");
            }
        }

        // ── 私有辅助 ──

        private void UpgradeOpen()
        {
            if (!CadBlkRef.IsWriteEnabled)
                CadBlkRef.UpgradeOpen();
        }

        private SpatialFilter GetXClipFilter()
        {
            try
            {
                if (CadBlkRef.ExtensionDictionary == ObjectId.Null)
                    return null;

                var extDict = GetDictionaryObject(CadBlkRef.ExtensionDictionary);
                if (extDict == null || !extDict.Contains("ACAD_FILTER"))
                    return null;

                var filterDict = GetDictionaryObject(extDict.GetAt("ACAD_FILTER"));
                if (filterDict == null || !filterDict.Contains("SPATIAL"))
                    return null;

                var spatialId = filterDict.GetAt("SPATIAL");
                return spatialId == ObjectId.Null ? null : ServiceTrans.GetObject<SpatialFilter>(spatialId);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取XClip过滤器失败: {ex.Message}");
                return null;
            }
        }

        private OpResult<Point2dCollection> GetXClipBoundaryPointsWcs(SpatialFilter spatialFilter, BlockReference blockRef)
        {
            try
            {
                if (spatialFilter == null) return OpResult<Point2dCollection>.Fail("XClip过滤器为空");
                if (blockRef == null) return OpResult<Point2dCollection>.Fail("块参照为空");

                var definition = spatialFilter.Definition;
                var localPoints = definition.GetPoints();
                if (localPoints == null || localPoints.Count == 0)
                    return OpResult<Point2dCollection>.Fail("XClip边界点为空");

                var localPoco = new List<CorePoint2D>(localPoints.Count);
                foreach (Point2d localPoint in localPoints)
                    localPoco.Add(new CorePoint2D(localPoint.X, localPoint.Y));

                var boundaryResult = _xclipBoundaryGeometry.BuildWcsBoundaryPoints(
                    localPoco,
                    CoreMatrix3D.FromArray(spatialFilter.ClipSpaceToWorldCoordinateSystemTransform.ToArray()),
                    CoreMatrix3D.FromArray(spatialFilter.OriginalInverseBlockTransform.ToArray()),
                    CoreMatrix3D.FromArray(blockRef.BlockTransform.ToArray()));

                if (!boundaryResult.IsSuccess)
                    return OpResult<Point2dCollection>.Fail(boundaryResult.Message);

                var wcsPoints = new Point2dCollection();
                foreach (var wcsPoint in boundaryResult.Data)
                    wcsPoints.Add(new Point2d(wcsPoint.X, wcsPoint.Y));

                return OpResult<Point2dCollection>.Success(wcsPoints);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取XClip边界点失败: {ex.Message}");
                return OpResult<Point2dCollection>.Fail($"获取XClip边界点失败: {ex.Message}");
            }
        }

        private DBDictionary GetDictionaryObject(ObjectId dictionaryId, OpenMode openMode = OpenMode.ForRead)
        {
            if (!dictionaryId.IsValid) return null;

            try
            {
                if (dictionaryId.GetObject(openMode) is DBDictionary direct)
                    return direct;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"直接打开扩展字典失败: {ex.Message}");
            }

            return ServiceTrans.GetObject<DBDictionary>(dictionaryId, openMode);
        }
    }
}
