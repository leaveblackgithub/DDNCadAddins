using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块服务接口 - 接口隔离原则
    /// </summary>
    public interface IXClipBlockService
    {
        /// <summary>
        /// 查找所有被XClip的图块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果，包含XClip图块列表</returns>
        OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database);
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        OperationResult CreateTestBlock(Database database);
        
        /// <summary>
        /// 自动对图块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockRefId">图块参照ID</param>
        /// <returns>操作结果</returns>
        OperationResult AutoXClipBlock(Database database, ObjectId blockRefId);
    }
} 