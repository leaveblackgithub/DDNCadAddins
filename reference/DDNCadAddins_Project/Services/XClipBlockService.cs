using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     XClip块服务实现类 - 门面模式(Facade)
    ///     组合三个具体实现类：XClipBlockFinder、XClipBlockCreator和XClipBlockManager
    ///     提供XClip块操作的完整功能集.
    /// </summary>
    public class XClipBlockService : IXClipBlockService
    {
        // 私有成员
        private readonly XClipBlockCreator blockCreator;
        private readonly XClipBlockFinder blockFinder;
        private readonly XClipBlockManager blockManager;

        // 公共成员
        /// <summary>
        /// Initializes a new instance of the <see cref="XClipBlockService"/> class.
        ///     构造函数 - 注入所有依赖项.
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口.</param>
        /// <param name="logger">日志服务接口，可选.</param>
        public XClipBlockService(IAcadService acadService, ILogger logger = null)
        {
            if (acadService == null)
            {
                throw new ArgumentNullException(nameof(acadService));
            }

            // 创建三个具体实现类的实例
            this.blockFinder = new XClipBlockFinder(acadService, logger);
            this.blockCreator = new XClipBlockCreator(acadService, logger);
            this.blockManager = new XClipBlockManager(acadService, this.blockCreator, this.blockFinder, logger);
        }

        /// <summary>
        ///     设置是否抑制日志输出.
        /// </summary>
        /// <param name="suppress">是否抑制.</param>
        public void SetLoggingSuppression(bool suppress)
        {
            this.blockFinder.SetLoggingSuppression(suppress);
            this.blockCreator.SetLoggingSuppression(suppress);
            this.blockManager.SetLoggingSuppression(suppress);
        }

        /// <summary>
        ///     查找所有被XClip的图块的简化方法（无参数版）.
        /// </summary>
        /// <returns>被XClip的图块ID列表.</returns>
        public List<ObjectId> FindXClippedBlocks()
        {
            return this.blockFinder.FindXClippedBlocks();
        }

        /// <summary>
        ///     根据图层查找被XClip的图块.
        /// </summary>
        /// <param name="layerName">图层名称.</param>
        /// <returns>被XClip的图块ID列表.</returns>
        public List<ObjectId> FindXClippedBlocksByLayer(string layerName)
        {
            return this.blockFinder.FindXClippedBlocksByLayer(layerName);
        }

        /// <summary>
        ///     查找所有被XClip的图块.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="editor">编辑器.</param>
        /// <returns>操作结果，包含XClip图块列表.</returns>
        public OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database, Editor editor)
        {
            return this.blockFinder.FindXClippedBlocks(database, editor);
        }

        /// <summary>
        ///     查找测试块.
        /// </summary>
        /// <param name="tr">事务.</param>
        /// <param name="db">数据库.</param>
        /// <param name="blockName">要查找的块名称，默认为"DDNTest".</param>
        /// <returns>找到的测试块ID，如未找到则返回ObjectId.Null.</returns>
        public ObjectId FindTestBlock(Transaction tr, Database db, string blockName = "DDNTest")
        {
            return this.blockFinder.FindTestBlock(tr, db, blockName);
        }

        /// <summary>
        ///     使用矩形裁剪块.
        /// </summary>
        /// <param name="blockRef">块参照.</param>
        /// <param name="p1">矩形第一个点.</param>
        /// <param name="p2">矩形第二个点.</param>
        /// <returns>裁剪操作是否成功.</returns>
        public bool ClipBlockWithRectangle(BlockReference blockRef, Point3d p1, Point3d p2)
        {
            return this.blockCreator.ClipBlockWithRectangle(blockRef, p1, p2);
        }

        /// <summary>
        ///     创建测试块.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <returns>操作结果.</returns>
        public OperationResult CreateTestBlock(Database database)
        {
            return this.blockCreator.CreateTestBlock(database);
        }

        /// <summary>
        ///     创建测试块并返回块ID.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <returns>操作结果，包含创建的块ID.</returns>
        public OperationResult<ObjectId> CreateTestBlockWithId(Database database)
        {
            return this.blockCreator.CreateTestBlockWithId(database);
        }

        /// <summary>
        ///     自动对图块进行XClip裁剪.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="blockRefId">图块参照ID.</param>
        /// <returns>操作结果.</returns>
        public OperationResult AutoXClipBlock(Database database, ObjectId blockRefId)
        {
            return this.blockCreator.AutoXClipBlock(database, blockRefId);
        }

        /// <summary>
        ///     将找到的XCLIP图块移到顶层并隔离显示.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="xclippedBlocks">XClip图块列表.</param>
        /// <returns>操作结果.</returns>
        public OperationResult IsolateXClippedBlocks(Database database, List<XClippedBlockInfo> xclippedBlocks)
        {
            return this.blockManager.IsolateXClippedBlocks(database, xclippedBlocks);
        }

        /// <summary>
        ///     自动对所有块进行XClip裁剪.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <returns>操作结果.</returns>
        public OperationResult AutoXClipAllBlocks(Database database)
        {
            return this.blockManager.AutoXClipAllBlocks(database);
        }

        /// <summary>
        ///     根据块名称自动对块进行XClip裁剪.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="blockName">块名称.</param>
        /// <returns>操作结果.</returns>
        public OperationResult AutoXClipBlocksByName(Database database, string blockName)
        {
            return this.blockManager.AutoXClipBlocksByName(database, blockName);
        }

        /// <summary>
        ///     查找图形中所有被XClip的块（包括嵌套块）.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <returns>操作结果，包含所有XClip块信息.</returns>
        public OperationResult<List<XClippedBlockInfo>> FindAllXClippedBlocks(Database database)
        {
            return this.blockManager.FindAllXClippedBlocks(database);
        }
    }
}
