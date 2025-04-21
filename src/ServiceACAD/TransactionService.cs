using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    /// 事务服务实现，提供与事务相关的操作
    /// </summary>
    public class TransactionService : ITransactionService
    {
        /// <summary>
        /// 事务对象
        /// </summary>
        public Transaction CadTrans { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="transaction">事务对象</param>
        public TransactionService(Transaction transaction)
        {
            CadTrans = transaction ;
        }

        /// <summary>
        /// 向模型空间添加实体
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
        /// 向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        public ObjectId AppendEntityToBlockTableRecord(BlockTableRecord blockTableRecord, Entity entity)
        {
            try
            {
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
        /// 获取模型空间
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
        /// 获取块表
        /// </summary>
        /// <returns>块表</returns>
        public BlockTable GetBlockTable()
        {
            try
            {
                return (BlockTable)CadTrans.GetObject(CadServiceManager._.CadDb.BlockTableId, OpenMode.ForRead);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取块表失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取块表记录ID
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
        /// 获取块表记录
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表记录</returns>
        public BlockTableRecord GetBlockTableRecord(string name, OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                return (BlockTableRecord)CadTrans.GetObject(GetBlockTableRecordId(name), openMode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取块表记录失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取块表记录的子对象
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public ICollection<ObjectId> GetChildObjects(BlockTableRecord blockTableRecord, Func<DBObject, bool> filter = null)
        {
            try
            {
                ICollection<ObjectId> ret;
                try
                {
                    var childIds = new List<ObjectId>();
                    foreach (var objectId in blockTableRecord)
                    {
                        var dbObject = CadTrans.GetObject(objectId, OpenMode.ForRead) as DBObject;
                        if (dbObject != null && (filter == null || filter(dbObject)))
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
        /// 从模型空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        public ICollection<ObjectId> GetChildObjectsFromModelspace(Func<DBObject, bool> filter = null)
        {
            try
            {
                return GetChildObjects(GetModelSpace(OpenMode.ForRead), filter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"从模型空间获取子对象失败: {ex.Message}");
                return new List<ObjectId>();
            }
        }
    }
} 
