using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     图层锁定与冻结状态的快照，用于命令结束后恢复
    /// </summary>
    public class LayerStateSnapshot
    {
        /// <summary>
        ///     各图层的原始状态，键为图层 ObjectId
        /// </summary>
        public Dictionary<ObjectId, LayerStateEntry> States { get; } = new Dictionary<ObjectId, LayerStateEntry>();
    }

    /// <summary>
    ///     单个图层的锁定与冻结状态
    /// </summary>
    public class LayerStateEntry
    {
        /// <summary>
        ///     是否锁定
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        ///     是否冻结
        /// </summary>
        public bool IsFrozen { get; set; }
    }
}
