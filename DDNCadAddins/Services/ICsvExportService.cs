using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// CSV导出服务接口
    /// </summary>
    public interface ICsvExportService
    {
        /// <summary>
        /// 导出图块数据到CSV文件
        /// </summary>
        /// <param name="blockData">图块数据字典</param>
        /// <param name="allAttributeTags">所有属性标签集合</param>
        /// <param name="csvFilePath">CSV文件保存路径</param>
        /// <returns>导出的记录数</returns>
        int ExportToCsv(
            Dictionary<ObjectId, Dictionary<string, string>> blockData,
            HashSet<string> allAttributeTags,
            string csvFilePath);
    }
} 