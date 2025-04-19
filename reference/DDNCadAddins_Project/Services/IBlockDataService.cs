using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     图块数据服务接口.
    /// </summary>
    public interface IBlockDataService
    {
        /// <summary>
        ///     从选定的图块中提取数据.
        /// </summary>
        /// <param name="blockIds">图块对象ID集合.</param>
        /// <param name="transaction">数据库事务.</param>
        /// <returns>操作结果，包含图块数据和所有属性标签.</returns>
        OperationResult<(Dictionary<ObjectId, Dictionary<string, string>> BlockData, HashSet<string> AllAttributeTags)>
            ExtractBlockData(
                IEnumerable<ObjectId> blockIds,
                Transaction transaction);
    }
}
