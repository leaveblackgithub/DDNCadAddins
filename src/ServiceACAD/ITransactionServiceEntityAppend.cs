using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    /// 事务服务实体添加接口 - 实体创建和添加操作
    /// </summary>
    public interface ITransactionServiceEntityAppend
    {
        /// <summary>
        /// 向模型空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToModelSpace(Entity entity);

        /// <summary>
        /// 向当前空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToCurrentSpace(Entity entity);

        /// <summary>
        /// 向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToBlockTableRecord(BlockTableRecord blockTableRecord, Entity entity);

        /// <summary>
        /// 向当前空间添加多个实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <returns>添加的实体ID集合</returns>
        List<ObjectId> AppendEntitiesToCurrentSpace(List<Entity> entities);

        /// <summary>
        /// 向块表记录添加多个实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entities">实体集合</param>
        /// <returns>添加的实体ID集合</returns>
        List<ObjectId> AppendEntitiesToBlockTableRecord(BlockTableRecord blockTableRecord,
            ICollection<Entity> entities);
    }
}
