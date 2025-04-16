using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块服务实现类 - 批量操作辅助方法
    /// </summary>
    public partial class XClipBlockService
    {
        /// <summary>
        /// 获取所有块引用
        /// </summary>
        /// <param name="database">当前图形数据库</param>
        /// <returns>块引用列表</returns>
        private List<BlockReference> GetAllBlockReferences(Database database)
        {
            List<BlockReference> result = new List<BlockReference>();
            
            try
            {
                var blockIds = _acadService.ExecuteInTransaction<List<ObjectId>>(database, tr =>
                {
                    var blockIdList = new List<ObjectId>();
                    
                    // 获取块表
                    BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt == null) {throw new Exception("无法获取块表");}
                        
                    // 获取模型空间
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    if (ms == null) {throw new Exception("无法获取模型空间");}
                        
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
                            if (!_suppressLogging)
                                _logger.LogError($"获取实体时出错: {ex.Message}", ex);
                        }
                    }
                    
                    return blockIdList;
                }, "获取所有块引用");
                
                if (blockIds.Success && blockIds.Data != null)
                {
                    foreach (var id in blockIds.Data)
                    {
                        var blockRef = _acadService.ExecuteInTransaction<BlockReference>(database, tr =>
                        {
                            return tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        }, "获取块引用");
                        
                        if (blockRef.Success && blockRef.Data != null)
                        {
                            result.Add(blockRef.Data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"获取所有块引用时出错: {ex.Message}", ex);
            }
            
            return result;
        }
        
        /// <summary>
        /// 根据块名称获取块引用
        /// </summary>
        /// <param name="database">当前图形数据库</param>
        /// <param name="blockName">块名称</param>
        /// <returns>匹配名称的块引用列表</returns>
        private List<BlockReference> GetBlockReferencesByName(Database database, string blockName)
        {
            List<BlockReference> result = new List<BlockReference>();
            
            try
            {
                var blockIds = _acadService.ExecuteInTransaction<List<ObjectId>>(database, tr =>
                {
                    var blockIdList = new List<ObjectId>();
                    
                    // 获取块表
                    BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt == null) {throw new Exception("无法获取块表");}
                        
                    // 获取模型空间
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    if (ms == null) {throw new Exception("无法获取模型空间");}
                        
                    // 遍历模型空间中的所有实体
                    foreach (ObjectId id in ms)
                    {
                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent is BlockReference)
                            {
                                BlockReference blockRef = ent as BlockReference;
                                BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                
                                // 检查块名称是否匹配
                                if (blockDef.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
                                {
                                    blockIdList.Add(id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!_suppressLogging)
                                _logger.LogError($"获取实体时出错: {ex.Message}", ex);
                        }
                    }
                    
                    return blockIdList;
                }, $"获取名称为{blockName}的块引用");
                
                if (blockIds.Success && blockIds.Data != null)
                {
                    foreach (var id in blockIds.Data)
                    {
                        var blockRef = _acadService.ExecuteInTransaction<BlockReference>(database, tr =>
                        {
                            return tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        }, "获取块引用");
                        
                        if (blockRef.Success && blockRef.Data != null)
                        {
                            result.Add(blockRef.Data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"获取名称为{blockName}的块引用时出错: {ex.Message}", ex);
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取块名称
        /// </summary>
        /// <param name="blockRef">块引用</param>
        /// <returns>块名称，如无法获取则返回"未知"</returns>
        private string GetBlockName(BlockReference blockRef)
        {
            try
            {
                var result = _acadService.ExecuteInTransaction<string>(null, tr =>
                {
                    BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    return blockDef?.Name ?? "未知";
                }, "获取块名称");
                
                return result.Success ? result.Data : "未知";
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"获取块名称时出错: {ex.Message}", ex);
                return "未知";
            }
        }
        
        /// <summary>
        /// 应用XClip到块
        /// </summary>
        /// <param name="database">当前图形数据库</param>
        /// <param name="blockRef">块引用</param>
        /// <returns>操作是否成功</returns>
        private bool ApplyXClipToBlock(Database database, BlockReference blockRef)
        {
            try
            {
                ObjectId blockId = blockRef.ObjectId;
                var result = AutoXClipBlock(database, blockId);
                return result.Success;
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"应用XClip到块时出错: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 检查块是否已被XClip
        /// </summary>
        /// <param name="blockRef">块引用</param>
        /// <returns>块是否已被XClip</returns>
        private bool IsBlockXClipped(BlockReference blockRef)
        {
            try
            {
                string detectionMethod;
                return _acadService.ExecuteInTransaction<bool>(null, tr =>
                {
                    return _acadService.IsBlockXClipped(tr, blockRef, out detectionMethod);
                }, "检查块是否已被XClip").Success;
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError($"检查块是否已被XClip时出错: {ex.Message}", ex);
                return false;
            }
        }
    }
} 