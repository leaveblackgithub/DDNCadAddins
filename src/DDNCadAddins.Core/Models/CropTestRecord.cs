using System;
using System.Collections.Generic;

namespace DDNCadAddins.Core.Models
{
    /// <summary>单个被裁剪实体的几何快照（裁剪前）</summary>
    public class CropEntitySnapshot
    {
        public string ObjectId { get; set; }
        /// <summary>Polyline / Circle / Line / Arc / BlockRef</summary>
        public string Type { get; set; }
        /// <summary>Inside / Outside / OnBoundary / Intersects</summary>
        public string Containment { get; set; }
        /// <summary>Kept / Deleted / Split / Skipped</summary>
        public string Result { get; set; }
        /// <summary>关键坐标（多段线=全部顶点，圆=中心，直线=起止，弧=中心+起止）</summary>
        public List<Point2D> KeyGeometry { get; set; }
        /// <summary>额外参数（圆半径，弧半径+起止角，多段线凸度列表，Hatch 则为 [Scale, Angle]）</summary>
        public List<double> KeyParams { get; set; }
        /// <summary>扩展信息（Hatch 时存放 "PATTERN=ANSI31, PatternType=PreDefined" 等）</summary>
        public string ExtraInfo { get; set; }
    }

    /// <summary>裁剪操作完整记录 — 边界 + 实体 + 结果几何。按 UID 索引在 TestRecords/ 中查找</summary>
    public class CropTestRecord
    {
        public string Uid { get; set; }
        public DateTime Timestamp { get; set; }
        public string Command { get; set; }
        /// <summary>Inside / Outside</summary>
        public string Direction { get; set; }

        // ── 坐标系统 ──
        /// <summary>UCS 原点 (WCS)</summary>
        public Point2D UcsOrigin { get; set; }
        /// <summary>UCS X轴方向 (WCS)</summary>
        public Point2D UcsXAxis { get; set; }
        /// <summary>UCS Y轴方向 (WCS)</summary>
        public Point2D UcsYAxis { get; set; }

        // ── 边界完整几何 ──
        public int BoundaryVertexCount { get; set; }
        /// <summary>边界所有顶点 (WCS)</summary>
        public List<Point2D> BoundaryVertices { get; set; }

        // ── 被裁剪实体 ──
        public int TotalEntityCount { get; set; }
        public List<CropEntitySnapshot> Entities { get; set; }

        // ── 汇总 ──
        public bool IsSuccess { get; set; }
        public int DeletedCount { get; set; }
        public int SplitCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
        public string ErrorMessage { get; set; }
        public long ElapsedMs { get; set; }
        public string ExcludedBoundaryId { get; set; }
    }
}
