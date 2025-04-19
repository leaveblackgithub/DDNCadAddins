using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     XClip块查找服务接口 - 负责查找被XClip的块
    ///     遵循接口隔离原则，从IXClipBlockService拆分.
    /// </summary>
    public interface IXClipBlockFinder
    {
        /// <summary>
        ///     查找所有被XClip的图块.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="editor">编辑器.</param>
        /// <returns>操作结果，包含XClip图块列表.</returns>
        OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database, Editor editor);

        /// <summary>
        ///     查找所有被XClip的图块的简化方法（无参数版）.
        /// </summary>
        /// <returns>被XClip的图块ID列表.</returns>
        List<ObjectId> FindXClippedBlocks();

        /// <summary>
        ///     根据图层查找被XClip的图块.
        /// </summary>
        /// <param name="layerName">图层名称.</param>
        /// <returns>被XClip的图块ID列表.</returns>
        List<ObjectId> FindXClippedBlocksByLayer(string layerName);

        /// <summary>
        ///     查找测试块.
        /// </summary>
        /// <param name="tr">事务.</param>
        /// <param name="db">数据库.</param>
        /// <param name="blockName">要查找的块名称，默认为"DDNTest".</param>
        /// <returns>找到的测试块ID，如未找到则返回ObjectId.Null.</returns>
        ObjectId FindTestBlock(Transaction tr, Database db, string blockName = "DDNTest");

        /// <summary>
        ///     设置是否抑制日志输出.
        /// </summary>
        /// <param name="suppress">是否抑制.</param>
        void SetLoggingSuppression(bool suppress);
    }
}
