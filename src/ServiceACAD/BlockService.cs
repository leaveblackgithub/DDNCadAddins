using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using DDNCadAddins.Core.Services;

namespace ServiceACAD
{
    public class BlockService : IBlockService
    {
        public BlockService(ITransactionService serviceTrans, BlockReference blkRef)
        {
            ServiceTrans = serviceTrans;
            CadBlkRef = blkRef;
        }

        public ITransactionService ServiceTrans { get; }

        public BlockReference CadBlkRef { get; }

        public bool IsXclipped()
        {
            if (CadBlkRef == null)
            {
                return false;
            }

            // 检查块参照是否有X裁剪
            // 在AutoCAD .NET API中，通过检查扩展字典中是否包含"ACAD_FILTER"字典和"SPATIAL"项来判断

            // 检查是否存在扩展字典
            if (CadBlkRef.ExtensionDictionary == ObjectId.Null)
            {
                return false;
            }

            try
            {
                // 打开扩展字典
                var extDict = ServiceTrans.GetObject<DBDictionary>(CadBlkRef.ExtensionDictionary);
                if (extDict == null)
                {
                    return false;
                }

                // 检查是否包含ACAD_FILTER字典
                if (!extDict.Contains("ACAD_FILTER"))
                {
                    return false;
                }

                // 打开ACAD_FILTER字典
                var filterDict = ServiceTrans.GetObject<DBDictionary>(extDict.GetAt("ACAD_FILTER"));
                if (filterDict == null)
                {
                    return false;
                }

                // 检查是否包含SPATIAL项，如果包含则表示有X裁剪
                return filterDict.Contains("SPATIAL");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     检查块参照是否包含属性
        /// </summary>
        /// <returns>如果块参照包含属性返回true，否则返回false</returns>
        public bool HasAttributes()
        {
            if (CadBlkRef == null)
            {
                return false;
            }

            // 检查块参照是否有属性
            return CadBlkRef.AttributeCollection.Count > 0;
        }

        public string Layer
        {
            get => CadBlkRef.Layer;
            set
            {
                UpgradeOpen();

                CadBlkRef.Layer = value;
            }
        }

        private void UpgradeOpen()
        {
            if (!CadBlkRef.IsWriteEnabled)
            {
                CadBlkRef.UpgradeOpen();
            }
        }


        public int ColorIndex
        {
            get => CadBlkRef.ColorIndex;
            set
            {
                UpgradeOpen();
                CadBlkRef.ColorIndex = value;
            }
        }

        public string Linetype
        {
            get => CadBlkRef.Linetype;
            set
            {
                UpgradeOpen();
                CadBlkRef.Linetype = value;
            }
        }

        public string Name => CadBlkRef.Name;

        /// <summary>
        ///     爆炸块参照并将其属性转换为文本
        /// </summary>
        /// <returns>爆炸结果及统计信息</returns>
        public OpResult<ExplodeAsShownResult> ExplodeAsShown()
        {
            if (CadBlkRef == null)
            {
                return OpResult<ExplodeAsShownResult>.Fail("CadBlkRef is null");
            }

            // if (!HasAttributes())
            // {
            //     return OpResult<ExplodeAsShownResult>.Fail("块参照不包含属性");
            // }

            try
            {
                // 以写方式获取块参照
                if (!CadBlkRef.IsWriteEnabled)
                {
                    CadBlkRef.UpgradeOpen();
                }

                var stats = new ExplodeAsShownResult();

                // 创建一个集合，用于收集需要添加到模型空间的实体
                var entitiesToAdd = new List<Entity>();

                // 处理所有属性引用，转换为文本
                var textList = ProcessAttributeReferences(CadBlkRef);
                foreach (var text in textList)
                {
                    if (text == null)
                    {
                        continue;
                    }

                    stats.AttributeTextCount++;
                    var adjustStats = SetChildPropsAsBlk(text, CadBlkRef);
                    if (adjustStats.LayerAdjusted)
                    {
                        stats.LayerAdjustedCount++;
                    }

                    if (adjustStats.ColorAdjusted)
                    {
                        stats.ColorAdjustedCount++;
                    }

                    entitiesToAdd.Add(text);
                }

                // 执行爆炸操作
                var explodedResult = ProcessExplodedEntities(CadBlkRef);
                if (!explodedResult.IsSuccess)
                {
                    return OpResult<ExplodeAsShownResult>.Fail(explodedResult.Message);
                }

                stats.LayerAdjustedCount += explodedResult.Data.LayerAdjustedCount;
                stats.ColorAdjustedCount += explodedResult.Data.ColorAdjustedCount;
                entitiesToAdd.AddRange(explodedResult.Data.Entities);
                if (entitiesToAdd.Count == 0)
                {
                    return OpResult<ExplodeAsShownResult>.Fail("无法获取爆炸后实体");
                }

                // 将所有实体添加到当前空间
                var addedEntities = ServiceTrans.AppendEntitiesToCurrentSpace(entitiesToAdd);
                if (addedEntities.Count == 0)
                {
                    return OpResult<ExplodeAsShownResult>.Fail("未能将实体添加到当前空间");
                }

                stats.EntityIds = addedEntities;

                // 删除原块参照
                CadBlkRef.Erase();

                return OpResult<ExplodeAsShownResult>.Success(stats);
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸块参照失败: {ex.Message}");
                return OpResult<ExplodeAsShownResult>.Fail(FormatExplodeErrorMessage(ex.Message));
            }
        }

        /// <summary>
        ///     删除块定义不含任何实体的图块参照
        /// </summary>
        /// <returns>删除成功返回 true</returns>
        public OpResult<bool> EraseIfEmptyDefinition()
        {
            try
            {
                if (CadBlkRef == null)
                {
                    return OpResult<bool>.Fail("块参照为空");
                }

                if (HasAttributes())
                {
                    return OpResult<bool>.Fail("空定义图块仍包含属性引用");
                }

                var blockDef = ServiceTrans.GetObject<BlockTableRecord>(CadBlkRef.BlockTableRecord);
                if (blockDef == null || !CadBlkRef.BlockTableRecord.IsValid)
                {
                    return OpResult<bool>.Fail("块定义无效");
                }

                var entityIds = ServiceTrans.GetChildObjects<DBObject>(blockDef);
                if (entityIds.Count > 0)
                {
                    return OpResult<bool>.Fail("块定义包含实体");
                }

                if (!CadBlkRef.IsWriteEnabled)
                {
                    CadBlkRef.UpgradeOpen();
                }

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


        // /// <summary>
        // /// 获取块参照的所有属性值
        // /// </summary>
        // /// <returns>属性标签和值的字典，如果块参照不存在或没有属性则返回空字典</returns>
        // public Dictionary<string, string> GetAllAttributeValues()
        // {
        //     var attributeValues = new Dictionary<string, string>();
        //     
        //     if (CadBlkRef == null)
        //     {
        //         return attributeValues;
        //     }
        //
        //     try
        //     {
        //         // 遍历块参照的所有属性
        //         foreach (ObjectId attId in CadBlkRef.AttributeCollection)
        //         {
        //             AttributeReference attRef = ServiceTrans.GetObject<AttributeReference>(attId, OpenMode.ForRead);
        //             if (attRef != null)
        //             {
        //                 // 添加属性标签和值到字典
        //                 attributeValues[attRef.Tag] = attRef.TextString;
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.WriteLine($"获取属性值失败: {ex.Message}");
        //     }
        //
        //     return attributeValues;
        // }
        //
        // /// <summary>
        // /// 获取指定标签的属性值
        // /// </summary>
        // /// <param name="tag">属性标签</param>
        // /// <returns>属性值，如果找不到则返回空字符串</returns>
        // public string GetAttributeValue(string tag)
        // {
        //     if (string.IsNullOrEmpty(tag) || CadBlkRef == null)
        //     {
        //         return string.Empty;
        //     }
        //
        //     try
        //     {
        //         // 遍历块参照的所有属性
        //         foreach (ObjectId attId in CadBlkRef.AttributeCollection)
        //         {
        //             AttributeReference attRef = ServiceTrans.GetObject<AttributeReference>(attId, OpenMode.ForRead);
        //             if (attRef != null && attRef.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
        //             {
        //                 // 找到匹配的属性标签，返回其值
        //                 return attRef.TextString;
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.WriteLine($"获取属性值失败: {ex.Message}");
        //     }
        //
        //     return string.Empty;
        // }
        /// <summary>
        ///     为图块生成Xclip边界
        /// </summary>
        /// <returns>操作结果，成功时返回创建的多段线 ObjectId</returns>
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
                {
                    pl.AddVertexAt(i, wcsPoints[i], 0, 0, 0);
                }

                var polyId = ServiceTrans.AppendEntityToModelSpace(pl);
                if (polyId == ObjectId.Null)
                    return OpResult<ObjectId>.Fail("无法将多段线添加到模型空间");

                Logger._.Info($"成功创建XClip边界多段线，顶点数: {wcsPoints.Count}，ID: {polyId}");
                return OpResult<ObjectId>.Success(polyId);
            }
            catch (Exception ex)
            {
                Logger._.Error($"生成Xclip边界时发生错误: {ex.Message}");
                return OpResult<ObjectId>.Fail($"生成Xclip边界时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     从 XClip 空间过滤器读取边界顶点，并变换到 WCS
        /// </summary>
        /// <param name="spatialFilter">XClip 空间过滤器</param>
        /// <param name="blockRef">块参照</param>
        /// <returns>变换后的边界顶点集合</returns>
        private OpResult<Point2dCollection> GetXClipBoundaryPointsWcs(SpatialFilter spatialFilter, BlockReference blockRef)
        {
            try
            {
                if (spatialFilter == null)
                    return OpResult<Point2dCollection>.Fail("XClip过滤器为空");

                if (blockRef == null)
                    return OpResult<Point2dCollection>.Fail("块参照为空");

                var definition = spatialFilter.Definition;
                var localPoints = definition.GetPoints();
                if (localPoints == null || localPoints.Count == 0)
                    return OpResult<Point2dCollection>.Fail("XClip边界点为空");

                // 裁剪创建时的逆块变换 + 当前块变换，才能正确处理旋转/移动后的图块
                var clipToWcs = spatialFilter.ClipSpaceToWorldCoordinateSystemTransform
                    .PreMultiplyBy(spatialFilter.OriginalInverseBlockTransform)
                    .PreMultiplyBy(blockRef.BlockTransform);
                var wcsPoints = new Point2dCollection();

                if (localPoints.Count > 2)
                {
                    for (var i = 0; i < localPoints.Count; i++)
                    {
                        wcsPoints.Add(TransformClipPointToWcs(localPoints[i], clipToWcs));
                    }
                }
                else
                {
                    var p1 = TransformClipPointToWcs(localPoints[0], clipToWcs);
                    var p2 = TransformClipPointToWcs(localPoints[1], clipToWcs);

                    wcsPoints.Add(p1);
                    wcsPoints.Add(new Point2d(p1.X, p2.Y));
                    wcsPoints.Add(p2);
                    wcsPoints.Add(new Point2d(p2.X, p1.Y));
                }

                return OpResult<Point2dCollection>.Success(wcsPoints);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取XClip边界点失败: {ex.Message}");
                return OpResult<Point2dCollection>.Fail($"获取XClip边界点失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     将 XClip 局部坐标点变换到 WCS
        /// </summary>
        /// <param name="localPoint">局部坐标点</param>
        /// <param name="clipToWcs">裁剪空间到 WCS 的完整变换矩阵</param>
        /// <returns>WCS 下的二维点</returns>
        private static Point2d TransformClipPointToWcs(Point2d localPoint, Matrix3d clipToWcs)
        {
            var pt3d = new Point3d(localPoint.X, localPoint.Y, 0);
            pt3d = pt3d.TransformBy(clipToWcs);
            return new Point2d(pt3d.X, pt3d.Y);
        }

        /// <summary>
        /// 获取图块参照的XClip过滤器
        /// </summary>
        /// <returns>XClip空间过滤器，如果不存在则返回null</returns>
        private SpatialFilter GetXClipFilter()
        {
            try
            {
                // 检查是否存在扩展字典
                if (CadBlkRef.ExtensionDictionary == ObjectId.Null)
                {
                    return null;
                }

                // 打开扩展字典
                var extDict = ServiceTrans.GetObject<DBDictionary>(CadBlkRef.ExtensionDictionary);
                if (extDict == null || !extDict.Contains("ACAD_FILTER"))
                {
                    return null;
                }

                // 打开ACAD_FILTER字典
                var filterDict = ServiceTrans.GetObject<DBDictionary>(extDict.GetAt("ACAD_FILTER"));
                if (filterDict == null || !filterDict.Contains("SPATIAL"))
                {
                    return null;
                }

                // 获取SPATIAL项
                var spatialId = filterDict.GetAt("SPATIAL");
                if (spatialId == ObjectId.Null)
                {
                    return null;
                }

                return ServiceTrans.GetObject<SpatialFilter>(spatialId);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取XClip过滤器失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     处理块参照的属性引用，将其转换为文本对象
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <returns>转换后的文本对象列表</returns>
        private List<DBText> ProcessAttributeReferences(BlockReference blockRef)
        {
            var textList = new List<DBText>();

            try
            {
                if (blockRef.AttributeCollection.Count == 0)
                {
                    return textList;
                }


                foreach (ObjectId attId in blockRef.AttributeCollection)
                {
                    try
                    {
                        if (attId == ObjectId.Null || !attId.IsValid)
                        {
                            continue;
                        }

                        var attRef = ServiceTrans.GetObject<AttributeReference>(attId);
                        if (attRef == null)
                        {
                            Logger._.Warn("\nCan't get attRef");
                            continue;
                        }

                        if (attRef.Invisible)
                        {
                            continue;
                        }


                        // 创建DBText并添加到列表
                        var text = ConvertAttributeToText(attRef);
                        textList.Add(text);
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"\n警告: 处理属性引用失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Warn($"\n警告: 处理属性引用集合失败: {ex.Message}");
            }

            return textList;
        }


        /// <summary>
        ///     将单个属性引用转换为文本对象
        /// </summary>
        /// <param name="attRef">属性引用</param>
        /// <returns>转换后的文本对象</returns>
        private DBText ConvertAttributeToText(AttributeReference attRef)
        {
            if (attRef == null)
            {
                Logger._.Warn("\n警告: 属性引用为空");
                return null;
            }

            // 创建文本对象
            var text = new DBText();

            try
            {
                // 复制基本属性
                PropertyUtils.MatchPropValues(text, attRef);
                // 使用ProcessEntityProperties方法处理图层和属性
                // SetChildPropsAsBlk(text, attRef);

                return text;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"\n警告: 转换属性到文本时发生异常: {ex.Message}");
                text?.Dispose();
                return null;
            }
        }

        /// <summary>
        ///     子实体属性继承统计
        /// </summary>
        private struct ChildPropAdjustStats
        {
            public bool LayerAdjusted;
            public bool ColorAdjusted;
        }

        /// <summary>
        ///     处理实体的图层和属性设置
        /// </summary>
        /// <param name="entChild">要修改的实体</param>
        /// <param name="entBlk">参考实体</param>
        /// <returns>图层与颜色是否被调整</returns>
        private ChildPropAdjustStats SetChildPropsAsBlk(Entity entChild, Entity entBlk)
        {
            var stats = new ChildPropAdjustStats();
            if (entChild == null || entBlk == null)
            {
                return stats;
            }

            try
            {
                if (MatchProp(entChild, entBlk, CadServiceManager.PropNames.Layer, CadServiceManager.Layers.Default)
                    .IsSuccess)
                {
                    stats.LayerAdjusted = true;
                }

                if (MatchProp(entChild, entBlk, CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByBlock)
                    .IsSuccess)
                {
                    stats.ColorAdjusted = true;
                }

                MatchProp(entChild, entBlk, CadServiceManager.PropNames.Linetype, CadServiceManager.Linetypes.ByBlock);
                MatchProp(entChild, entBlk, CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"\n警告: 处理实体属性时发生异常: {ex.Message}");
            }

            return stats;
        }

        public OpResult<object> MatchProp(Entity entTo, Entity entFr, string propName, object valueToFix) =>
            PropertyUtils.MatchPropValue(entTo, entFr, propName, (entT, entF) =>
            {
                var getValueTo = PropertyUtils.GetPropertyValue(entT, propName);
                if (!getValueTo.IsSuccess)
                {
                    return false;
                }

                return PropertyComparisonUtils.ValueEquals(getValueTo.Data, valueToFix);
            });

        /// <summary>
        ///     爆炸后实体批次结果
        /// </summary>
        private class ExplodedEntitiesBatch
        {
            public List<Entity> Entities { get; } = new List<Entity>();
            public int LayerAdjustedCount { get; set; }
            public int ColorAdjustedCount { get; set; }
        }

        /// <summary>
        ///     处理爆炸后的实体，将非属性定义的实体添加到实体列表
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <returns>爆炸实体及属性调整统计</returns>
        private OpResult<ExplodedEntitiesBatch> ProcessExplodedEntities(BlockReference blockRef)
        {
            try
            {
                // 1. 检查块参照状态
                if (!blockRef.ObjectId.IsValid)
                {
                    Logger._.Error("块参照无效");
                    return OpResult<ExplodedEntitiesBatch>.Fail("块参照无效");
                }

                // 2. 确保以写方式打开
                if (!blockRef.IsWriteEnabled)
                {
                    blockRef.UpgradeOpen();
                    if (!blockRef.IsWriteEnabled)
                    {
                        Logger._.Error("无法以写方式打开块参照");
                        return OpResult<ExplodedEntitiesBatch>.Fail("无法以写方式打开块参照");
                    }
                }

                // 3. 检查块定义
                var blockDef = ServiceTrans.GetObject<BlockTableRecord>(blockRef.BlockTableRecord);
                if (blockDef == null || !blockRef.BlockTableRecord.IsValid)
                {
                    Logger._.Error("块定义无效");
                    return OpResult<ExplodedEntitiesBatch>.Fail("块定义无效");
                }

                // 4. 检查图层是否锁定
                var layer = ServiceTrans.GetObject<LayerTableRecord>(blockRef.LayerId);
                if (layer != null && layer.IsLocked)
                {
                    Logger._.Error($"图块所在图层已锁定: {layer.Name}");
                    return OpResult<ExplodedEntitiesBatch>.Fail("图块所在图层已锁定");
                }

                // 5. 检查块定义中的实体数量
                var entityIds = ServiceTrans.GetChildObjects<DBObject>(blockDef);
                if (entityIds.Count == 0)
                {
                    Logger._.Error("块定义不含实体");
                    return OpResult<ExplodedEntitiesBatch>.Fail("块定义不含实体");
                }

                Logger._.Info($"块定义中的实体数量: {entityIds.Count}");


                // 6. 检查块参照的变换
                if (blockRef.ScaleFactors.X == 0 || blockRef.ScaleFactors.Y == 0 || blockRef.ScaleFactors.Z == 0)
                {
                    Logger._.Error("块参照的缩放比例无效");
                    return OpResult<ExplodedEntitiesBatch>.Fail("块参照的缩放比例无效");
                }

                if (double.IsNaN(blockRef.Rotation))
                {
                    Logger._.Error("块参照的旋转角度无效");
                    return OpResult<ExplodedEntitiesBatch>.Fail("块参照的旋转角度无效");
                }

                // 7. 执行爆炸操作
                var batch = new ExplodedEntitiesBatch();
                var explodedEntities = new DBObjectCollection();

                blockRef.Explode(explodedEntities);
                if (explodedEntities.Count == 0)
                {
                    Logger._.Error("爆炸后实体数量为0");
                    return OpResult<ExplodedEntitiesBatch>.Fail("爆炸后实体数量为0");
                }

                Logger._.Info($"爆炸后实体数量: {explodedEntities.Count}");

                // 8. 处理爆炸后的实体
                foreach (DBObject obj in explodedEntities)
                {
                    if (obj == null)
                    {
                        continue;
                    }

                    if (obj is AttributeDefinition)
                    {
                        obj.Dispose();
                    }
                    else if (obj is Entity entity)
                    {
                        var adjustStats = SetChildPropsAsBlk(entity, blockRef);
                        if (adjustStats.LayerAdjusted)
                        {
                            batch.LayerAdjustedCount++;
                        }

                        if (adjustStats.ColorAdjusted)
                        {
                            batch.ColorAdjustedCount++;
                        }

                        batch.Entities.Add(entity);
                    }
                    else
                    {
                        Logger._.Warn($"遇到未处理的对象类型: {obj.GetType().Name}");
                        if (obj is DBObject dbObj && !dbObj.IsDisposed)
                        {
                            dbObj.Dispose();
                        }
                    }
                }

                return OpResult<ExplodedEntitiesBatch>.Success(batch);
            }
            catch (Exception ex)
            {
                Logger._.Error($"爆炸块参照时发生异常: {ex.Message}");
                return OpResult<ExplodedEntitiesBatch>.Fail(FormatExplodeErrorMessage(ex.Message));
            }
        }

        /// <summary>
        ///     将爆炸相关异常消息转换为用户可读的简短说明
        /// </summary>
        /// <param name="message">原始异常消息</param>
        /// <returns>用户可读的错误说明</returns>
        private static string FormatExplodeErrorMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "爆炸图块时发生未知错误";
            }

            if (message.IndexOf("eOnLockedLayer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "图块所在图层已锁定";
            }

            if (message.IndexOf("eWasErased", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "图块已被删除";
            }

            if (message.IndexOf("eNotInDatabase", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "图块不在当前图形数据库中";
            }

            return message;
        }

        /// <summary>
        /// 尝试缩放视图到图块位置
        /// </summary>
        /// <returns>操作结果</returns>
        public OpResult<bool> TryZoomToBlock()
        {
            try
            {
                if (CadBlkRef == null)
                {
                    Logger._.Error("无法获取图块引用");
                    return OpResult<bool>.Fail("无法获取图块引用");
                }

                // 获取图块的精确位置
                Point3d blockPosition = CadBlkRef.Position;

                // 获取当前文档
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    Logger._.Error("无法获取当前文档");
                    return OpResult<bool>.Fail("无法获取当前文档");
                }

                try
                {
                    // 获取当前视图
                    using (ViewTableRecord view = doc.Editor.GetCurrentView())
                    {
                        // 设置视图中心到图块位置
                        view.CenterPoint = new Point2d(blockPosition.X, blockPosition.Y);
                        
                        // 保持当前高宽比
                        double ratio = view.Height / view.Width;
                        
                        // 设置合适的缩放比例
                        double viewWidth = 50.0; // 缩小视图宽度，更接近图块实际大小
                        view.Width = viewWidth;
                        view.Height = viewWidth * ratio;
                        
                        // 应用视图设置
                        doc.Editor.SetCurrentView(view);
                        
                        // 强制重新生成显示
                        doc.Editor.Regen();
                        
                        Logger._.Info($"视图已缩放到图块位置: ({blockPosition.X}, {blockPosition.Y})");
                        return OpResult<bool>.Success(true);
                    }
                }
                catch (Exception ex)
                {
                    Logger._.Error($"设置视图失败: {ex.Message}");
                    return OpResult<bool>.Fail($"设置视图失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"尝试缩放视图时发生错误: {ex.Message}");
                return OpResult<bool>.Fail($"尝试缩放视图时发生错误: {ex.Message}");
            }
        }
    }
}

