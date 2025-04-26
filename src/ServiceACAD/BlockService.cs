using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Diagnostics;
using System.Collections.Generic;

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
                            Debug.WriteLine("\nCan't get attRef");
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
        ///     将单个属性引用转换为文本对象
        /// </summary>
        /// <param name="attRef">属性引用</param>
        /// <returns>转换后的文本对象</returns>
        private DBText ConvertAttributeToText(AttributeReference attRef)
        {
            if (attRef == null)
            {
                Debug.WriteLine("\n警告: 属性引用为空");
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
                Debug.WriteLine($"\n警告: 转换属性到文本时发生异常: {ex.Message}");
                text?.Dispose();
                return null;
            }
        }

        /// <summary>
        ///     处理实体的图层和属性设置
        /// </summary>
        /// <param name="targetEntity">要修改的实体</param>
        /// <param name="referenceEntity">参考实体</param>
        private void SetChildPropsAsBlk(Entity targetEntity, Entity referenceEntity)
        {
            if (targetEntity == null || referenceEntity == null)
            {
                return;
            }

            try
            {
                // 处理0图层的对象
                var nameLayer = "Layer";
                if (HasProperty(referenceEntity, nameLayer) && HasProperty(targetEntity, nameLayer) &&
                    (referenceEntity is AttributeReference || targetEntity.Layer == "0"))
                {
                    targetEntity.Layer = referenceEntity.Layer;
                }

                // 处理BYBLOCK颜色
                var nameColor = "ColorIndex";
                if (HasProperty(referenceEntity, nameColor) && HasProperty(targetEntity, nameColor) &&
                    (referenceEntity is AttributeReference || targetEntity.ColorIndex == 0))
                {
                    targetEntity.ColorIndex = referenceEntity.ColorIndex;
                }

                // 处理BYBLOCK线型
                var nameLinetype = "Linetype";
                if (HasProperty(referenceEntity, nameLinetype) && HasProperty(targetEntity, nameLinetype) &&
                    (referenceEntity is AttributeReference || targetEntity.Linetype == "BYBLOCK"))

                {
                    targetEntity.LinetypeId = referenceEntity.LinetypeId;
                }


                // 处理BYBLOCK线型比例
                var nameLtScale = "LinetypeScale";
                if (HasProperty(referenceEntity, nameLtScale) && HasProperty(targetEntity, nameLtScale) &&
                    (referenceEntity is AttributeReference || targetEntity.LinetypeScale == 1.0))

                {
                    targetEntity.LinetypeScale = CadBlkRef.LinetypeScale;
                }


                // 处理BYBLOCK线宽
                if (HasProperty(referenceEntity, "LineWeight") && HasProperty(targetEntity, "LineWeight") &&
                    (referenceEntity is AttributeReference || targetEntity.LineWeight == LineWeight.ByBlock))

                {
                    targetEntity.LineWeight = referenceEntity.LineWeight;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"\n警告: 处理实体属性时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        ///     检查对象是否具有指定属性
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果对象具有该属性返回true，否则返回false</returns>
        public bool HasProperty(object obj, string propertyName)
        {
            if (obj == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            try
            {
                var property = obj.GetType().GetProperty(propertyName);
                return property != null;
            }
            catch
            {
                return false;
            }
        }

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

        /// <summary>
        /// 创建用于测试爆炸命令的测试块
        /// </summary>
        /// <returns>创建的测试块的ObjectId</returns>
        public ObjectId CreateTestBlockForExplodeCommand()
        {
            if (CadBlkRef == null)
            {
                return ObjectId.Null;
            }

            try
            {
                // 创建测试实体
                var entities = new List<Entity>();
                CreateTestEntities(entities);

                // 使用事务服务创建块
                return ServiceTrans.CreateBlock(entities, "TestBlockForExplode");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"\n警告: 创建测试块时发生异常: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// 创建测试实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        private void CreateTestEntities(List<Entity> entities)
        {
            // 1. 直线1：0图层，BYBLOCK颜色，BYBLOCK线型，默认线型比例，BYBLOCK线宽
            var line1 = new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
            line1.Layer = "0";
            line1.ColorIndex = 0; // BYBLOCK
            line1.Linetype = "BYBLOCK";
            line1.LinetypeScale = 1.0;
            line1.LineWeight = LineWeight.ByBlock;
            entities.Add(line1);

            // 2. 直线2：非0图层，特定颜色，特定线型，自定义线型比例，特定线宽
            var line2 = new Line(new Point3d(0, 10, 0), new Point3d(10, 10, 0));
            line2.Layer = "TestLayer";
            line2.ColorIndex = 1; // 红色
            line2.Linetype = "TestLinetype";
            line2.LinetypeScale = 2.0;
            line2.LineWeight = LineWeight.LineWeight050;
            entities.Add(line2);

            // 3. 圆1：0图层，BYLAYER颜色，BYBLOCK线型，默认线型比例，BYBLOCK线宽
            var circle1 = new Circle(new Point3d(20, 0, 0), Vector3d.ZAxis, 5);
            circle1.Layer = "0";
            circle1.ColorIndex = 256; // BYLAYER
            circle1.Linetype = "BYBLOCK";
            circle1.LinetypeScale = 1.0;
            circle1.LineWeight = LineWeight.ByBlock;
            entities.Add(circle1);

            // 4. 圆2：非0图层，BYBLOCK颜色，特定线型，自定义线型比例，特定线宽
            var circle2 = new Circle(new Point3d(20, 10, 0), Vector3d.ZAxis, 5);
            circle2.Layer = "TestLayer";
            circle2.ColorIndex = 0; // BYBLOCK
            circle2.Linetype = "TestLinetype";
            circle2.LinetypeScale = 0.5;
            circle2.LineWeight = LineWeight.LineWeight030;
            entities.Add(circle2);

            // 5. 文本1：0图层，特定颜色，BYBLOCK线型，默认线型比例，BYBLOCK线宽
            var text1 = new DBText();
            text1.Position = new Point3d(30, 0, 0);
            text1.TextString = "Text1";
            text1.Height = 2.5;
            text1.Layer = "0";
            text1.ColorIndex = 3; // 绿色
            text1.Linetype = "BYBLOCK";
            text1.LinetypeScale = 1.0;
            text1.LineWeight = LineWeight.ByBlock;
            entities.Add(text1);

            // 6. 文本2：非0图层，BYBLOCK颜色，特定线型，自定义线型比例，特定线宽
            var text2 = new DBText();
            text2.Position = new Point3d(30, 10, 0);
            text2.TextString = "Text2";
            text2.Height = 2.5;
            text2.Layer = "TestLayer";
            text2.ColorIndex = 0; // BYBLOCK
            text2.Linetype = "TestLinetype";
            text2.LinetypeScale = 1.5;
            text2.LineWeight = LineWeight.LineWeight070;
            entities.Add(text2);
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
