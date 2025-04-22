using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace ServiceACAD
{
    public class BlockService : IBlockService
    {
        public BlockService(ITransactionService serviceTrans, BlockReference blkRef)
        {
            ServiceTrans=serviceTrans;
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
        /// 检查块参照是否包含属性
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
        /// 爆炸块参照并将其属性转换为文本
        /// </summary>
        /// <returns>如果爆炸成功返回true，否则返回false</returns>
        public OpResult<List<ObjectId>> ExplodeWithAttributes()
        {


            if (CadBlkRef == null)
            {
                return OpResult<List<ObjectId>>.Fail($"CadBlkRef is null");
            }

            if (!HasAttributes())
            {
                return OpResult<List<ObjectId>>.Fail($"块参照不包含属性");
            }

            try
            {
                // 以写方式获取块参照
                if(!CadBlkRef.IsWriteEnabled)
                {
                    CadBlkRef.UpgradeOpen();
                }
                
                // 创建一个集合，用于收集需要添加到模型空间的实体
                List<Entity> entitiesToAdd = new List<Entity>();

                // 处理所有属性引用，转换为文本
                List<DBText> textList = ProcessAttributeReferences(CadBlkRef);
                if (textList.Count == 0)
                {
                    return OpResult<List<ObjectId>>.Fail($"未能从块参照中提取属性");
                }
                
                // 将文本添加到实体列表
                entitiesToAdd.AddRange(textList);

                // 执行爆炸操作（只处理非属性对象）
                ProcessExplodedEntities(CadBlkRef, entitiesToAdd);

                // 记录添加到当前空间前实体数量
                int entitiesCount = entitiesToAdd.Count;

                // 将所有实体添加到当前空间
                var addedEntities = ServiceTrans.AppendEntitiesToCurrentSpace(entitiesToAdd);
                if (addedEntities.Count == 0)
                {
                    return OpResult<List<ObjectId>>.Fail($"未能将实体添加到当前空间");
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
        /// 处理块参照的属性引用，将其转换为文本对象
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <returns>转换后的文本对象列表</returns>
        private List<DBText> ProcessAttributeReferences(BlockReference blockRef)
        {
            List<DBText> textList = new List<DBText>();
            
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
                        
                        AttributeReference attRef = ServiceTrans.GetObject<AttributeReference>(attId, OpenMode.ForRead);
                        if (attRef == null)
                        {
                            Debug.WriteLine("\nCan't get attRef");
                            continue;
                        }
                        
                        if (attRef.Invisible)
                        {
                            continue;
                        }
                        

                        // 创建DBText并添加到列表
                        DBText text = ConvertAttributeToText(attRef);
                        textList.Add(text);
                    }
                    catch (Exception ex)
                    {
                            Debug.WriteLine($"\n警告: 处理属性引用失败: {ex.Message}");
                    }
                }
                

            }
            catch (Exception ex)
            {
                    Debug.WriteLine($"\n警告: 处理属性引用集合失败: {ex.Message}");
            }
            
            return textList;
        }


        /// <summary>
        /// 将单个属性引用转换为文本对象
        /// </summary>
        /// <param name="attRef">属性引用</param>
        /// <returns>转换后的文本对象</returns>
        private DBText ConvertAttributeToText(AttributeReference attRef)
        {
            // 创建文本对象
            DBText text = new DBText();
            
            // 复制属性
            text.Position = attRef.Position;
            text.TextString = attRef.TextString;
            text.Height = attRef.Height;
            text.WidthFactor = attRef.WidthFactor;
            text.Rotation = attRef.Rotation;
            text.TextStyleId = attRef.TextStyleId;
            text.Layer = attRef.Layer;
            text.LineWeight = attRef.LineWeight;
            text.Linetype = attRef.Linetype;
            text.LinetypeScale = attRef.LinetypeScale;
            text.Visible = attRef.Visible;
            text.ColorIndex = attRef.ColorIndex;

            // 处理对齐方式
            text.HorizontalMode = attRef.HorizontalMode;
            text.VerticalMode = attRef.VerticalMode;
            
            if (attRef.Justify != AttachmentPoint.BaseLeft)
            {
                text.Justify = attRef.Justify;
                text.AlignmentPoint = attRef.AlignmentPoint;
            }

            return text;
        }

        /// <summary>
        /// 处理爆炸后的实体，将非属性定义的实体添加到实体列表
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <param name="entitiesToAdd">实体收集列表</param>
        private void ProcessExplodedEntities(BlockReference blockRef, List<Entity> entitiesToAdd)
        {
            try
            {
                DBObjectCollection explodedEntities = new DBObjectCollection();
                blockRef.Explode(explodedEntities);
                

                int entityCount = 0;
                int attributeDefCount = 0;
                
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
                            entitiesToAdd.Add(entity);
                            entityCount++;
                        }
                        else
                        {
                            Debug.WriteLine($"\n警告: 遇到未处理的对象类型 {obj.GetType().Name}");
                            
                            if (obj is DBObject dbObj && !dbObj.IsDisposed)
                            {
                                dbObj.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                       Debug.WriteLine($"\n警告: 处理爆炸实体时发生异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
               Debug.WriteLine($"\n警告: 执行块参照爆炸时发生异常: {ex.Message}");
                    Debug.WriteLine($"\n{ex.StackTrace}");
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
