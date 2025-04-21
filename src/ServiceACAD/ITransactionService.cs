using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    /// 事务服务接口，提供与事务相关的操作
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        /// 事务对象
        /// </summary>
        Transaction CadTrans { get; }

        /// <summary>
        /// 向模型空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToModelSpace(Entity entity);

        /// <summary>
        /// 向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToBlockTableRecord(BlockTableRecord blockTableRecord, Entity entity);

        /// <summary>
        /// 获取模型空间
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>模型空间块表记录</returns>
        BlockTableRecord GetModelSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        /// 获取块表
        /// </summary>
        /// <returns>块表</returns>
        BlockTable GetBlockTable();

        /// <summary>
        /// 获取块表记录ID
        /// </summary>
        /// <param name="name">块名称</param>
        /// <returns>块表记录ID</returns>
        ObjectId GetBlockTableRecordId(string name);

        /// <summary>
        /// 获取块表记录
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表记录</returns>
        BlockTableRecord GetBlockTableRecord(string name, OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        /// 获取块表记录的子对象
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        ICollection<ObjectId> GetChildObjects(BlockTableRecord blockTableRecord, Func<DBObject, bool> filter = null);

        /// <summary>
        /// 从模型空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        ICollection<ObjectId> GetChildObjectsFromModelspace(Func<DBObject, bool> filter = null);
    }
} 