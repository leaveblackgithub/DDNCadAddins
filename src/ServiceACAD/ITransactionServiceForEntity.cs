using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务实体部分接口，提供实体创建和属性管理功能
    /// </summary>
    public interface ITransactionServiceForEntity
    {
        /// <summary>
        ///     为实体添加自定义标识
        /// </summary>
        /// <param name="entity">要添加标识的实体</param>
        /// <param name="identityKey">标识键名</param>
        /// <param name="identityValue">标识值</param>
        /// <returns>是否添加成功</returns>
        bool AddCustomIdentity(Entity entity, string identityKey, string identityValue);

        /// <summary>
        ///     获取实体的自定义标识
        /// </summary>
        /// <param name="entity">要获取标识的实体</param>
        /// <param name="identityKey">标识键名</param>
        /// <returns>标识值，如不存在则返回null</returns>
        string GetCustomIdentity(Entity entity, string identityKey);

        /// <summary>
        ///     根据类型名称和属性字典创建实体
        /// </summary>
        /// <param name="typeName">实体类型名称</param>
        /// <param name="properties">属性字典</param>
        /// <returns>创建的实体对象，如果创建失败则返回null</returns>
        Entity CreateEntityByTypeAndProperties(string typeName, Dictionary<string, object> properties);
    }
}
