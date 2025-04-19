using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    //如果包装到类里就会出现锁定错误……
    //本类返回方法不能使用OpResult，否则会报错
    public static class TransactionExtension
    {
        public static ObjectId AppendEntityToModelSpace(this Transaction tr, Database db, Entity entity)
        {
            // var blockTable = GetBlockTable(tr, db);
            var modelSpace =
                GetModelSpace(tr, db, OpenMode.ForWrite);

            // var objectId = modelSpace.AppendEntity(entity);
            // tr.AddNewlyCreatedDBObject(entity, true);
            return tr.AppendEntityToBlockTableRecord(modelSpace, entity);
        }

        public static ObjectId AppendEntityToBlockTableRecord(this Transaction tr, BlockTableRecord blockTableRecord,
            Entity entity)
        {
            var objectId = blockTableRecord.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
            return objectId;
        }

        public static BlockTableRecord GetModelSpace(this Transaction tr, Database db,
            OpenMode openMode = OpenMode.ForRead) =>
            tr.GetBlockTableRecord(db, BlockTableRecord.ModelSpace, openMode);

        public static BlockTable GetBlockTable(this Transaction tr, Database db) =>
            (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        public static ObjectId GetBlockTableRecordId(this Transaction tr, Database db, string name)
        {
            var blockTable = GetBlockTable(tr, db);
            return blockTable[name];
        }

        public static BlockTableRecord GetBlockTableRecord(this Transaction tr, Database db, string name,
            OpenMode openMode = OpenMode.ForRead) =>
            (BlockTableRecord)tr.GetObject(GetBlockTableRecordId(tr, db, name), openMode);

        public static ICollection<ObjectId> GetChildObjects(this Transaction tr, Database db,
            BlockTableRecord blockTableRecord, Func<DBObject, bool> filter = null)
        {
            try
            {
                var childIds = new List<ObjectId>();
                foreach (var objectId in blockTableRecord)
                {
                    var dbObject = tr.GetObject(objectId, OpenMode.ForRead) as DBObject;
                    if (dbObject != null && (filter == null || filter(dbObject)))
                    {
                        childIds.Add(objectId);
                    }
                }

                return childIds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public static ICollection<ObjectId> GetChildObjectsFrModelspace(this Transaction transaction, Database database,
            Func<DBObject, bool> filter = null)
        {
            var modelSpace = transaction.GetModelSpace(database);
            return transaction.GetChildObjects(database, modelSpace, filter);
        }
    }
}
