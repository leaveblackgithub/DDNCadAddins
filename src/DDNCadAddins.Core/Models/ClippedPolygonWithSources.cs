using System.Collections.Generic;

namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     段来源标记：表示裁剪结果中的段来自哪个多边形.
    /// </summary>
    public enum SegmentSource
    {
        /// <summary>来自 Subject 多边形（被裁剪的多边形 B）.</summary>
        Subject,

        /// <summary>来自 Clip 多边形（裁剪多边形 A）.</summary>
        Clip,

        /// <summary>交点（同时属于 Subject 和 Clip）.</summary>
        Intersection
    }

    /// <summary>
    ///     带来源标记的裁剪段：表示裁剪结果中的一个段及其来源.
    /// </summary>
    public class ClippedSegment
    {
        /// <summary>起点索引（在顶点列表中的索引）.</summary>
        public int StartIndex { get; set; }

        /// <summary>终点索引（在顶点列表中的索引）.</summary>
        public int EndIndex { get; set; }

        /// <summary>段的来源.</summary>
        public SegmentSource Source { get; set; }

        /// <summary>段中的顶点列表（包含起点和终点，以及中间的采样点）.</summary>
        public List<Point2D> Vertices { get; set; } = new List<Point2D>();
    }

    /// <summary>
    ///     带来源标记的裁剪多边形：包含顶点列表和每个段的来源标记.
    ///     用于混合绘制：曲线段用 CurveFit，折线段保持折线.
    /// </summary>
    public class ClippedPolygonWithSources
    {
        /// <summary>所有顶点（按顺序排列）.</summary>
        public List<Point2D> Vertices { get; set; } = new List<Point2D>();

        /// <summary>段列表：每个段包含起点/终点索引、来源和顶点.</summary>
        public List<ClippedSegment> Segments { get; set; } = new List<ClippedSegment>();

        /// <summary>是否为空多边形.</summary>
        public bool IsEmpty => Vertices.Count < 3;
    }
}