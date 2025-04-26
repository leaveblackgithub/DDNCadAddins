using System;
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
        public static IBlockService GetFirstBlkServiceOf23432(ITransactionService tr)
        {
            var blkRefIds = GetBlkRefIdsOf23432(tr);
            var blkService = tr.GetBlockService(blkRefIds[0]);
            return blkService;
        }

        public static string GetTestLayerName() => "TestLayer_" + Guid.NewGuid().ToString("N");
        public static string GetTestLineTypeName() => "TestLineType_" + Guid.NewGuid().ToString("N");
    }
}
