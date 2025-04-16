using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块查找服务实现类 - 负责查找被XClip的块
    /// 从原始XClipBlockService拆分出来的独立实现
    /// </summary>
    public class XClipBlockFinder : IXClipBlockFinder
    {
        private readonly IAcadService _acadService;
        private readonly ILogger _logger;
        private bool _suppressLogging = false;
        
        /// <summary>
        /// 构造函数 - 注入所有依赖项
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口</param>
        /// <param name="logger">日志服务接口，可选</param>
        public XClipBlockFinder(IAcadService acadService, ILogger logger = null)
        {
            _acadService = acadService ?? throw new ArgumentNullException(nameof(acadService));
            _logger = logger ?? new EmptyLogger();
        }
        
        /// <summary>
        /// 设置是否抑制日志输出
        /// </summary>
        /// <param name="suppress">是否抑制</param>
        public void SetLoggingSuppression(bool suppress)
        {
            _suppressLogging = suppress;
        }
        
        /// <summary>
        /// 查找所有被XClip的图块的简化方法（无参数版）
        /// </summary>
        /// <returns>被XClip的图块ID列表</returns>
        public List<ObjectId> FindXClippedBlocks()
        {
            // 获取当前活动文档
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return new List<ObjectId>();
                
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            // 调用完整版方法
            var result = FindXClippedBlocks(db, ed);
            if (result.Success && result.Data != null)
            {
                return result.Data.Select(b => b.BlockReferenceId).ToList();
            }
            
            return new List<ObjectId>();
        }
        
        /// <summary>
        /// 根据图层查找被XClip的图块
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>被XClip的图块ID列表</returns>
        public List<ObjectId> FindXClippedBlocksByLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return new List<ObjectId>();
                
            // 获取当前活动文档
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return new List<ObjectId>();
                
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            // 先获取所有被XClip的图块
            var result = FindXClippedBlocks(db, ed);
            if (!result.Success || result.Data == null)
                return new List<ObjectId>();
                
            // 筛选指定图层的图块
            List<ObjectId> filteredBlocks = new List<ObjectId>();
            
            // 使用事务查询每个块的图层
            var filteredResult = _acadService.ExecuteInTransaction<List<ObjectId>>(db, tr => {
                List<ObjectId> layerBlocks = new List<ObjectId>();
                
                foreach (var blockInfo in result.Data)
                {
                    ObjectId blockRefId = blockInfo.BlockReferenceId;
                    
                    // 获取块参照
                    BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                    if (blockRef != null)
                    {
                        // 获取块参照的图层
                        LayerTableRecord layer = tr.GetObject(blockRef.LayerId, OpenMode.ForRead) as LayerTableRecord;
                        if (layer != null && layer.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                        {
                            layerBlocks.Add(blockRefId);
                        }
                    }
                }
                
                return layerBlocks;
            }, "按图层筛选XClip图块");
            
            if (filteredResult.Success && filteredResult.Data != null)
            {
                return filteredResult.Data;
            }
            
            return new List<ObjectId>();
        }
        
        /// <summary>
        /// 查找所有被XClip的图块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="editor">编辑器</param>
        /// <returns>操作结果，包含XClip图块列表</returns>
        public OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database, Editor editor)
        {
            if (database == null)
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult("数据库为空", TimeSpan.Zero);
                
            if (editor == null)
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult("编辑器为空", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            // 使用AcadService执行事务操作
            var transactionResult = _acadService.ExecuteInTransaction<(List<XClippedBlockInfo> xclippedBlocks, string statusMessage)>(database, tr => {
                var xclippedBlocks = new List<XClippedBlockInfo>();
                
                // 获取所有块参照
                ObjectId[] blockRefIds = _acadService.FindAllBlockReferences(editor);
                
                int totalBlocks = blockRefIds.Length;
                int processed = 0;
                int skipped = 0;
                
                // 处理所有顶层图块
                foreach (ObjectId id in blockRefIds)
                {
                    processed++;
                    ProcessBlockReference(tr, id, xclippedBlocks, 0, ref processed, ref skipped);
                }
                
                string statusMessage = $"已处理 {processed} 个块参照，找到 {xclippedBlocks.Count} 个被XClip的块" + 
                                       (skipped > 0 ? $"（跳过 {skipped} 个）" : "");
                
                return (xclippedBlocks, statusMessage);
            }, "查找被XClip的块");
            
            if (!transactionResult.Success)
            {
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult(
                    transactionResult.ErrorMessage, 
                    DateTime.Now - startTime
                );
            }
            
            if (!_suppressLogging)
                _logger.LogInfo(transactionResult.Data.statusMessage);
                
            return new OperationResult<List<XClippedBlockInfo>>(
                true, 
                transactionResult.Data.xclippedBlocks, 
                transactionResult.Data.statusMessage,
                DateTime.Now - startTime
            );
        }
        
        /// <summary>
        /// 查找测试块
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="db">数据库</param>
        /// <param name="blockName">要查找的块名称，默认为"DDNTest"</param>
        /// <returns>找到的测试块ID，如未找到则返回ObjectId.Null</returns>
        public ObjectId FindTestBlock(Transaction tr, Database db, string blockName = "DDNTest")
        {
            if (tr == null || db == null)
                return ObjectId.Null;
                
            try
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null)
                    return ObjectId.Null;
                    
                // 获取模型空间
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (ms == null)
                    return ObjectId.Null;
                    
                // 遍历模型空间中的所有实体
                foreach (ObjectId id in ms)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference)
                    {
                        BlockReference blockRef = ent as BlockReference;
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        
                        // 检查块名称是否匹配
                        if (blockDef.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
                        {
                            return id;
                        }
                    }
                }
                
                return ObjectId.Null;
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"查找测试块时出错: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }
        
        /// <summary>
        /// 处理块参照，判断是否被XClip并添加到结果列表中
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRefId">块参照ID</param>
        /// <param name="xclippedBlocks">结果列表</param>
        /// <param name="nestLevel">嵌套层级</param>
        /// <param name="processed">已处理块计数器</param>
        /// <param name="skipped">已跳过块计数器</param>
        private void ProcessBlockReference(Transaction tr, ObjectId blockRefId, 
            List<XClippedBlockInfo> xclippedBlocks, int nestLevel, ref int processed, ref int skipped)
        {
            try
            {
                // 获取块参照
                BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                if (blockRef == null || blockRef.IsErased)
                {
                    skipped++;
                    return;
                }
                
                // 检查是否被XClip
                string detectionMethod;
                bool isXClipped = _acadService.IsBlockXClipped(tr, blockRef, out detectionMethod);
                
                if (isXClipped)
                {
                    // 创建被XClip的块信息对象
                    XClippedBlockInfo blockInfo = new XClippedBlockInfo
                    {
                        BlockReferenceId = blockRefId,
                        Position = blockRef.Position,
                        NestLevel = nestLevel,
                        DetectionMethod = detectionMethod
                    };
                    
                    // 获取块定义的名称
                    try
                    {
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        blockInfo.BlockName = blockDef?.Name ?? "未知";
                    }
                    catch
                    {
                        blockInfo.BlockName = "未知（访问失败）";
                    }
                    
                    // 添加到结果列表
                    xclippedBlocks.Add(blockInfo);
                }
                
                // 如果存在块表记录，处理嵌套块
                if (blockRef.BlockTableRecord.IsValid)
                {
                    // 获取块定义
                    BlockTableRecord btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (btr != null && !btr.IsAnonymous)
                    {
                        // 遍历块定义中的所有实体
                        foreach (ObjectId id in btr)
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent is BlockReference)
                            {
                                // 递归处理嵌套块
                                processed++;
                                ProcessBlockReference(tr, id, xclippedBlocks, nestLevel + 1, ref processed, ref skipped);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"处理块参照时出错: {ex.Message}", ex);
                skipped++;
            }
        }
    }
} 