using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    /// 事务服务核心接口 - 基础对象操作
    /// </summary>
    public interface ITransactionServiceCore
    {
        /// <summary>
        /// 获取数据库对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="objectId">对象ID</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>数据库对象</returns>
        T GetObject<T>(ObjectId objectId, OpenMode openMode = OpenMode.ForRead) where T : DBObject;

        /// <summary>
        /// 添加新创建的数据库对象
        /// </summary>
        /// <param name="obj">数据库对象</param>
        /// <param name="add">是否添加</param>
        void AddNewlyCreatedDBObject(DBObject obj, bool add);

        /// <summary>
        /// 过滤对象集合
        /// </summary>
        /// <param name="objectIds">对象ID集合</param>
        /// <param name="filter">过滤器</param>
        /// <returns>过滤后的对象ID集合</returns>
        List<ObjectId> FilterObjects<T>(ICollection<ObjectId> objectIds, Func<T, bool> filter = null)
            where T : DBObject;
    }
}
