using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务接口，组合所有子接口，提供完整的事务操作能力
    /// </summary>
    public interface ITransactionService :
        ITransactionServiceCore,
        ITransactionServiceSpace,
        ITransactionServiceEntityAppend,
        ITransactionServiceQuery
    {
        /// <summary>
        ///     块服务缓存字典
        /// </summary>
        IDictionary<ObjectId, IBlockService> BlockServiceDict { get; }

        /// <summary>
        ///     实体服务组件
        /// </summary>
        ITransactionServiceForEntity Entity { get; }

        /// <summary>
        ///     块服务组件
        /// </summary>
        ITransactionServiceForBlock Block { get; }

        /// <summary>
        ///     样式服务组件
        /// </summary>
        ITransactionServiceForStyle Style { get; }

        /// <summary>
        ///     隔离模型空间中的指定对象，隐藏其余所有实体
        /// </summary>
        /// <param name="objectIdsToIsolate">要保持可见的对象ID集合</param>
        void IsolateObjectsOfModelSpace(ICollection<ObjectId> objectIdsToIsolate);
    }
}
