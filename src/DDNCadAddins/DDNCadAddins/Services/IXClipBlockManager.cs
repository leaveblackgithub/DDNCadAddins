using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块管理服务接口 - 负责XClip块的批量管理操作
    /// 遵循接口隔离原则，从IXClipBlockService拆分
    /// </summary>
    public interface IXClipBlockManager
    {
        /// <summary>
        /// 将找到的XCLIP图块移到顶层并隔离显示
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="xclippedBlocks">XClip图块列表</param>
        /// <returns>操作结果</returns>
        OperationResult IsolateXClippedBlocks(Database database, List<XClippedBlockInfo> xclippedBlocks);
        
        /// <summary>
        /// 自动对所有块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        OperationResult AutoXClipAllBlocks(Database database);
        
        /// <summary>
        /// 根据块名称自动对块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockName">块名称</param>
        /// <returns>操作结果</returns>
        OperationResult AutoXClipBlocksByName(Database database, string blockName);
        
        /// <summary>
        /// 查找图形中所有被XClip的块（包括嵌套块）
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果，包含所有XClip块信息</returns>
        OperationResult<List<XClippedBlockInfo>> FindAllXClippedBlocks(Database database);
        
        /// <summary>
        /// 设置是否抑制日志输出
        /// </summary>
        /// <param name="suppress">是否抑制</param>
        void SetLoggingSuppression(bool suppress);
    }
} 