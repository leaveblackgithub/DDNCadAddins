using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// 图块数据服务接口
    /// </summary>
    public interface IBlockDataService
    {
        /// <summary>
        /// 从选定的图块中提取数据
        /// </summary>
        /// <param name="blockIds">图块对象ID集合</param>
        /// <param name="transaction">数据库事务</param>
        /// <returns>图块数据和所有属性标签的元组</returns>
        (Dictionary<ObjectId, Dictionary<string, string>> BlockData, HashSet<string> AllAttributeTags) ExtractBlockData(
            IEnumerable<ObjectId> blockIds, 
            Transaction transaction);
    }
} 