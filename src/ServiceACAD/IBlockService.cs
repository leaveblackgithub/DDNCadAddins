using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public interface IBlockService
    {
        /// <summary>
        /// 获取块参照对象
        /// </summary>
        BlockReference CadBlkRef { get; }

        /// <summary>
        /// 获取事务服务
        /// </summary>
        ITransactionService ServiceTrans { get; }

        /// <summary>
        /// 检查块参照是否被X裁剪
        /// </summary>
        /// <returns>如果块参照被X裁剪返回true，否则返回false</returns>
        bool IsXclipped();
        
        /// <summary>
        /// 检查块参照是否包含属性
        /// </summary>
        /// <returns>如果块参照包含属性返回true，否则返回false</returns>
        bool HasAttributes();

        /// <summary>
        /// 爆炸块参照并将其属性转换为文本
        /// </summary>
        /// <returns>如果爆炸成功返回true，否则返回false</returns>
        OpResult<List<ObjectId>> ExplodeWithAttributes();
    }
}
