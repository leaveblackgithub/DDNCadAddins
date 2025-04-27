using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.AutoCAD.Colors;

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

            using (var tr = CadBlkRef.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    // 打开扩展字典
                    var extDict = tr.GetObject(CadBlkRef.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
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
                    var filterDict = tr.GetObject(extDict.GetAt("ACAD_FILTER"), OpenMode.ForRead) as DBDictionary;
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
                finally
                {
                    tr.Commit();
                }
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

        /// <summary>
        ///     爆炸块参照并将其属性转换为文本
        /// </summary>
        /// <returns>如果爆炸成功返回true，否则返回false</returns>
        public OpResult<List<ObjectId>> ExplodeWithAttributes()
        {
            if (CadBlkRef == null)
            {
                return OpResult<List<ObjectId>>.Fail("CadBlkRef is null");
            }

            if (!HasAttributes())
            {
                return OpResult<List<ObjectId>>.Fail("块参照不包含属性");
            }

            try
            {
                // 以写方式获取块参照
                if (!CadBlkRef.IsWriteEnabled)
                {
                    CadBlkRef.UpgradeOpen();
                }

                // 创建一个集合，用于收集需要添加到模型空间的实体
                var entitiesToAdd = new List<Entity>();

                // 处理所有属性引用，转换为文本
                var textList = ProcessAttributeReferences(CadBlkRef);
                if (textList.Count == 0)
                {
                    return OpResult<List<ObjectId>>.Fail("未能从块参照中提取属性");
                }

                // 将文本添加到实体列表
                entitiesToAdd.AddRange(textList);

                // 执行爆炸操作（只处理非属性对象）
                ProcessExplodedEntities(CadBlkRef, entitiesToAdd);

                // 记录添加到当前空间前实体数量
                var entitiesCount = entitiesToAdd.Count;

                // 将所有实体添加到当前空间
                var addedEntities = ServiceTrans.AppendEntitiesToCurrentSpace(entitiesToAdd);
                if (addedEntities.Count == 0)
                {
                    return OpResult<List<ObjectId>>.Fail("未能将实体添加到当前空间");
                }

                // 删除原块参照
                CadBlkRef.Erase();

                return OpResult<List<ObjectId>>.Success(addedEntities);
            }
            catch (Exception ex)
            {
                return OpResult<List<ObjectId>>.Fail($"爆炸块参照失败: {ex.Message}");
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
                text.Position = attRef.Position;
                text.TextString = attRef.TextString;
                text.Height = attRef.Height;
                text.WidthFactor = attRef.WidthFactor;
                text.Rotation = attRef.Rotation;
                text.TextStyleId = attRef.TextStyleId;
                text.Visible = attRef.Visible;

                // 处理对齐方式
                text.HorizontalMode = attRef.HorizontalMode;
                text.VerticalMode = attRef.VerticalMode;

                if (attRef.Justify != AttachmentPoint.BaseLeft)
                {
                    text.Justify = attRef.Justify;
                    text.AlignmentPoint = attRef.AlignmentPoint;
                }

                // 使用ProcessEntityProperties方法处理图层和属性
                SetChildPropsAsBlk(text, attRef);

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
        ///     处理实体的图层和属性设置
        /// </summary>
        /// <param name="entTo">要修改的实体</param>
        /// <param name="entFr">参考实体</param>
        private void SetChildPropsAsBlk(Entity entTo, Entity entFr)
        {
            if (entTo == null || entFr == null)
            {
                return;
            }

            try
            {
                // 处理0图层的对象
                // var nameLayer = "Layer";
                // if (HasProperty(entFr, nameLayer) && HasProperty(entTo, nameLayer) &&
                //     (entFr is AttributeReference || entTo.Layer == "0"))
                // {
                //     entTo.Layer = entFr.Layer;
                // }

                MatchProp(entTo, entFr, CadServiceManager.StrLayer, CadServiceManager.Layer0);
                // 处理BYBLOCK颜色
                // var nameColor = "ColorIndex";
                // if (HasProperty(entFr, nameColor) && HasProperty(entTo, nameColor) &&
                //     (entFr is AttributeReference || entTo.ColorIndex == 0))
                // {
                //     entTo.ColorIndex = entFr.ColorIndex;
                // }
                MatchProp(entTo, entFr, CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByBlock);
                // 处理BYBLOCK线型
                // var nameLinetype = "Linetype";
                // if (HasProperty(entFr, nameLinetype) && HasProperty(entTo, nameLinetype) &&
                //     (entFr is AttributeReference || entTo.Linetype == "BYBLOCK"))
                //
                // {
                //     entTo.LinetypeId = entFr.LinetypeId;
                // }
                MatchProp(entTo, entFr, CadServiceManager.StrLinetype, CadServiceManager.StrByBlock);

                // 处理BYBLOCK线宽
                // if (HasProperty(entFr, "LineWeight") && HasProperty(entTo, "LineWeight") &&
                //     (entFr is AttributeReference || entTo.LineWeight == LineWeight.ByBlock))
                //
                // {
                //     entTo.LineWeight = entFr.LineWeight;
                // }
                MatchProp(entTo, entFr, CadServiceManager.StrLineWeight, LineWeight.ByBlock);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"\n警告: 处理实体属性时发生异常: {ex.Message}");
            }
        }

        public OpResult<object> MatchProp(Entity entTo, Entity entFr, string propName, object valueToFix) =>
            ServiceACAD.PropertyUtils.MatchPropValues(entFr, entTo, propName, (entT, entF) =>
            {
                var getValueTo = PropertyUtils.GetPropertyValue(entT, propName);
                return !(entF is AttributeReference) ||
                       (getValueTo.IsSuccess & getValueTo.Data.Equals(valueToFix));
            });

        /// <summary>
        ///     处理爆炸后的实体，将非属性定义的实体添加到实体列表
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <param name="entitiesToAdd">实体收集列表</param>
        private void ProcessExplodedEntities(BlockReference blockRef, List<Entity> entitiesToAdd)
        {
            try
            {
                var explodedEntities = new DBObjectCollection();
                blockRef.Explode(explodedEntities);

                var entityCount = 0;
                var attributeDefCount = 0;

                foreach (DBObject obj in explodedEntities)
                {
                    try
                    {
                        if (obj == null)
                        {
                            continue;
                        }

                        if (obj is AttributeDefinition)
                        {
                            // 丢弃属性定义
                            obj.Dispose();
                            attributeDefCount++;
                        }
                        else if (obj is Entity entity)
                        {
                            // 处理实体的图层和属性
                            SetChildPropsAsBlk(entity, blockRef);
                            entitiesToAdd.Add(entity);
                            entityCount++;
                        }
                        else
                        {
                            Logger._.Warn($"\n警告: 遇到未处理的对象类型 {obj.GetType().Name}");

                            if (obj is DBObject dbObj && !dbObj.IsDisposed)
                            {
                                dbObj.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"\n警告: 处理爆炸实体时发生异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Warn($"\n警告: 执行块参照爆炸时发生异常: {ex.Message}");
                Logger._.Error($"\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 在当前空间创建块参照
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="insertPt">插入点</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="color">颜色</param>
        /// <param name="linetype">线型</param>
        /// <returns>创建成功的块参照ObjectId，失败返回ObjectId.Null</returns>
        public ObjectId CreateBlockRefInCurrentSpace(string name, Point3d insertPt, string layerName, Color color, string linetype)
        {
            if (string.IsNullOrEmpty(name))
            {
                Logger._.Warn("块名称不能为空");
                return ObjectId.Null;
            }

            try
            {
                // 获取块表记录
                var btr = ServiceTrans.GetBlockTableRecord(name);
                if (btr == null)
                {
                    Logger._.Warn($"块 {name} 不存在");
                    return ObjectId.Null;
                }

                // 创建块参照
                var blkRef = new BlockReference(insertPt, btr.ObjectId)
                {
                    Layer = layerName,
                    Color = color,
                    Linetype = linetype
                };

                // 将块参照添加到当前空间
                return ServiceTrans.AppendEntityToCurrentSpace(blkRef);
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建块参照失败: {ex.Message}");
                return ObjectId.Null;
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
    }
}
