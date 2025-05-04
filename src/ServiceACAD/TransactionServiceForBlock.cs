using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务块部分，提供块管理功能
    /// </summary>
    public class TransactionServiceForBlock : ITransactionServiceForBlock
    {
        private readonly TransactionService _transactionService;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        public TransactionServiceForBlock(TransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        ///     获取块服务
        /// </summary>
        /// <param name="objectId">块引用ID</param>
        /// <returns>块服务实例</returns>
        public IBlockService GetBlockService(ObjectId objectId)
        {
            if (objectId.IsNull)
            {
                return null;
            }

            if (_transactionService.BlockServiceDict.TryGetValue(objectId, out var blockService))
            {
                return blockService;
            }

            try
            {
                if (!(_transactionService.GetObject<DBObject>(objectId) is BlockReference blkRef))
                {
                    return null;
                }

                blockService = new BlockService(_transactionService, blkRef);
                _transactionService.BlockServiceDict[objectId] = blockService;
                return blockService;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块服务异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     创建块定义
        /// </summary>
        /// <param name="entities">要包含在块中的实体集合</param>
        /// <param name="blockName">块名称</param>
        /// <returns>创建的块的ObjectId</returns>
        public ObjectId CreateBlockDef(ICollection<Entity> entities, string blockName = "")
        {
            try
            {
                if (entities == null || entities.Count == 0)
                {
                    Logger._.Error("创建块定义失败：实体集合为空");
                    return ObjectId.Null;
                }

                // 获取块表
                var bt = _transactionService.GetBlockTable(OpenMode.ForWrite);
                if (bt == null)
                {
                    Logger._.Error("创建块定义失败：获取块表失败");
                    return ObjectId.Null;
                }

                // 生成唯一的块名
                if (string.IsNullOrEmpty(blockName))
                {
                    blockName = $"Block_{Guid.NewGuid():N}";
                }
                else
                {
                    // 确保块名唯一
                    var suffix = 1;
                    var originalName = blockName;
                    while (bt.Has(blockName))
                    {
                        blockName = $"{originalName}_{suffix++}";
                    }
                }

                // 创建新的块表记录
                var btr = new BlockTableRecord();
                btr.Name = blockName;

                // 添加块表记录到块表
                var btrId = bt.Add(btr);
                _transactionService.AddNewlyCreatedDBObject(btr, true);

                // 将实体添加到块表记录
                foreach (var entity in entities)
                {
                    btr.AppendEntity(entity);
                    _transactionService.AddNewlyCreatedDBObject(entity, true);
                }

                Logger._.Info($"创建块定义成功：{blockName}");
                return btrId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建块定义失败：{ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     在当前空间创建块参照
        /// </summary>
        /// <param name="blkDefId">块定义ID</param>
        /// <param name="insertPt">插入点</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="linetype">线型</param>
        /// <returns>创建成功的块参照ObjectId，失败返回ObjectId.Null</returns>
        public ObjectId CreateBlockRefInCurrentSpace(ObjectId blkDefId, Point3d insertPt = default(Point3d),
            string layerName = "", short colorIndex = 256, string linetype = "BYLAYER")
        {
            try
            {
                if (blkDefId.IsNull)
                {
                    Logger._.Error("创建块参照失败：块定义ID为空");
                    return ObjectId.Null;
                }

                // 获取当前空间
                using (var currentSpace = _transactionService.GetCurrentSpace(OpenMode.ForWrite))
                {
                    if (currentSpace == null)
                    {
                        Logger._.Error("创建块参照失败：获取当前空间失败");
                        return ObjectId.Null;
                    }

                    // 创建块参照
                    var blockRef = new BlockReference(insertPt, blkDefId);

                    // 设置块参照属性
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        blockRef.Layer = _transactionService.Style.GetValidLayerName(layerName);
                    }

                    if (colorIndex != 256)
                    {
                        blockRef.ColorIndex = _transactionService.Style.GetValidColorIndex(colorIndex);
                    }

                    if (linetype != "BYLAYER")
                    {
                        blockRef.Linetype = _transactionService.Style.GetValidLineTypeName(linetype);
                    }

                    // 添加块参照到当前空间
                    var blockRefId = currentSpace.AppendEntity(blockRef);
                    _transactionService.AddNewlyCreatedDBObject(blockRef, true);

                    // 处理块中的属性定义
                    var blockDef = _transactionService.GetObject<BlockTableRecord>(blkDefId);
                    if (blockDef != null)
                    {
                        // 获取块定义中的所有属性定义
                        var attDefIds = _transactionService.GetChildObjects<AttributeDefinition>(blockDef);
                        if (attDefIds.Count > 0)
                        {
                            foreach (var attDefId in attDefIds)
                            {
                                var attDef = _transactionService.GetObject<AttributeDefinition>(attDefId);
                                if (attDef != null)
                                {
                                    // 创建属性引用
                                    var attRef = new AttributeReference();
                                    attRef.SetAttributeFromBlock(attDef, blockRef.BlockTransform);
                                    attRef.Position = attDef.Position.TransformBy(blockRef.BlockTransform);
                                    attRef.TextString = attDef.TextString;

                                    // 添加属性引用到块参照
                                    blockRef.AttributeCollection.AppendAttribute(attRef);
                                    _transactionService.AddNewlyCreatedDBObject(attRef, true);
                                }
                            }
                        }
                    }

                    Logger._.Info("创建块参照成功");
                    return blockRefId;
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建块参照失败：{ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     为块参照添加多个属性并赋值
        /// </summary>
        /// <param name="blkDefId">块定义ID</param>
        /// <param name="blkRefId">块参照ID</param>
        /// <param name="attributeValues">属性Tag和对应的值字典</param>
        /// <returns>是否成功添加属性</returns>
        public bool AddAttributesToBlockReference(ObjectId blkDefId,
            ObjectId blkRefId,
            Dictionary<string, Dictionary<string, object>> attributeValues)
        {
            try
            {
                if (blkDefId.IsNull || blkRefId.IsNull || attributeValues == null || attributeValues.Count == 0)
                {
                    Logger._.Error("添加属性失败：参数无效");
                    return false;
                }

                // 获取块定义
                var blockDef = _transactionService.GetObject<BlockTableRecord>(blkDefId);
                if (blockDef == null)
                {
                    Logger._.Error("添加属性失败：获取块定义失败");
                    return false;
                }

                // 获取块参照
                var blockRef = _transactionService.GetObject<BlockReference>(blkRefId, OpenMode.ForWrite);
                if (blockRef == null)
                {
                    Logger._.Error("添加属性失败：获取块参照失败");
                    return false;
                }

                // 获取块定义中的所有属性定义
                var attDefIds = _transactionService.GetChildObjects<AttributeDefinition>(blockDef);
                var attDefDict = new Dictionary<string, AttributeDefinition>();

                foreach (var attDefId in attDefIds)
                {
                    var attDef = _transactionService.GetObject<AttributeDefinition>(attDefId);
                    if (attDef != null)
                    {
                        attDefDict[attDef.Tag] = attDef;
                    }
                }

                // 处理属性值
                foreach (var attValPair in attributeValues)
                {
                    var attTag = attValPair.Key;
                    var attProps = attValPair.Value;

                    if (attDefDict.TryGetValue(attTag, out var attDef))
                    {
                        // 检查块参照中是否已存在该属性
                        AttributeReference existingAttRef = null;
                        foreach (ObjectId attRefId in blockRef.AttributeCollection)
                        {
                            var tempAttRef =
                                _transactionService.GetObject<AttributeReference>(attRefId, OpenMode.ForWrite);
                            if (tempAttRef != null && tempAttRef.Tag == attTag)
                            {
                                existingAttRef = tempAttRef;
                                break;
                            }
                        }

                        // 如果属性已存在，则更新它
                        if (existingAttRef != null)
                        {
                            // 设置属性值
                            foreach (var prop in attProps)
                            {
                                switch (prop.Key.ToLower())
                                {
                                    case "textstring":
                                        existingAttRef.TextString = prop.Value.ToString();
                                        break;
                                    case "height":
                                        existingAttRef.Height = Convert.ToDouble(prop.Value);
                                        break;
                                    case "rotation":
                                        existingAttRef.Rotation = Convert.ToDouble(prop.Value);
                                        break;
                                    case "position":
                                        if (prop.Value is Point3d point)
                                        {
                                            existingAttRef.Position = point;
                                        }

                                        break;
                                    // 添加其他属性...
                                }
                            }
                        }
                        else
                        {
                            // 创建新的属性引用
                            var attRef = new AttributeReference();
                            attRef.SetAttributeFromBlock(attDef, blockRef.BlockTransform);

                            // 设置属性值
                            foreach (var prop in attProps)
                            {
                                switch (prop.Key.ToLower())
                                {
                                    case "textstring":
                                        attRef.TextString = prop.Value.ToString();
                                        break;
                                    case "height":
                                        attRef.Height = Convert.ToDouble(prop.Value);
                                        break;
                                    case "rotation":
                                        attRef.Rotation = Convert.ToDouble(prop.Value);
                                        break;
                                    case "position":
                                        if (prop.Value is Point3d point)
                                        {
                                            attRef.Position = point;
                                        }

                                        break;
                                    // 添加其他属性...
                                }
                            }

                            // 如果没有设置TextString，则使用默认值
                            if (!attProps.ContainsKey("TextString") && !attProps.ContainsKey("textstring"))
                            {
                                attRef.TextString = attDef.TextString;
                            }

                            // 添加属性引用到块参照
                            blockRef.AttributeCollection.AppendAttribute(attRef);
                            _transactionService.AddNewlyCreatedDBObject(attRef, true);
                        }
                    }
                    else
                    {
                        Logger._.Warn($"属性定义 {attTag} 不存在于块定义中");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger._.Error($"添加属性失败：{ex.Message}");
                return false;
            }
        }
    }
}
