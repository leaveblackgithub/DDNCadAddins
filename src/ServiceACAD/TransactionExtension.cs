using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    //如果包装到类里就会出现锁定错误……
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

        public static BlockTableRecord GetModelSpace(this Transaction tr, Database db, OpenMode openMode) =>
            tr.GetBlockTableRecord(db, BlockTableRecord.ModelSpace, openMode);

        public static BlockTable GetBlockTable(this Transaction tr, Database db) =>
            (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        public static ObjectId GetBlockTableRecordId(this Transaction tr, Database db, string name)
        {
            var blockTable = GetBlockTable(tr, db);
            return blockTable[name];
        }

        public static BlockTableRecord GetBlockTableRecord(this Transaction tr, Database db, string name,
            OpenMode openMode) =>
            (BlockTableRecord)tr.GetObject(GetBlockTableRecordId(tr, db, name), openMode);
    }
}
