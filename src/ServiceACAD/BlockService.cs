using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public class BlockService : IBlockService
    {
        public BlockService(ITransactionService serviceTrans, BlockReference blkRef)
        {
            ServiceTrans=serviceTrans;
            CadBlkRef = blkRef;
        }

        public ITransactionService ServiceTrans { get; }

        public BlockReference CadBlkRef { get; }

        public bool IsXclipped()
        {
            if (CadBlkRef == null)
            {
                return false;
            }

            // 检查块参照是否有X裁剪
            // 在AutoCAD .NET API中，通过检查扩展字典中是否包含"ACAD_FILTER"字典和"SPATIAL"项来判断

            // 检查是否存在扩展字典
            if (CadBlkRef.ExtensionDictionary == ObjectId.Null)
            {
                return false;
            }

            using (var tr = CadBlkRef.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    // 打开扩展字典
                    var extDict = tr.GetObject(CadBlkRef.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                    if (extDict == null)
                    {
                        return false;
                    }

                    // 检查是否包含ACAD_FILTER字典
                    if (!extDict.Contains("ACAD_FILTER"))
                    {
                        return false;
                    }

                    // 打开ACAD_FILTER字典
                    var filterDict = tr.GetObject(extDict.GetAt("ACAD_FILTER"), OpenMode.ForRead) as DBDictionary;
                    if (filterDict == null)
                    {
                        return false;
                    }

                    // 检查是否包含SPATIAL项，如果包含则表示有X裁剪
                    return filterDict.Contains("SPATIAL");
                }
                catch
                {
                    return false;
                }
                finally
                {
                    tr.Commit();
                }
            }
        }
    }
}
