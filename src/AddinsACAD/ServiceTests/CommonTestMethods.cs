using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using ServiceACAD;

namespace AddinsACAD.ServiceTests
{
    public static class CommonTestMethods
    {
        public static List<ObjectId> GetBlkRefIdsOf23432(ITransactionService tr)
        {
            var blkRefIds = tr.GetChildObjectsFromModelspace<BlockReference>(
                blkRef => blkRef.Name == "23432");
            return blkRefIds;
        }
    }
}
