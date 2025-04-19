using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     XClip块管理服务实现类 - 负责XClip块的批量管理操作
    ///     从原始XClipBlockService拆分出来的独立实现.
    /// </summary>
    public class XClipBlockManager : IXClipBlockManager
    {
        private readonly IAcadService acadService;
        private readonly IXClipBlockCreator blockCreator;
        private readonly IXClipBlockFinder blockFinder;
        private readonly ILogger logger;
        private bool suppressLogging;

        /// <summary>
        /// Initializes a new instance of the <see cref="XClipBlockManager"/> class.
        ///     构造函数 - 注入所有依赖项.
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口.</param>
        /// <param name="blockCreator">XClip块创建服务.</param>
        /// <param name="blockFinder">XClip块查找服务.</param>
        /// <param name="logger">日志服务接口，可选.</param>
        public XClipBlockManager(
            IAcadService acadService,
            IXClipBlockCreator blockCreator,
            IXClipBlockFinder blockFinder,
            ILogger logger = null)
        {
            this.acadService = acadService ?? throw new ArgumentNullException(nameof(acadService));
            this.blockCreator = blockCreator ?? throw new ArgumentNullException(nameof(blockCreator));
            this.blockFinder = blockFinder ?? throw new ArgumentNullException(nameof(blockFinder));
            this.logger = logger ?? new EmptyLogger();
        }

        /// <summary>
        ///     设置是否抑制日志输出.
        /// </summary>
        /// <param name="suppress">是否抑制.</param>
        public void SetLoggingSuppression(bool suppress)
        {
            this.suppressLogging = suppress;
            this.blockCreator.SetLoggingSuppression(suppress);
            this.blockFinder.SetLoggingSuppression(suppress);
        }

        /// <summary>
        ///     将找到的XCLIP图块移到顶层并隔离显示.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="xclippedBlocks">XClip图块列表.</param>
        /// <returns>操作结果.</returns>
        public OperationResult IsolateXClippedBlocks(Database database, List<XClippedBlockInfo> xclippedBlocks)
        {
            if (database == null)
            {
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
            }

            if (xclippedBlocks == null || xclippedBlocks.Count == 0)
            {
                return OperationResult.ErrorResult("没有找到被XClip的块", TimeSpan.Zero);
            }

            DateTime startTime = DateTime.Now;

            OperationResult<bool> result = this.acadService.ExecuteInTransaction(database, tr =>
            {
                // 获取当前文档
                Editor editor = this.acadService.GetEditor();
                if (editor == null)
                {
                    throw new Exception("无法获取编辑器");
                }

                // 获取块参照ID列表
                List<ObjectId> blockIds = xclippedBlocks.Select(b => b.BlockReferenceId).ToList();

                // 创建选择集
                SelectionSet selectionSet = SelectionSet.FromObjectIds(blockIds.ToArray());

                // 隔离显示
                editor.SetImpliedSelection(selectionSet);
                _ = editor.SelectImplied();

                // 修改被选中块的颜色（可选，便于识别）
                foreach (ObjectId blockId in blockIds)
                {
                    BlockReference blockRef = tr.GetObject(blockId, OpenMode.ForWrite) as BlockReference;
                    if (blockRef != null)
                    {
                        // 保存原始颜色信息（可选）
                        // 设置高亮颜色
                        blockRef.ColorIndex = 1; // 红色
                    }
                }

                return true;
            }, "隔离XClip块");

            if (!result.Success)
            {
                return OperationResult.ErrorResult(
                    result.ErrorMessage,
                    DateTime.Now - startTime);
            }

            string message = $"已隔离显示 {xclippedBlocks.Count} 个被XClip的块";

            if (!this.suppressLogging)
            {
                this.logger.LogInfo(message);
            }

            return new OperationResult(
                true,
                message,
                DateTime.Now - startTime);
        }

        /// <summary>
        ///     自动对所有块进行XClip裁剪.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <returns>操作结果.</returns>
        public OperationResult AutoXClipAllBlocks(Database database)
        {
            if (database == null)
            {
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
            }

            DateTime startTime = DateTime.Now;

            // 获取所有块引用
            List<BlockReference> blockRefs = this.GetAllBlockReferences(database);

            if (blockRefs.Count == 0)
            {
                return OperationResult.ErrorResult("没有找到块引用", DateTime.Now - startTime);
            }

            int successCount = 0;
            int skipCount = 0;

            // 对每个块进行XClip
            foreach (BlockReference blockRef in blockRefs)
            {
                try
                {
                    OperationResult result = this.blockCreator.AutoXClipBlock(database, blockRef.ObjectId);

                    if (result.Success)
                    {
                        if (result.Message.Contains("成功裁剪"))
                        {
                            successCount++;
                        }
                        else if (result.Message.Contains("已被裁剪"))
                        {
                            skipCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!this.suppressLogging)
                    {
                        this.logger.LogError($"自动XClip块失败: {ex.Message}", ex);
                    }
                }
            }

            string message = $"处理了 {blockRefs.Count} 个块，成功裁剪 {successCount} 个，跳过 {skipCount} 个";

            if (!this.suppressLogging)
            {
                this.logger.LogInfo(message);
            }

            return new OperationResult(
                true,
                message,
                DateTime.Now - startTime);
        }

        /// <summary>
        ///     根据块名称自动对块进行XClip裁剪.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <param name="blockName">块名称.</param>
        /// <returns>操作结果.</returns>
        public OperationResult AutoXClipBlocksByName(Database database, string blockName)
        {
            if (database == null)
            {
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
            }

            if (string.IsNullOrEmpty(blockName))
            {
                return OperationResult.ErrorResult("块名称为空", TimeSpan.Zero);
            }

            DateTime startTime = DateTime.Now;

            // 获取指定名称的块引用
            List<BlockReference> blockRefs = this.GetBlockReferencesByName(database, blockName);

            if (blockRefs.Count == 0)
            {
                return OperationResult.ErrorResult($"没有找到名称为 {blockName} 的块", DateTime.Now - startTime);
            }

            int successCount = 0;
            int skipCount = 0;

            // 对每个块进行XClip
            foreach (BlockReference blockRef in blockRefs)
            {
                try
                {
                    OperationResult result = this.blockCreator.AutoXClipBlock(database, blockRef.ObjectId);

                    if (result.Success)
                    {
                        if (result.Message.Contains("成功裁剪"))
                        {
                            successCount++;
                        }
                        else if (result.Message.Contains("已被裁剪"))
                        {
                            skipCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!this.suppressLogging)
                    {
                        this.logger.LogError($"自动XClip块失败: {ex.Message}", ex);
                    }
                }
            }

            string message = $"处理了 {blockRefs.Count} 个名称为 {blockName} 的块，成功裁剪 {successCount} 个，跳过 {skipCount} 个";

            if (!this.suppressLogging)
            {
                this.logger.LogInfo(message);
            }

            return new OperationResult(
                true,
                message,
                DateTime.Now - startTime);
        }

        /// <summary>
        ///     查找图形中所有被XClip的块（包括嵌套块）.
        /// </summary>
        /// <param name="database">当前CAD数据库.</param>
        /// <returns>操作结果，包含所有XClip块信息.</returns>
        public OperationResult<List<XClippedBlockInfo>> FindAllXClippedBlocks(Database database)
        {
            if (database == null)
            {
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult("数据库为空", TimeSpan.Zero);
            }

            Editor editor = this.acadService.GetEditor();
            return editor == null
                ? OperationResult<List<XClippedBlockInfo>>.ErrorResult("无法获取编辑器", TimeSpan.Zero)
                : this.blockFinder.FindXClippedBlocks(database, editor);
        }

        /// <summary>
        ///     获取所有块引用.
        /// </summary>
        /// <param name="database">当前图形数据库.</param>
        /// <returns>块引用列表.</returns>
        private List<BlockReference> GetAllBlockReferences(Database database)
        {
            List<BlockReference> result = new List<BlockReference>();

            try
            {
                OperationResult<List<ObjectId>> blockIds = this.acadService.ExecuteInTransaction(database, tr =>
                {
                    List<ObjectId> blockIdList = new List<ObjectId>();

                    // 获取块表
                    BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt == null)
                    {
                        throw new Exception("无法获取块表");
                    }

                    // 获取模型空间
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    if (ms == null)
                    {
                        throw new Exception("无法获取模型空间");
                    }

                    // 遍历模型空间中的所有实体
                    foreach (ObjectId id in ms)
                    {
                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent is BlockReference)
                            {
                                blockIdList.Add(id);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!this.suppressLogging)
                            {
                                this.logger.LogError($"获取实体时出错: {ex.Message}", ex);
                            }
                        }
                    }

                    return blockIdList;
                }, "获取所有块引用");

                if (blockIds.Success && blockIds.Data != null)
                {
                    foreach (ObjectId id in blockIds.Data)
                    {
                        OperationResult<BlockReference> blockRef = this.acadService.ExecuteInTransaction(
                            database,
                            tr => { return tr.GetObject(id, OpenMode.ForRead) as BlockReference; }, "获取块引用");

                        if (blockRef.Success && blockRef.Data != null)
                        {
                            result.Add(blockRef.Data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!this.suppressLogging)
                {
                    this.logger.LogError($"获取所有块引用时出错: {ex.Message}", ex);
                }
            }

            return result;
        }

        /// <summary>
        ///     根据块名称获取块引用.
        /// </summary>
        /// <param name="database">当前图形数据库.</param>
        /// <param name="blockName">块名称.</param>
        /// <returns>匹配名称的块引用列表.</returns>
        private List<BlockReference> GetBlockReferencesByName(Database database, string blockName)
        {
            List<BlockReference> result = new List<BlockReference>();

            try
            {
                OperationResult<List<ObjectId>> blockIds = this.acadService.ExecuteInTransaction(database, tr =>
                {
                    List<ObjectId> blockIdList = new List<ObjectId>();

                    // 获取块表
                    BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt == null)
                    {
                        throw new Exception("无法获取块表");
                    }

                    // 获取模型空间
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    if (ms == null)
                    {
                        throw new Exception("无法获取模型空间");
                    }

                    // 遍历模型空间中的所有实体
                    foreach (ObjectId id in ms)
                    {
                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent is BlockReference)
                            {
                                BlockReference blockRef = ent as BlockReference;
                                BlockTableRecord blockDef =
                                    tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;

                                // 检查块名称是否匹配
                                if (blockDef.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
                                {
                                    blockIdList.Add(id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!this.suppressLogging)
                            {
                                this.logger.LogError($"获取实体时出错: {ex.Message}", ex);
                            }
                        }
                    }

                    return blockIdList;
                }, $"获取名称为{blockName}的块引用");

                if (blockIds.Success && blockIds.Data != null)
                {
                    foreach (ObjectId id in blockIds.Data)
                    {
                        OperationResult<BlockReference> blockRef = this.acadService.ExecuteInTransaction(
                            database,
                            tr => { return tr.GetObject(id, OpenMode.ForRead) as BlockReference; }, "获取块引用");

                        if (blockRef.Success && blockRef.Data != null)
                        {
                            result.Add(blockRef.Data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!this.suppressLogging)
                {
                    this.logger.LogError($"获取名称为{blockName}的块引用时出错: {ex.Message}", ex);
                }
            }

            return result;
        }
    }
}
