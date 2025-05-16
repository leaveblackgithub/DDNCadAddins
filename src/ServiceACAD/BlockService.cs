using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

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

            // if (!HasAttributes())
            // {
            //     return OpResult<List<ObjectId>>.Fail("块参照不包含属性");
            // }

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
                // if (textList.Count == 0)
                // {
                //     return OpResult<List<ObjectId>>.Fail("未能从块参照中提取属性");
                // }

                // 将文本添加到实体列表
                entitiesToAdd.AddRange(textList);

                // 执行爆炸操作
                var explodedResult=ProcessExplodedEntities(CadBlkRef);
                if (!explodedResult.IsSuccess)
                {
                    return OpResult<List<ObjectId>>.Fail(explodedResult.Message);
                }
                entitiesToAdd.AddRange(explodedResult.Data);

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
                        SetChildPropsAsBlk(text,CadBlkRef);
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
        ///     处理实体的图层和属性设置
        /// </summary>
        /// <param name="entChild">要修改的实体</param>
        /// <param name="entBlk">参考实体</param>
        private void SetChildPropsAsBlk(Entity entChild, Entity entBlk)
        {
            if (entChild == null || entBlk == null)
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

                MatchProp(entChild, entBlk, CadServiceManager.StrLayer, CadServiceManager.Layer0);
                // 处理BYBLOCK颜色
                // var nameColor = "ColorIndex";
                // if (HasProperty(entFr, nameColor) && HasProperty(entTo, nameColor) &&
                //     (entFr is AttributeReference || entTo.ColorIndex == 0))
                // {
                //     entTo.ColorIndex = entFr.ColorIndex;
                // }
                MatchProp(entChild, entBlk, CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByBlock);
                // 处理BYBLOCK线型
                // var nameLinetype = "Linetype";
                // if (HasProperty(entFr, nameLinetype) && HasProperty(entTo, nameLinetype) &&
                //     (entFr is AttributeReference || entTo.Linetype == "BYBLOCK"))
                //
                // {
                //     entTo.LinetypeId = entFr.LinetypeId;
                // }
                MatchProp(entChild, entBlk, CadServiceManager.StrLinetype, CadServiceManager.StrByBlock);

                // 处理BYBLOCK线宽
                // if (HasProperty(entFr, "LineWeight") && HasProperty(entTo, "LineWeight") &&
                //     (entFr is AttributeReference || entTo.LineWeight == LineWeight.ByBlock))
                //
                // {
                //     entTo.LineWeight = entFr.LineWeight;
                // }
                MatchProp(entChild, entBlk, CadServiceManager.StrLineWeight, LineWeight.ByBlock);
            }
            catch (Exception ex)
            {
                Logger._.Warn($"\n警告: 处理实体属性时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        ///     比较两个值是否相等，支持不同类型之间的比较
        /// </summary>
        /// <param name="value1">第一个值</param>
        /// <param name="value2">第二个值</param>
        /// <returns>如果两个值相等返回true，否则返回false</returns>
        private static bool ValueEquals(object value1, object value2)
        {
            // 处理null值的情况
            if (value1 == null && value2 == null)
            {
                return true;
            }

            if (value1 == null || value2 == null)
            {
                return false;
            }

            // 处理字符串和数值类型的比较
            if (value1 is string strValue1 && value2 is string strValue2)
            {
                return string.Equals(strValue1, strValue2, StringComparison.OrdinalIgnoreCase);
            }

            // 如果两个值类型相同，直接比较
            if (value1.GetType() == value2.GetType())
            {
                return value1.Equals(value2);
            }

            // 尝试将值转换为相同类型后比较
            try
            {
                var convertedValue = Convert.ChangeType(value1, value2.GetType());
                return convertedValue.Equals(value2);
            }
            catch
            {
                return false;
            }
        }

        public OpResult<object> MatchProp(Entity entTo, Entity entFr, string propName, object valueToFix) =>
            PropertyUtils.MatchPropValue(entTo, entFr, propName, (entT, entF) =>
            {
                var getValueTo = PropertyUtils.GetPropertyValue(entT, propName);
                if (!getValueTo.IsSuccess)
                {
                    return false;
                }

                return ValueEquals(getValueTo.Data, valueToFix);
            });

        /// <summary>
        ///     处理爆炸后的实体，将非属性定义的实体添加到实体列表
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <param name="entitiesToAdd">实体收集列表</param>
        private OpResult<List<Entity>> ProcessExplodedEntities(BlockReference blockRef)
        {
            try
            {
                var entitiesToAdd = new List<Entity>();
                var explodedEntities = new DBObjectCollection();
                blockRef.Explode(explodedEntities);

                var entityCount = 0;
                var attributeDefCount = 0;

                foreach (DBObject obj in explodedEntities)
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

                return OpResult<List<Entity>>.Success(entitiesToAdd);
            }
            catch (Exception ex)
            {
                return OpResult<List<Entity>>.Fail(ex.Message);
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
