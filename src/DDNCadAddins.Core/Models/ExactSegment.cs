using System.Collections.Generic;

namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     精确子线段 — SUBTRACTCLOSEDCURVE 差集结果中的一条连续边.
    ///     <para>
    ///         每条子线段记录来源（Subject / Clip）、起止端点、
    ///         以及用于绘制的参数化曲线信息（弧段：圆心+半径+起止角；
    ///         椭圆弧：中心+长短轴+起止角；直线段：仅端点）。
    ///         所有坐标均为 WCS 二维坐标，无 AutoCAD 依赖。
    ///     </para>
    /// </summary>
    public class ExactSegment
    {
        /// <summary>段来源：Subject（曲线 A）或 Clip（曲线 B）.</summary>
        public SegmentSource Source { get; set; }

        /// <summary>段类型：Line=直线, Arc=圆弧, Ellipse=椭圆弧.</summary>
        public ExactSegmentType SegmentType { get; set; }

        /// <summary>起点（WCS）.</summary>
        public Point2D Start { get; set; }

        /// <summary>终点（WCS）.</summary>
        public Point2D End { get; set; }

        // ── Arc 参数（SegmentType == Arc 时有效）──────────────────

        /// <summary>圆弧圆心（WCS）.</summary>
        public Point2D ArcCenter { get; set; }

        /// <summary>圆弧半径.</summary>
        public double ArcRadius { get; set; }

        /// <summary>圆弧起始角（弧度）.</summary>
        public double ArcStartAngle { get; set; }

        /// <summary>圆弧终止角（弧度）.</summary>
        public double ArcEndAngle { get; set; }

        /// <summary>圆弧是否顺时针.</summary>
        public bool ArcIsClockwise { get; set; }

        // ── Ellipse 参数（SegmentType == Ellipse 时有效）──────────

        /// <summary>椭圆中心（WCS）.</summary>
        public Point2D EllipseCenter { get; set; }

        /// <summary>椭圆长轴半径.</summary>
        public double EllipseMajorRadius { get; set; }

        /// <summary>椭圆短轴半径.</summary>
        public double EllipseMinorRadius { get; set; }

        /// <summary>椭圆长轴旋转角度（弧度，0=沿X轴）.</summary>
        public double EllipseRotation { get; set; }

        /// <summary>椭圆弧起始角（弧度）.</summary>
        public double EllipseStartAngle { get; set; }

        /// <summary>椭圆弧终止角（弧度）.</summary>
        public double EllipseEndAngle { get; set; }

        /// <summary>椭圆弧是否顺时针.</summary>
        public bool EllipseIsClockwise { get; set; }

        /// <summary>
        ///     将该段采样为多边形顶点（含起止点），用于可视化绘制.
        ///     采样密度按弧长自适应，满足和弦容忍度。
        /// </summary>
        public List<Point2D> ToPolylinePoints()
        {
            var pts = new List<Point2D>();
            switch (SegmentType)
            {
                case ExactSegmentType.Line:
                    pts.Add(Start);
                    pts.Add(End);
                    break;

                case ExactSegmentType.Arc:
                    pts.AddRange(SampleArc(
                        ArcCenter, ArcRadius,
                        ArcStartAngle, ArcEndAngle, ArcIsClockwise));
                    break;

                case ExactSegmentType.Ellipse:
                    pts.AddRange(SampleEllipseArc(
                        EllipseCenter,
                        EllipseMajorRadius, EllipseMinorRadius,
                        EllipseRotation,
                        EllipseStartAngle, EllipseEndAngle,
                        EllipseIsClockwise));
                    break;
            }
            return pts;
        }

        /// <summary>
        ///     参数化采样圆弧（禁止分段采样，使用解析公式）.
        /// </summary>
        private static List<Point2D> SampleArc(
            Point2D center, double radius,
            double startAngle, double endAngle, bool isClockwise)
        {
            var pts = new List<Point2D>();

            double span = isClockwise
                ? startAngle - endAngle
                : endAngle - startAngle;
            if (span < 0) span += 2.0 * System.Math.PI;

            const double samplesPerRadian = 8.0;
            const int maxSamples = 64;
            const int minSamples = 4;

            int samples = (int)System.Math.Ceiling(span * samplesPerRadian);
            samples = System.Math.Max(minSamples, System.Math.Min(maxSamples, samples));

            double step = span / samples;
            double dir = isClockwise ? -1.0 : 1.0;

            for (int i = 0; i <= samples; i++)
            {
                double angle = startAngle + dir * step * i;
                pts.Add(new Point2D(
                    center.X + radius * System.Math.Cos(angle),
                    center.Y + radius * System.Math.Sin(angle)));
            }
            return pts;
        }

        /// <summary>
        ///     参数化采样椭圆弧（禁止分段采样，使用解析公式）.
        /// </summary>
        private static List<Point2D> SampleEllipseArc(
            Point2D center,
            double majorR, double minorR,
            double rotation,
            double startAngle, double endAngle,
            bool isClockwise)
        {
            var pts = new List<Point2D>();

            double span = isClockwise
                ? startAngle - endAngle
                : endAngle - startAngle;
            if (span < 0) span += 2.0 * System.Math.PI;

            const double samplesPerRadian = 8.0;
            const int maxSamples = 128;
            const int minSamples = 4;

            int samples = (int)System.Math.Ceiling(span * samplesPerRadian);
            samples = System.Math.Max(minSamples, System.Math.Min(maxSamples, samples));

            double step = span / samples;
            double dir = isClockwise ? -1.0 : 1.0;

            double cosRot = System.Math.Cos(rotation);
            double sinRot = System.Math.Sin(rotation);

            for (int i = 0; i <= samples; i++)
            {
                double angle = startAngle + dir * step * i;
                double cosA = System.Math.Cos(angle);
                double sinA = System.Math.Sin(angle);
                double lx = majorR * cosA;
                double ly = minorR * sinA;
                pts.Add(new Point2D(
                    center.X + lx * cosRot - ly * sinRot,
                    center.Y + lx * sinRot + ly * cosRot));
            }
            return pts;
        }
    }

    /// <summary>
    ///     精确子线段类型.
    /// </summary>
    public enum ExactSegmentType
    {
        /// <summary>直线段.</summary>
        Line,

        /// <summary>圆弧段.</summary>
        Arc,

        /// <summary>椭圆弧段.</summary>
        Ellipse
    }

    /// <summary>
    ///     精确差集结果 — 包含多条子线段组成的闭合环.
    ///     <para>
    ///         差集 A \ B 的结果可能包含 0 个或多个闭合环，
    ///         每个环由多条 <see cref="ExactSegment"/> 首尾相接组成。
    ///     </para>
    /// </summary>
    public class ExactSubtractResult
    {
        /// <summary>闭合环列表（每个环是首尾相接的子线段序列）.</summary>
        public List<List<ExactSegment>> Loops { get; set; } = new List<List<ExactSegment>>();

        /// <summary>是否为空结果（B 完全包含 A，A 被全部减去）.</summary>
        public bool IsEmpty => Loops.Count == 0;
    }
}
