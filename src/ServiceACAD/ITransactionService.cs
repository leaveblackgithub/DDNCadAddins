using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务接口，提供与事务相关的操作
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        ///     块服务缓存字典
        /// </summary>
        IDictionary<ObjectId, IBlockService> BlockServiceDict { get; }

        /// <summary>
        ///     实体服务组件
        /// </summary>
        ITransactionServiceForEntity Entity { get; }

        /// <summary>
        ///     块服务组件
        /// </summary>
        ITransactionServiceForBlock Block { get; }

        /// <summary>
        ///     样式服务组件
        /// </summary>
        ITransactionServiceForStyle Style { get; }

        /// <summary>
        ///     获取数据库对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="objectId">对象ID</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>数据库对象</returns>
        T GetObject<T>(ObjectId objectId, OpenMode openMode = OpenMode.ForRead) where T : DBObject;

        /// <summary>
        ///     向模型空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToModelSpace(Entity entity);

        /// <summary>
        ///     向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToBlockTableRecord(BlockTableRecord blockTableRecord, Entity entity);

        /// <summary>
        ///     获取模型空间
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>模型空间块表记录</returns>
        BlockTableRecord GetModelSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取块表记录ID
        /// </summary>
        /// <param name="name">块名称</param>
        /// <returns>块表记录ID</returns>
        ObjectId GetBlockTableRecordId(string name);

        /// <summary>
        ///     获取块表记录
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表记录</returns>
        BlockTableRecord GetBlockTableRecord(string name, OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取块表记录的子对象
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        List<ObjectId> GetChildObjects<T>(BlockTableRecord blockTableRecord, Func<T, bool> filter = null)
            where T : DBObject;

        /// <summary>
        ///     从模型空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        List<ObjectId> GetChildObjectsFromModelspace<T>(Func<T, bool> filter = null) where T : DBObject;

        /// <summary>
        ///     从当前空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        List<ObjectId> GetChildObjectsFromCurrentSpace<T>(Func<T, bool> filter = null) where T : DBObject;

        void IsolateObjectsOfModelSpace(ICollection<ObjectId> objectIdsToIsolate);

        /// <summary>
        ///     获取当前空间（模型空间或纸空间）
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>当前空间块表记录</returns>
        BlockTableRecord GetCurrentSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     向当前空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToCurrentSpace(Entity entity);

        List<ObjectId> AppendEntitiesToCurrentSpace(List<Entity> entities);

        List<ObjectId> AppendEntitiesToBlockTableRecord(BlockTableRecord blockTableRecord,
            ICollection<Entity> entities);

        List<ObjectId> FilterObjects<T>(ICollection<ObjectId> objectIds, Func<T, bool> filter = null)
            where T : DBObject;

        /// <summary>
        ///     获取块表
        /// </summary>
        /// <returns>块表</returns>
        BlockTable GetBlockTable(OpenMode openMode = OpenMode.ForRead);

        void AddNewlyCreatedDBObject(DBObject obj, bool add);
    }
}
