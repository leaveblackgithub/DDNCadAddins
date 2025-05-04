using System;
using System.Collections.Generic;
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

            // 初始化服务组件
            Entity = new TransactionServiceForEntity(this);
            Block = new TransactionServiceForBlock(this);
            Style = new TransactionServiceForStyle(this);
        }

        /// <summary>
        ///     事务对象
        /// </summary>
        private Transaction CadTrans { get; }

        /// <summary>
        ///     块服务缓存字典
        /// </summary>
        public IDictionary<ObjectId, IBlockService> BlockServiceDict { get; }

        /// <summary>
        ///     实体服务组件
        /// </summary>
        public ITransactionServiceForEntity Entity { get; }

        /// <summary>
        ///     块服务组件
        /// </summary>
        public ITransactionServiceForBlock Block { get; }

        /// <summary>
        ///     样式服务组件
        /// </summary>
        public ITransactionServiceForStyle Style { get; }

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
                if (objectId.IsNull)
                {
                    return null;
                }

                return CadTrans.GetObject(objectId, openMode) as T;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取对象异常: {ex.Message}");
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
                if (entity == null)
                {
                    Logger._.Error("添加实体失败：实体为null");
                    return ObjectId.Null;
                }

                using (var modelSpace = GetModelSpace(OpenMode.ForWrite))
                {
                    if (modelSpace == null)
                    {
                        Logger._.Error("添加实体失败：获取模型空间失败");
                        return ObjectId.Null;
                    }

                    var id = modelSpace.AppendEntity(entity);
                    CadTrans.AddNewlyCreatedDBObject(entity, true);
                    return id;
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"添加实体异常: {ex.Message}");
                return ObjectId.Null;
            }
        }

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
                if (blockTableRecord == null || entity == null)
                {
                    Logger._.Error("添加实体失败：块表记录或实体为null");
                    return ObjectId.Null;
                }

                var id = blockTableRecord.AppendEntity(entity);
                CadTrans.AddNewlyCreatedDBObject(entity, true);
                return id;
            }
            catch (Exception ex)
            {
                Logger._.Error($"添加实体异常: {ex.Message}");
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
                var blockTable = GetBlockTable(openMode);
                if (blockTable == null)
                {
                    Logger._.Error("获取模型空间失败：获取块表失败");
                    return null;
                }

                return GetObject<BlockTableRecord>(blockTable[BlockTableRecord.ModelSpace], openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取模型空间异常: {ex.Message}");
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
                if (string.IsNullOrEmpty(name))
                {
                    Logger._.Error("获取块表记录ID失败：块名称为空");
                    return ObjectId.Null;
                }

                var blockTable = GetBlockTable();
                if (blockTable == null)
                {
                    Logger._.Error("获取块表记录ID失败：获取块表失败");
                    return ObjectId.Null;
                }

                if (!blockTable.Has(name))
                {
                    Logger._.Warn($"块 {name} 不存在");
                    return ObjectId.Null;
                }

                return blockTable[name];
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块表记录ID异常: {ex.Message}");
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
                var id = GetBlockTableRecordId(name);
                if (id.IsNull)
                {
                    return null;
                }

                return GetObject<BlockTableRecord>(id, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块表记录异常: {ex.Message}");
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
            var result = new List<ObjectId>();

            try
            {
                if (blockTableRecord == null)
                {
                    Logger._.Error("获取子对象失败：块表记录为null");
                    return result;
                }

                foreach (var id in blockTableRecord)
                {
                    try
                    {
                        var obj = GetObject<T>(id);
                        if (obj != null && (filter == null || filter(obj)))
                        {
                            result.Add(id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"访问对象 {id} 异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取子对象异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        ///     从模型空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public List<ObjectId> GetChildObjectsFromModelspace<T>(Func<T, bool> filter = null) where T : DBObject
        {
            using (var modelSpace = GetModelSpace())
            {
                if (modelSpace == null)
                {
                    Logger._.Error("从模型空间获取子对象失败：获取模型空间失败");
                    return new List<ObjectId>();
                }

                return GetChildObjects(modelSpace, filter);
            }
        }

        /// <summary>
        ///     从当前空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public List<ObjectId> GetChildObjectsFromCurrentSpace<T>(Func<T, bool> filter = null) where T : DBObject
        {
            using (var currentSpace = GetCurrentSpace())
            {
                if (currentSpace == null)
                {
                    Logger._.Error("从当前空间获取子对象失败：获取当前空间失败");
                    return new List<ObjectId>();
                }

                return GetChildObjects(currentSpace, filter);
            }
        }

        /// <summary>
        ///     隔离模型空间中的对象
        /// </summary>
        /// <param name="objectIdsToIsolate">要隔离的对象ID集合</param>
        public void IsolateObjectsOfModelSpace(ICollection<ObjectId> objectIdsToIsolate)
        {
            try
            {
                if (objectIdsToIsolate == null || objectIdsToIsolate.Count == 0)
                {
                    Logger._.Error("隔离对象失败：对象集合为空");
                    return;
                }

                using (var modelSpace = GetModelSpace(OpenMode.ForWrite))
                {
                    if (modelSpace == null)
                    {
                        Logger._.Error("隔离对象失败：获取模型空间失败");
                        return;
                    }

                    foreach (var id in modelSpace)
                    {
                        try
                        {
                            // 跳过非实体对象
                            if (!(GetObject<DBObject>(id) is Entity))
                            {
                                continue;
                            }

                            var entity = GetObject<Entity>(id, OpenMode.ForWrite);
                            if (entity == null)
                            {
                                continue;
                            }

                            if (objectIdsToIsolate.Contains(id))
                            {
                                // 显示要隔离的对象
                                entity.Visible = true;
                            }
                            else
                            {
                                // 隐藏其他对象
                                entity.Visible = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger._.Warn($"处理对象 {id} 异常: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"隔离对象异常: {ex.Message}");
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
                var db = HostApplicationServices.WorkingDatabase;
                var blockTable = GetObject<BlockTable>(db.BlockTableId, openMode);
                if (blockTable == null)
                {
                    Logger._.Error("获取当前空间失败：获取块表失败");
                    return null;
                }

                ObjectId currentSpaceId;
                if (db.TileMode)
                {
                    // 模型空间
                    currentSpaceId = blockTable[BlockTableRecord.ModelSpace];
                }
                else
                {
                    // 纸空间
                    currentSpaceId = db.CurrentSpaceId;
                }

                return GetObject<BlockTableRecord>(currentSpaceId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取当前空间异常: {ex.Message}");
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
                if (entity == null)
                {
                    Logger._.Error("添加实体失败：实体为null");
                    return ObjectId.Null;
                }

                using (var currentSpace = GetCurrentSpace(OpenMode.ForWrite))
                {
                    if (currentSpace == null)
                    {
                        Logger._.Error("添加实体失败：获取当前空间失败");
                        return ObjectId.Null;
                    }

                    var id = currentSpace.AppendEntity(entity);
                    CadTrans.AddNewlyCreatedDBObject(entity, true);
                    return id;
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"添加实体异常: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     向当前空间添加多个实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <returns>添加的实体ID集合</returns>
        public List<ObjectId> AppendEntitiesToCurrentSpace(List<Entity> entities) =>
            AppendEntitiesToBlockTableRecord(GetCurrentSpace(OpenMode.ForWrite), entities);

        /// <summary>
        ///     向块表记录添加多个实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entities">实体集合</param>
        /// <returns>添加的实体ID集合</returns>
        public List<ObjectId> AppendEntitiesToBlockTableRecord(BlockTableRecord blockTableRecord,
            ICollection<Entity> entities)
        {
            var ids = new List<ObjectId>();

            try
            {
                if (blockTableRecord == null || entities == null || entities.Count == 0)
                {
                    Logger._.Error("添加实体失败：块表记录或实体集合为null或为空");
                    return ids;
                }

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null)
                        {
                            continue;
                        }

                        var id = blockTableRecord.AppendEntity(entity);
                        CadTrans.AddNewlyCreatedDBObject(entity, true);
                        ids.Add(id);
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"添加实体异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"添加实体集合异常: {ex.Message}");
            }

            if (blockTableRecord.IsDisposed == false)
            {
                blockTableRecord.Dispose();
            }

            return ids;
        }

        /// <summary>
        ///     过滤对象集合
        /// </summary>
        /// <param name="objectIds">对象ID集合</param>
        /// <param name="filter">过滤器</param>
        /// <returns>过滤后的对象ID集合</returns>
        public List<ObjectId> FilterObjects<T>(ICollection<ObjectId> objectIds, Func<T, bool> filter = null)
            where T : DBObject
        {
            var result = new List<ObjectId>();

            try
            {
                if (objectIds == null || objectIds.Count == 0)
                {
                    Logger._.Error("过滤对象失败：对象集合为空");
                    return result;
                }

                foreach (var id in objectIds)
                {
                    try
                    {
                        var obj = GetObject<T>(id);
                        if (obj != null && (filter == null || filter(obj)))
                        {
                            result.Add(id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger._.Warn($"访问对象 {id} 异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Error($"过滤对象异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        ///     获取块表
        /// </summary>
        /// <returns>块表</returns>
        public BlockTable GetBlockTable(OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                var db = HostApplicationServices.WorkingDatabase;
                return GetObject<BlockTable>(db.BlockTableId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取块表异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     添加新创建的数据库对象
        /// </summary>
        /// <param name="obj">数据库对象</param>
        /// <param name="add">是否添加</param>
        public void AddNewlyCreatedDBObject(DBObject obj, bool add) => CadTrans.AddNewlyCreatedDBObject(obj, add);
    }
}
