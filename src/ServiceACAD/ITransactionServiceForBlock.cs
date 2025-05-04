using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务块部分接口，提供块管理功能
    /// </summary>
    public interface ITransactionServiceForBlock
    {
        /// <summary>
        ///     获取块服务
        /// </summary>
        /// <param name="objectId">块引用ID</param>
        /// <returns>块服务实例</returns>
        IBlockService GetBlockService(ObjectId objectId);

        /// <summary>
        ///     创建块定义
        /// </summary>
        /// <param name="entities">要包含在块中的实体集合</param>
        /// <param name="blockName">块名称</param>
        /// <returns>创建的块的ObjectId</returns>
        ObjectId CreateBlockDef(ICollection<Entity> entities, string blockName = "");

        /// <summary>
        ///     在当前空间创建块参照
        /// </summary>
        /// <param name="blkDefId">块定义ID</param>
        /// <param name="insertPt">插入点</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="linetype">线型</param>
        /// <returns>创建成功的块参照ObjectId，失败返回ObjectId.Null</returns>
        ObjectId CreateBlockRefInCurrentSpace(ObjectId blkDefId, Point3d insertPt = default(Point3d),
            string layerName = "", short colorIndex = 256, string linetype = "BYLAYER");

        /// <summary>
        ///     为块参照添加多个属性并赋值
        /// </summary>
        /// <param name="blkDefId">块定义ID</param>
        /// <param name="blkRefId">块参照ID</param>
        /// <param name="attributeValues">属性Tag和对应的值字典</param>
        /// <returns>是否成功添加属性</returns>
        bool AddAttributesToBlockReference(ObjectId blkDefId,
            ObjectId blkRefId,
            Dictionary<string, Dictionary<string, object>> attributeValues);
    }
}
