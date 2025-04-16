using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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
        /// <param name="editor">编辑器</param>
        /// <returns>操作结果，包含XClip图块列表</returns>
        OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database, Editor editor);
        
        /// <summary>
        /// 查找所有被XClip的图块的简化方法（无参数版）
        /// </summary>
        /// <returns>被XClip的图块ID列表</returns>
        List<ObjectId> FindXClippedBlocks();
        
        /// <summary>
        /// 根据图层查找被XClip的图块
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>被XClip的图块ID列表</returns>
        List<ObjectId> FindXClippedBlocksByLayer(string layerName);
        
        /// <summary>
        /// 使用矩形裁剪块
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <param name="p1">矩形第一个点</param>
        /// <param name="p2">矩形第二个点</param>
        /// <returns>裁剪操作是否成功</returns>
        bool ClipBlockWithRectangle(BlockReference blockRef, Point3d p1, Point3d p2);
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        OperationResult CreateTestBlock(Database database);
        
        /// <summary>
        /// 创建测试块并返回块ID
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果，包含创建的块ID</returns>
        OperationResult<ObjectId> CreateTestBlockWithId(Database database);
        
        /// <summary>
        /// 自动对图块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockRefId">图块参照ID</param>
        /// <returns>操作结果</returns>
        OperationResult AutoXClipBlock(Database database, ObjectId blockRefId);
        
        /// <summary>
        /// 查找测试块
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="db">数据库</param>
        /// <param name="blockName">要查找的块名称，默认为"DDNTest"</param>
        /// <returns>找到的测试块ID，如未找到则返回ObjectId.Null</returns>
        ObjectId FindTestBlock(Transaction tr, Database db, string blockName = "DDNTest");
        
        /// <summary>
        /// 设置是否抑制日志输出
        /// </summary>
        /// <param name="suppress">是否抑制</param>
        void SetLoggingSuppression(bool suppress);

        /// <summary>
        /// 将找到的XCLIP图块移到顶层并隔离显示
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="xclippedBlocks">XClip图块列表</param>
        /// <returns>操作结果</returns>
        OperationResult IsolateXClippedBlocks(Database database, List<XClippedBlockInfo> xclippedBlocks);
    }
} 