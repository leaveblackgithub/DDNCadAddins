using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

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
                Debug.WriteLine($"获取对象失败: {ex.Message}");
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
                    Debug.WriteLine($"获取块引用失败，ObjectId: {objectId}");
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
                Debug.WriteLine($"获取块服务失败: {ex.Message}");
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
                Debug.WriteLine($"向模型空间添加实体失败: {ex.Message}");
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
                Debug.WriteLine($"向块表记录添加实体失败: {ex.Message}");
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
                Debug.WriteLine($"获取模型空间失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取块表
        /// </summary>
        /// <returns>块表</returns>
        public BlockTable GetBlockTable()
        {
            try
            {
                return GetObject<BlockTable>(CadServiceManager._.CadDb.BlockTableId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取块表失败: {ex.Message}");
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
                Debug.WriteLine($"获取块表记录ID失败: {ex.Message}");
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
                Debug.WriteLine($"获取块表记录失败: {ex.Message}");
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
                    Debug.WriteLine(ex.Message);
                    ret = null;
                }

                return ret ?? new List<ObjectId>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取子对象失败: {ex.Message}");
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
                Debug.WriteLine($"从模型空间获取子对象失败: {ex.Message}");
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
                Debug.WriteLine($"从当前空间获取子对象失败: {ex.Message}");
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
                Debug.WriteLine($"获取当前空间失败: {ex.Message}");
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
                Debug.WriteLine($"向当前空间添加实体失败: {ex.Message}");
                return ObjectId.Null;
            }
        }

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
                Debug.WriteLine($"向块表记录添加实体失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }

        public List<ObjectId> FilterObjects<T>(ICollection<ObjectId> objectIds, Func<T, bool> filter = null)
            where T : DBObject
        {
            try
            {
                List<ObjectId> result = new List<ObjectId>();
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
                Debug.WriteLine($"过滤对象失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }

        /// <summary>
        ///     创建块定义
        /// </summary>
        /// <param name="entities">要包含在块中的实体集合</param>
        /// <param name="blockName">块名称</param>
        /// <returns>创建的块的ObjectId</returns>
        public ObjectId CreateBlock(ICollection<Entity> entities, string blockName = "")
        {
            try
            {
                // 获取块表
                var bt = GetBlockTable();
                if (bt == null)
                {
                    Debug.WriteLine($"获取块表失败");
                    return ObjectId.Null;
                }

                // 创建块表记录
                var btr = new BlockTableRecord();
                btr.Name = string.IsNullOrEmpty(blockName)?DateTime.UtcNow.ToShortTimeString():blockName;
                

                // 添加块表记录到块表
                bt.UpgradeOpen();
                var blockId = bt.Add(btr);

                // 将实体添加到块表记录
                foreach (var entity in entities)
                {
                    AppendEntityToBlockTableRecord(btr, entity);
                }

                return blockId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建块失败: {ex.Message}");
                return ObjectId.Null;
            }
        }
    }
}
