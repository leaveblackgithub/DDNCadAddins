using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务实现，提供与事务相关的操作
    /// </summary>
    public class TransactionService : ITransactionService
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="transaction">事务对象</param>
        public TransactionService(Transaction transaction)
        {
            CadTrans = transaction;
            BlockServiceDict = new Dictionary<ObjectId, IBlockService>();
        }

        /// <summary>
        ///     事务对象
        /// </summary>
        public Transaction CadTrans { get; }

        /// <summary>
        ///     块服务缓存字典
        /// </summary>
        public IDictionary<ObjectId, IBlockService> BlockServiceDict { get; }

        /// <summary>
        ///     获取数据库对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="objectId">对象ID</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>数据库对象</returns>
        public T GetObject<T>(ObjectId objectId, OpenMode openMode = OpenMode.ForRead) where T : DBObject
        {
            try
            {
                return (T)CadTrans.GetObject(objectId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取对象失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取块服务
        /// </summary>
        /// <param name="objectId">块引用ID</param>
        /// <returns>块服务实例</returns>
        public IBlockService GetBlockService(ObjectId objectId)
        {
            try
            {
                if (!objectId.IsValid)
                {
                    return null;
                }

                // 检查是否已存在缓存的块服务
                if (BlockServiceDict.TryGetValue(objectId, out var serviceBlk))
                {
                    return serviceBlk;
                }

                // 获取块引用对象
                var blockRef = GetObject<BlockReference>(objectId);
                if (blockRef == null)
                {
                    Logger._.Error($"获取块引用失败，ObjectId: {objectId}");
                    return null;
                }

                // 创建块服务实例
                var blockService = new BlockService(this, blockRef);

                // 添加到缓存字典
                BlockServiceDict[objectId] = blockService;

                return blockService;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块服务失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     向模型空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        public ObjectId AppendEntityToModelSpace(Entity entity)
        {
            try
            {
                var objectId = AppendEntityToBlockTableRecord(GetModelSpace(OpenMode.ForWrite), entity);
                return objectId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"向模型空间添加实体失败: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param
        /// <summary>
        ///     向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        public ObjectId AppendEntityToBlockTableRecord(BlockTableRecord blockTableRecord, Entity entity)
        {
            try
            {
                if (!blockTableRecord.IsWriteEnabled)
                {
                    blockTableRecord.UpgradeOpen();
                }

                var objectId = blockTableRecord.AppendEntity(entity);
                CadTrans.AddNewlyCreatedDBObject(entity, true);
                return objectId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"向块表记录添加实体失败: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     获取模型空间
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>模型空间块表记录</returns>
        public BlockTableRecord GetModelSpace(OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                return GetBlockTableRecord(BlockTableRecord.ModelSpace, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取模型空间失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取块表记录ID
        /// </summary>
        /// <param name="name">块名称</param>
        /// <returns>块表记录ID</returns>
        public ObjectId GetBlockTableRecordId(string name)
        {
            try
            {
                var blockTable = GetBlockTable();
                return blockTable[name];
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块表记录ID失败: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     获取块表记录
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表记录</returns>
        public BlockTableRecord GetBlockTableRecord(string name, OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                return GetObject<BlockTableRecord>(GetBlockTableRecordId(name), openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块表记录失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取块表记录的子对象
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public List<ObjectId> GetChildObjects<T>(BlockTableRecord blockTableRecord, Func<T, bool> filter = null)
            where T : DBObject
        {
            try
            {
                List<ObjectId> ret;
                try
                {
                    var childIds = new List<ObjectId>();
                    foreach (var objectId in blockTableRecord)
                    {
                        var dbObject = GetObject<DBObject>(objectId);
                        if (dbObject != null && dbObject is T && (filter == null || filter((T)dbObject)))
                        {
                            childIds.Add(objectId);
                        }
                    }

                    ret = childIds;
                }
                catch (Exception ex)
                {
                    Logger._.Error(ex.Message);
                    ret = null;
                }

                return ret ?? new List<ObjectId>();
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取子对象失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }

        /// <summary>
        ///     从模型空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public List<ObjectId> GetChildObjectsFromModelspace<T>(Func<T, bool> filter = null) where T : DBObject
        {
            try
            {
                return GetChildObjects(GetModelSpace(), filter);
            }
            catch (Exception ex)
            {
                Logger._.Error($"从模型空间获取子对象失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }

        /// <summary>
        ///     从当前空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public List<ObjectId> GetChildObjectsFromCurrentSpace<T>(Func<T, bool> filter = null) where T : DBObject
        {
            try
            {
                return GetChildObjects(GetCurrentSpace(), filter);
            }
            catch (Exception ex)
            {
                Logger._.Error($"从当前空间获取子对象失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }

        public void IsolateObjectsOfModelSpace(ICollection<ObjectId> objectIdsToIsolate)
        {
            // 获取所有模型空间对象
            var allObjects = GetChildObjectsFromModelspace<DBObject>();

            // 确定需要隐藏的对象（所有对象减去需要隔离的对象）
            var objectsToHide = allObjects.Where(id => !objectIdsToIsolate.Contains(id)).ToList();

            // 设置需要隐藏的对象为不可见
            foreach (var id in objectsToHide)
            {
                if (!id.IsValid)
                {
                    continue;
                }

                var entity = GetObject<Entity>(id, OpenMode.ForWrite);
                if (entity != null)
                {
                    entity.Visible = false;
                }
            }

            // 确保需要隔离的对象可见
            foreach (var id in objectIdsToIsolate)
            {
                if (!id.IsValid)
                {
                    continue;
                }

                var entity = GetObject<Entity>(id, OpenMode.ForWrite);
                if (entity != null)
                {
                    entity.Visible = true;
                }
            }
        }

        /// <summary>
        ///     获取当前空间（模型空间或纸空间）
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>当前空间块表记录</returns>
        public BlockTableRecord GetCurrentSpace(OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                var database = CadServiceManager._.CadDb;
                var currentSpaceId = database.CurrentSpaceId;
                return GetObject<BlockTableRecord>(currentSpaceId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取当前空间失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     向当前空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        public ObjectId AppendEntityToCurrentSpace(Entity entity)
        {
            try
            {
                var objectId = AppendEntityToBlockTableRecord(GetCurrentSpace(OpenMode.ForWrite), entity);
                return objectId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"向当前空间添加实体失败: {ex.Message}");
                return ObjectId.Null;
            }
        }
 
        /// <summary
        public List<ObjectId> AppendEntitiesToCurrentSpace(List<Entity> entities) =>
            AppendEntitiesToBlockTableRecord(GetCurrentSpace(OpenMode.ForWrite), entities);

        public List<ObjectId> AppendEntitiesToBlockTableRecord(BlockTableRecord blockTableRecord,
            ICollection<Entity> entities)
        {
            try
            {
                var objectIds = new List<ObjectId>();
                foreach (var entity in entities)
                {
                    var objectId = AppendEntityToBlockTableRecord(blockTableRecord, entity);
                    objectIds.Add(objectId);
                }

                return objectIds;
            }
            catch (Exception ex)
            {
                Logger._.Error($"向块表记录添加实体失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }

        public List<ObjectId> FilterObjects<T>(ICollection<ObjectId> objectIds, Func<T, bool> filter = null)
            where T : DBObject
        {
            try
            {
                var result = new List<ObjectId>();
                foreach (var objectId in objectIds)
                {
                    var dbObject = GetObject<DBObject>(objectId);
                    if (dbObject == null || !(dbObject is T) || (filter != null && !filter((T)dbObject)))
                    {
                        continue;
                    }

                    result.Add(objectId);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger._.Error($"过滤对象失败: {ex.Message}");
                return new List<ObjectId>();
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
                BlockTableRecord btr;
                if (!string.IsNullOrEmpty(blockName))
                {
                    btr = GetBlockTableRecord(blockName);
                    if (btr != null)
                    {
                        Logger._.Warn($"图块{blockName}定义已存在！");
                        return ObjectId.Null;
                    }
                }
                else
                {
                    blockName = CadServiceManager.GetDefaultName();
                }

                // 创建块表记录
                btr = new BlockTableRecord();
                btr.Name = blockName;


                // 添加块表记录到块表
                var bt = GetBlockTable(OpenMode.ForWrite);

                var blockId = bt.Add(btr);

                // 将实体添加到块表记录
                AppendEntitiesToBlockTableRecord(btr, entities);
                CadTrans.AddNewlyCreatedDBObject(btr,true);

                return blockId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建块失败: {ex.Message}");
                return ObjectId.Null;
            }
        }

        // ... existing code ...

        /// <summary>
        ///     在当前空间创建块参照
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="insertPt">插入点</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="color">颜色</param>
        /// <param name="linetype">线型</param>
        /// <returns>创建成功的块参照ObjectId，失败返回ObjectId.Null</returns>
        public ObjectId CreateBlockRefInCurrentSpace(ObjectId blkDefId, Point3d insertPt = default(Point3d),
            string layerName = "", short colorIndex = 256, string linetype = "BYLAYER")
        {
            try
            {
                insertPt = insertPt == default(Point3d) ? Point3d.Origin : insertPt;
                layerName = GetValidLayerName(layerName);

                colorIndex = GetValidColorIndex(colorIndex, CadServiceManager.ColorIndexByLayer);
                linetype = GetValidLineTypeName(linetype);
                // 创建块参照
                var blkRef = new BlockReference(insertPt, blkDefId)
                {
                    Layer = layerName,
                    ColorIndex = colorIndex,
                    Linetype = linetype
                };

                // 将块参照添加到当前空间
                AppendEntityToCurrentSpace(blkRef);
                return blkRef.Id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建块参照失败: {ex.Message}");
                return ObjectId.Null;
            }
        }

        public LayerTable GetLayerTable(OpenMode openMode = OpenMode.ForRead)
        {

            return  GetObject<LayerTable>(CadServiceManager._.CadDb.LayerTableId, openMode);
        }

        /// <summary>
        ///     创建新线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的线型对象，如果创建失败则返回null</returns>
        public LinetypeTableRecord CreateNewLineType(string lineTypeName)
        {
            try
            {
                // 获取线型表
                var lineTypeTable = GetLineTypeTable(OpenMode.ForWrite);
                if (lineTypeTable == null)
                {
                    Logger._.Error("获取线型表失败");
                    return null;
                }

                if (string.IsNullOrEmpty(lineTypeName))
                {
                    Logger._.Error("LineType Name is null or empty.");
                    return null;
                }

                // 检查线型是否已存在
                if (lineTypeTable.Has(lineTypeName))
                {
                    Logger._.Warn($"线型 {lineTypeName} 已存在");
                    return null;
                }

                // 创建新线型
                var lineType = new LinetypeTableRecord
                {
                    Name = lineTypeName,
                    AsciiDescription = $"线型 {lineTypeName}",
                    PatternLength = 1.0,
                    NumDashes = 1
                };

                // 将新线型添加到线型表
                var lineTypeId = lineTypeTable.Add(lineType);
                CadTrans.AddNewlyCreatedDBObject(lineType, true);

                Logger._.Info($"成功创建线型: {lineTypeName}");
                return lineType;
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建线型失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        ///     获取图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>图层对象，如果不存在则返回null</returns>
        public LayerTableRecord GetLayer(string layerName, OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                // 获取图层表
                var layerTable = GetLayerTable();
                if (layerTable == null)
                {
                    Logger._.Error("获取图层表失败");
                    return null;
                }

                // 检查图层是否存在
                if (string.IsNullOrEmpty(layerName))
                {
                    Logger._.Warn("Use current layer as default");
                    layerName = GetCurrentLayerName();
                }
                else if (!layerTable.Has(layerName))
                {
                    Logger._.Debug($"图层 {layerName} 不存在，自动创建图层");
                    return CreateNewLayer(layerName);
                }

                // 获取图层对象
                var layerId = layerTable[layerName];
                return GetObject<LayerTableRecord>(layerId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取图层失败: {ex.Message}", ex);
                return layerName != GetCurrentLayerName() ? GetLayer(GetCurrentLayerName(), openMode) : null;
            }
        }

        /// <summary>
        ///     获取线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>线型对象，如果不存在则返回null</returns>
        public LinetypeTableRecord GetLineType(string lineTypeName, OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                // 获取线型表
                var lineTypeTable = GetLineTypeTable();
                if (lineTypeTable == null)
                {
                    Logger._.Error("获取线型表失败");
                    return null;
                }

                if (string.IsNullOrEmpty(lineTypeName))
                {
                    Logger._.Warn("Use Continuous as default lineType");
                    lineTypeName = CadServiceManager.LineTypeContinuous;
                }
                // 检查线型是否存在
                else if (!lineTypeTable.Has(lineTypeName))
                {
                    Logger._.Debug($"线型 {lineTypeName} 不存在，自动创建线型");
                    return CreateNewLineType(lineTypeName);
                }

                // 获取线型对象
                var lineTypeId = lineTypeTable[lineTypeName];
                return GetObject<LinetypeTableRecord>(lineTypeId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取线型失败: {ex.Message}", ex);
                return lineTypeName != CadServiceManager.LineTypeContinuous
                    ? GetLineType(CadServiceManager.LineTypeContinuous, openMode)
                    : null;
            }
        }

        public LinetypeTable GetLineTypeTable(OpenMode openMode = OpenMode.ForRead) =>
            GetObject<LinetypeTable>(CadServiceManager._.CadDb.LinetypeTableId, openMode);

        /// <summary>
        ///     获取块表
        /// </summary>
        /// <returns>块表</returns>
        public BlockTable GetBlockTable(OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                return GetObject<BlockTable>(CadServiceManager._.CadDb.BlockTableId, openMode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取块表失败: {ex.Message}");
                return null;
            }
        }

        public string GetValidLayerName(string layerName)
        {
            var layer = GetLayer(layerName);
            return layer == null ? GetCurrentLayerName() : layer.Name;
        }
        public string GetValidLineTypeName(string linetypeName)
        {
            var lineType = GetLineType(linetypeName);
            return lineType == null ? CadServiceManager.LineTypeContinuous : lineType.Name;
        }

        public short GetValidColorIndex(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite)
        {
            if (colorIndex >= 0 && colorIndex <= 256 && colorIndex != default(short))
            {
                return colorIndex;
            }

            return GetValidColorIndex(defaultColorIndex);
        }
        public Color GetValidColor(short colorIndex,short defaultColorIndex=CadServiceManager.ColorIndexWhite)
        {
            if(colorIndex==0) return Color.FromColorIndex(ColorMethod.ByBlock,CadServiceManager.ColorIndexByBlock);
            if (colorIndex == 256)
                return Color.FromColorIndex(ColorMethod.ByLayer, CadServiceManager.ColorIndexByLayer);
            return Color.FromColorIndex(ColorMethod.ByAci, GetValidColorIndex(colorIndex, defaultColorIndex));
        }
        /// <summary>
        ///     获取当前图层名称
        /// </summary>
        /// <returns>当前图层名称</returns>
        public string GetCurrentLayerName()
        {
            try
            {
                LayerTableRecord layer;
                try
                {
                    // 获取当前图层ID
                    var currentLayerId = CadServiceManager._.CadDb.Clayer;

                    // 获取图层对象
                    layer = GetObject<LayerTableRecord>(currentLayerId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"获取当前图层失败: {ex.Message}");
                    layer = null;
                }

                return layer?.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取当前图层名称失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        ///     创建新图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>创建的图层对象，如果创建失败则返回null</returns>
        public LayerTableRecord CreateNewLayer(string layerName = "",
            short colorIndex = CadServiceManager.ColorIndexWhite,
            string lineType = CadServiceManager.LineTypeContinuous)
        {
            try
            {
                // 获取图层表
                var layerTable = GetLayerTable(OpenMode.ForWrite);
                if (layerTable == null)
                {
                    Logger._.Error("获取图层表失败");
                    return null;
                }

                if (string.IsNullOrEmpty(layerName))
                {
                    layerName = $"Layer_{DateTime.Now.ToShortTimeString()}";
                }
                else if (layerTable.Has(layerName))
                {
                    Logger._.Warn($"图层 {layerName} 已存在");
                    return null;
                }

                var color = GetValidColor(colorIndex, CadServiceManager.ColorIndexWhite);

                var linetypeObjectId = GetLineType(lineType).Id;

                // 创建新图层
                var layer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = color,
                    LinetypeObjectId = linetypeObjectId,
                    LineWeight = LineWeight.LineWeight000
                };
                var layerId = layerTable.Add(layer);
                CadTrans.AddNewlyCreatedDBObject(layer, true);

                Logger._.Info($"成功创建图层: {layerName}");
                return layer;
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建图层失败: {ex.Message}", ex);
                return null;
            }
        }
    }
}
