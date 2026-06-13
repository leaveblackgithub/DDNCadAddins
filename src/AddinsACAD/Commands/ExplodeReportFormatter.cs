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
            int colorAdjustedCount)
        {
            var name = string.IsNullOrEmpty(blockName) ? "(未知图块)" : blockName;
            return $"\n{name}: 属性转文字 {attributeTextCount} 个，" +
                   $"图层继承 {layerAdjustedCount} 个，颜色继承 {colorAdjustedCount} 个。";
        }
    }
}
