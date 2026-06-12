using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public interface IBlockService
    {

        string Name { get; }
        string Layer { get; set; }
        int ColorIndex { get; set; }
        string Linetype { get; set; }

        /// <summary>
        ///     检查块参照是否被X裁剪
        /// </summary>
        /// <returns>如果块参照被X裁剪返回true，否则返回false</returns>
        bool IsXclipped();

        /// <summary>
        ///     检查块参照是否包含属性
        /// </summary>
        /// <returns>如果块参照包含属性返回true，否则返回false</returns>
        bool HasAttributes();

        /// <summary>
        ///     爆炸块参照并将其属性转换为文本
        /// </summary>
        /// <returns>如果爆炸成功返回true，否则返回false</returns>
        OpResult<List<ObjectId>> ExplodeAsShown();

        /// <summary>
        ///     删除块定义不含任何实体的图块参照
        /// </summary>
        /// <returns>删除成功返回 true</returns>
        OpResult<bool> EraseIfEmptyDefinition();

        /// <summary>
        ///     为图块生成Xclip边界
        /// </summary>
        /// <param name="serviceTrans">事务服务</param>
        /// <param name="blockRefId">图块引用ID</param>
        /// <returns>操作结果</returns>
        OpResult<ObjectId> GenerateXclipBoundary();
    }
}
