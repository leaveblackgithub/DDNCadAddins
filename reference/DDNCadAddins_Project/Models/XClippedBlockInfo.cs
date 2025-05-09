using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DDNCadAddins.Models
{
    /// <summary>
    ///     存储被XClip的图块信息的类.
    /// </summary>
    public class XClippedBlockInfo
    {
        /// <summary>
        ///     Gets or sets 图块参照的ObjectId.
        /// </summary>
        public ObjectId BlockReferenceId { get; set; }

        /// <summary>
        ///     Gets or sets 图块定义名称.
        /// </summary>
        public string BlockName { get; set; }

        /// <summary>
        ///     Gets or sets xClip检测方法.
        /// </summary>
        public string DetectionMethod { get; set; }

        /// <summary>
        ///     Gets or sets 嵌套级别，0表示顶层图块，大于0表示嵌套图块.
        /// </summary>
        public int NestLevel { get; set; }

        /// <summary>
        ///     Gets or sets 图块插入点位置.
        /// </summary>
        public Point3d Position { get; set; }
    }
}
