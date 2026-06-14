using DDNCadAddins.Core.Models;
using ServiceACAD;

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     图块爆炸统计的命令行格式化
    /// </summary>
    internal static class ExplodeReportFormatter
    {
        /// <summary>
        ///     格式化 Core 层爆炸报告为一行命令行文本
        /// </summary>
        /// <param name="report">爆炸报告</param>
        /// <returns>命令行文本</returns>
        public static string FormatBlockLine(BlockExplodeReport report)
        {
            return FormatBlockLine(report?.BlockName, report?.Stats);
        }

        /// <summary>
        ///     格式化 ExplodeAsShown 统计为一行命令行文本
        /// </summary>
        /// <param name="blockName">图块名称</param>
        /// <param name="stats">爆炸统计</param>
        /// <returns>命令行文本</returns>
        public static string FormatBlockLine(string blockName, ExplodeAsShownResult stats)
        {
            return FormatBlockLine(
                blockName,
                stats?.AttributeTextCount ?? 0,
                stats?.LayerAdjustedCount ?? 0,
                stats?.ColorAdjustedCount ?? 0);
        }

        /// <summary>
        ///     格式化 Core 爆炸统计为一行命令行文本
        /// </summary>
        /// <param name="blockName">图块名称</param>
        /// <param name="stats">爆炸统计</param>
        /// <returns>命令行文本</returns>
        public static string FormatBlockLine(string blockName, BlockExplodeResult stats)
        {
            return FormatBlockLine(
                blockName,
                stats?.AttributeTextCount ?? 0,
                stats?.LayerAdjustedCount ?? 0,
                stats?.ColorAdjustedCount ?? 0);
        }

        private static string FormatBlockLine(
            string blockName,
            int attributeTextCount,
            int layerAdjustedCount,
            int colorAdjustedCount,
            int index = 0,
            int totalCount = 0,
            int roundNumber = 0)
        {
            var name = string.IsNullOrEmpty(blockName) ? "(未知图块)" : blockName;
            var sequenceInfo = string.Empty;
            if (index > 0 && totalCount > 0 && roundNumber > 0)
            {
                sequenceInfo = $"\n[第{roundNumber}轮，第{index}/{totalCount}个] ";
            }

            return $"\n{name}: 属性转文字 {attributeTextCount} 个，" +
                   $"图层继承 {layerAdjustedCount} 个，颜色继承 {colorAdjustedCount} 个。";
        }

        /// <summary>
        ///     格式化单行爆炸报告（带序号信息）
        /// </summary>
        /// <param name="report">爆炸报告</param>
        /// <returns>命令行文本</returns>
        public static string FormatBlockLineWithSequence(BlockExplodeReport report)
        {
            if (report == null)
            {
                return "\n(未知图块): 属性转文字 0 个，图层继承 0 个，颜色继承 0 个。";
            }

            var name = string.IsNullOrEmpty(report.BlockName) ? "(未知图块)" : report.BlockName;
            var attributeTextCount = report.Stats?.AttributeTextCount ?? 0;
            var layerAdjustedCount = report.Stats?.LayerAdjustedCount ?? 0;
            var colorAdjustedCount = report.Stats?.ColorAdjustedCount ?? 0;

            var sequenceInfo = string.Empty;
            if (report.Index > 0 && report.TotalCount > 0 && report.RoundNumber > 0)
            {
                sequenceInfo = $"\n[第{report.RoundNumber}轮，第{report.Index}/{report.TotalCount}个] ";
            }

            var aggregatedInfo = string.Empty;
            if (report.AggregatedCount > 1)
            {
                aggregatedInfo = $"（合并{report.AggregatedCount}个同名图块）";
            }

            return $"{sequenceInfo}{name}{aggregatedInfo}: 属性转文字 {attributeTextCount} 个，" +
                   $"图层继承 {layerAdjustedCount} 个，颜色继承 {colorAdjustedCount} 个。";
        }
    }
}
