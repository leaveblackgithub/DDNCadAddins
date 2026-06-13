using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     ExplodeAsShown 操作结果，包含生成的实体及统计信息
    /// </summary>
    public class ExplodeAsShownResult
    {
        /// <summary>
        ///     添加到当前空间的实体 ID 列表
        /// </summary>
        public List<ObjectId> EntityIds { get; set; } = new List<ObjectId>();

        /// <summary>
        ///     由属性转换而来的文字数量
        /// </summary>
        public int AttributeTextCount { get; set; }

        /// <summary>
        ///     继承块参照图层的子实体数量（原图层为 0）
        /// </summary>
        public int LayerAdjustedCount { get; set; }

        /// <summary>
        ///     继承块参照颜色的子实体数量（原颜色为 ByBlock）
        /// </summary>
        public int ColorAdjustedCount { get; set; }
    }
}
