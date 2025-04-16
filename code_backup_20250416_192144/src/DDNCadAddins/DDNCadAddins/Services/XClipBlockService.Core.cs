using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using DDNCadAddins.Services;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块服务实现类 - 处理XClip相关操作核心功能
    /// 按照单一职责原则拆分自原始XClipBlockService
    /// </summary>
    public partial class XClipBlockService
    {
        private readonly IAcadService _acadService;
        private readonly ILogger _logger;
        private bool _suppressLogging = false;
        
        /// <summary>
        /// 构造函数 - 注入所有依赖项
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口</param>
        /// <param name="logger">日志服务接口，可选</param>
        public XClipBlockService(IAcadService acadService, ILogger logger = null)
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
        /// 查找所有被XClip的图块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="editor">编辑器</param>
        /// <returns>操作结果，包含XClip图块列表</returns>
        public OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database, Editor editor)
        {
            if (database == null) {return OperationResult<List<XClippedBlockInfo>>.ErrorResult("数据库为空", TimeSpan.Zero);}
                
            if (editor == null) {return OperationResult<List<XClippedBlockInfo>>.ErrorResult("编辑器为空", TimeSpan.Zero);}
                
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
                
                string statusMessage = $"扫描完成: 处理了 {processed} 个图块, 跳过了 {skipped} 个图块";
                
                if (xclippedBlocks.Count == 0)
                {
                    statusMessage += " 提示: 请确保您已经使用AutoCAD的XCLIP命令对块进行了裁剪。 " +
                                    "操作步骤: 输入XCLIP命令 -> 选择块 -> 输入N(新建) -> 输入R(矩形) -> 选择裁剪边界";
                }
                
                return (xclippedBlocks, statusMessage);
            }, "查找XClip图块");
            
            // 处理事务结果
            if (transactionResult.Success)
            {
                var (xclippedBlocks, statusMessage) = transactionResult.Data;
                return OperationResult<List<XClippedBlockInfo>>.SuccessResult(
                    xclippedBlocks, 
                    transactionResult.ExecutionTime, 
                    $"搜索完成，共找到 {xclippedBlocks.Count} 个被XClip的图块。{statusMessage}");
            }
            else
            {
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult(
                    transactionResult.ErrorMessage, 
                    transactionResult.ExecutionTime);
            }
        }
        
        /// <summary>
        /// 创建测试块并返回块ID
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果，包含创建的块ID</returns>
        public OperationResult<ObjectId> CreateTestBlockWithId(Database database)
        {
            if (database == null) {return OperationResult<ObjectId>.ErrorResult("数据库为空", TimeSpan.Zero);}
                
            DateTime startTime = DateTime.Now;
            string blockName = "DDNTest";
            
            // 使用AcadService执行事务操作
            var transactionResult = _acadService.ExecuteInTransaction<(ObjectId blockRefId, Point3d insertionPoint)>(database, tr => {
                // 创建测试块
                Point3d insertionPoint = new Point3d(10, 10, 0);
                ObjectId blockRefId = _acadService.CreateTestBlock(tr, blockName, insertionPoint);
                
                if (blockRefId == ObjectId.Null) {throw new Exception("创建测试块失败");}
                    
                return (blockRefId, insertionPoint);
            }, "创建测试块");
            
            // 处理事务结果
            if (transactionResult.Success)
            {
                var (blockRefId, insertionPoint) = transactionResult.Data;
                return OperationResult<ObjectId>.SuccessResult(
                    blockRefId, 
                    transactionResult.ExecutionTime, 
                    $"成功创建测试块 '{blockName}'，插入点: ({insertionPoint.X}, {insertionPoint.Y}, {insertionPoint.Z})");
            }
            else
            {
                return OperationResult<ObjectId>.ErrorResult(
                    transactionResult.ErrorMessage, 
                    transactionResult.ExecutionTime);
            }
        }

        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        public OperationResult CreateTestBlock(Database database)
        {
            // 调用返回ObjectId版本并转换结果
            var result = CreateTestBlockWithId(database);
            
            if (result.Success) {return OperationResult.SuccessResult(result.ExecutionTime, result.Message);}
            else {return OperationResult.ErrorResult(result.ErrorMessage, result.ExecutionTime);}
        }
        
        /// <summary>
        /// 处理单个图块参照及其嵌套图块
        /// </summary>
        private void ProcessBlockReference(Transaction tr, ObjectId blockRefId, 
            List<XClippedBlockInfo> xclippedBlocks, int nestLevel, ref int processed, ref int skipped)
        {
            // 防止递归过深
            if (nestLevel > 5)
            {
                skipped++;
                return;
            }
            
            try
            {
                // 获取块参照对象
                BlockReference blockRef = _acadService.GetBlockReference(tr, blockRefId);
                if (blockRef == null)
                {
                    skipped++;
                    return;
                }
                
                // 获取块定义
                var blockInfo = _acadService.GetBlockInfo(tr, blockRef);
                if (!blockInfo.HasValue)
                {
                    skipped++;
                    return;
                }
                
                string blockName = blockInfo.Value.BlockName;
                BlockTableRecord blockDef = blockInfo.Value.BlockDef;
                
                // 检查当前图块是否被XClip
                string detectionMethod;
                if (_acadService.IsBlockXClipped(tr, blockRef, out detectionMethod))
                {
                    xclippedBlocks.Add(new XClippedBlockInfo
                    {
                        BlockReferenceId = blockRef.ObjectId,
                        BlockName = blockName,
                        DetectionMethod = detectionMethod,
                        NestLevel = nestLevel
                    });
                }
                
                // 递归处理嵌套图块
                // 检查块定义中的实体，寻找其中的图块参照
                foreach (ObjectId entId in blockDef)
                {
                    Entity entity = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (entity != null && entity is BlockReference)
                    {
                        BlockReference nestedBlockRef = entity as BlockReference;
                        processed++;
                        ProcessBlockReference(tr, nestedBlockRef.ObjectId, xclippedBlocks, nestLevel + 1, ref processed, ref skipped);
                    }
                }
            }
            catch
            {
                // 处理单个块的错误不应该影响整个操作
                skipped++;
            }
        }
        
        /// <summary>
        /// 自动对图块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockRefId">图块参照ID</param>
        /// <returns>操作结果</returns>
        public OperationResult AutoXClipBlock(Database database, ObjectId blockRefId)
        {
            // 重要说明：AUTOXCLIP是用命令行实现的，不要改为API方式
            if (database == null) {return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);}
                
            if (blockRefId == ObjectId.Null) {return OperationResult.ErrorResult("无效的块参照ID", TimeSpan.Zero);}
                
            DateTime startTime = DateTime.Now;
            
            // 日志输出块基本信息
            if (!_suppressLogging)
                _logger.Log($"开始对块(ID:{blockRefId.ToString()})执行XClip操作");
            
            // 使用AcadService执行事务操作
            var blockInfoResult = _acadService.ExecuteInTransaction<(string blockName, string detectionMethod, Point3d? min, Point3d? max, bool alreadyClipped)>(database, tr => {
                // 获取块参照对象
                BlockReference blockRef = _acadService.GetBlockReference(tr, blockRefId);
                if (blockRef == null)
                {
                    if (!_suppressLogging)
                        _logger.LogError($"无法获取块参照对象(ID:{blockRefId.ToString()})", null);
                    throw new Exception("无法获取块参照对象");
                }
                    
                // 获取块定义名
                var blockInfo = _acadService.GetBlockInfo(tr, blockRef);
                if (!blockInfo.HasValue)
                {
                    if (!_suppressLogging)
                        _logger.LogError($"无法获取块定义(ID:{blockRefId.ToString()})", null);
                    throw new Exception("无法获取块定义");
                }
                    
                string blockNameResult = blockInfo.Value.BlockName;
                
                if (!_suppressLogging)
                    _logger.Log($"处理块: '{blockNameResult}', 位置: ({blockRef.Position.X:F2}, {blockRef.Position.Y:F2})");
                
                // 检查块是否已被XClip
                string detectionMethodResult;
                if (_acadService.IsBlockXClipped(tr, blockRef, out detectionMethodResult))
                {
                    if (!_suppressLogging)
                        _logger.Log($"块'{blockNameResult}'已被XClip(方法:{detectionMethodResult})");
                    return (blockNameResult, detectionMethodResult, null, null, true);
                }
                
                // 创建块的边界框
                Extents3d? extents = _acadService.GetBlockGeometricExtents(blockRef);
                if (!extents.HasValue)
                {
                    if (!_suppressLogging)
                        _logger.LogError($"无法获取块'{blockNameResult}'的几何边界", null);
                    throw new Exception("无法获取块的几何边界");
                }
                
                // 设置裁剪点
                Point3d minPoint = extents.Value.MinPoint;
                Point3d maxPoint = extents.Value.MaxPoint;
                
                if (!_suppressLogging)
                    _logger.Log($"块'{blockNameResult}'原始边界: ({minPoint.X:F2}, {minPoint.Y:F2}) 到 ({maxPoint.X:F2}, {maxPoint.Y:F2})");
                
                // 稍微缩小边界以创建可见的裁剪效果 (原边界的90%)
                double marginX = (maxPoint.X - minPoint.X) * 0.05;
                double marginY = (maxPoint.Y - minPoint.Y) * 0.05;
                
                minPoint = new Point3d(minPoint.X + marginX, minPoint.Y + marginY, minPoint.Z);
                maxPoint = new Point3d(maxPoint.X - marginX, maxPoint.Y - marginY, maxPoint.Z);
                
                // 确保边界有实际大小
                if (Math.Abs(maxPoint.X - minPoint.X) < 0.001 || Math.Abs(maxPoint.Y - minPoint.Y) < 0.001)
                {
                    // 如果边界太小，创建一个默认大小的边界
                    minPoint = blockRef.Position - new Vector3d(5, 5, 0);
                    maxPoint = blockRef.Position + new Vector3d(5, 5, 0);
                    
                    if (!_suppressLogging)
                        _logger.Log($"块'{blockNameResult}'的几何边界太小，使用默认大小边界");
                }
                
                if (!_suppressLogging)
                    _logger.Log($"块'{blockNameResult}'计算裁剪边界: ({minPoint.X:F2}, {minPoint.Y:F2}) 到 ({maxPoint.X:F2}, {maxPoint.Y:F2})");
                
                return (blockNameResult, null, minPoint, maxPoint, false);
            }, "获取块信息");
            
            // 处理事务结果
            if (!blockInfoResult.Success)
            {
                if (!_suppressLogging)
                    _logger.LogError($"获取块信息失败: {blockInfoResult.ErrorMessage}", null);
                return OperationResult.ErrorResult(blockInfoResult.ErrorMessage, blockInfoResult.ExecutionTime);
            }
                
            var (blockName, detectionMethod, min, max, alreadyClipped) = blockInfoResult.Data;
            
            // 如果块已被XClip，直接返回成功
            if (alreadyClipped)
            {
                return OperationResult.SuccessResult(
                    blockInfoResult.ExecutionTime,
                    $"块'{blockName}'已被XClip (方法: {detectionMethod})，无需重复裁剪");
            }
            
            // 执行XClip命令
            OperationResult xclipResult = null;
            int retryCount = 0;
            int maxRetries = 3;
            Exception lastException = null;
            
            if (!_suppressLogging)
                _logger.Log($"开始对块'{blockName}'执行XCLIP命令");
            
            while (retryCount < maxRetries)
            {
                try
                {
                    if (retryCount > 0 && !_suppressLogging)
                        _logger.Log($"尝试第{retryCount + 1}次对块'{blockName}'执行XClip命令");
                    
                    // 执行XCLIP命令
                    xclipResult = _acadService.ExecuteXClipCommand(blockRefId, min.Value, max.Value);
                    
                    // 如果成功或返回了明确的错误（非异常），不再重试
                    if (xclipResult.Success || !xclipResult.Message.Contains("异常", StringComparison.Ordinal))
                    {
                        if (!_suppressLogging)
                            _logger.Log($"XCLIP命令执行结果: {(xclipResult.Success ? "成功" : "失败")}");
                        break;
                    }
                    
                    // 在重试之间添加延迟
                    System.Threading.Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (!_suppressLogging)
                        _logger.LogError($"执行XClip命令时出错 (第{retryCount + 1}次尝试): {ex.Message}", ex);
                }
                
                retryCount++;
            }
            
            // 如果所有尝试都失败了
            if (xclipResult == null)
            {
                string errorMessage = lastException != null ? 
                    $"执行XClip命令多次失败: {lastException.Message}" : 
                    "执行XClip命令失败，原因未知";
                
                if (!_suppressLogging) {_logger.LogError(errorMessage, lastException);}
                    
                return OperationResult.ErrorResult(
                    errorMessage,
                    DateTime.Now - startTime);
            }
            
            if (xclipResult.Success)
            {
                return OperationResult.SuccessResult(
                    TimeSpan.FromTicks(blockInfoResult.ExecutionTime.Ticks + xclipResult.ExecutionTime.Ticks),
                    $"块'{blockName}'已成功应用XClip裁剪，范围: ({min.Value.X:F2}, {min.Value.Y:F2}) 到 ({max.Value.X:F2}, {max.Value.Y:F2})");
            }
            else
            {
                return OperationResult.ErrorResult(
                    $"对块'{blockName}'应用XClip失败: {xclipResult.ErrorMessage}",
                    TimeSpan.FromTicks(blockInfoResult.ExecutionTime.Ticks + xclipResult.ExecutionTime.Ticks));
            }
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
            if (tr == null || db == null) {return ObjectId.Null;}
                
            try
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) {return ObjectId.Null;}
                    
                // 获取模型空间
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (ms == null) {return ObjectId.Null;}
                    
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
                            if (blockDef.Name == blockName || blockDef.Name.StartsWith(blockName))
                            {
                                return blockRef.ObjectId;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个实体的错误，继续处理下一个
                    }
                }
                
                return ObjectId.Null;
            }
            catch
            {
                return ObjectId.Null;
            }
        }
        
        /// <summary>
        /// 自动对当前图形中的所有块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        public OperationResult AutoXClipAllBlocks(Database database)
        {
            // 检查参数有效性
            if (database == null) {return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);}
            
            DateTime startTime = DateTime.Now;
            int succeeded = 0;
            int failed = 0;
            List<string> failedBlockNames = new List<string>();
            
            try
            {
                // 获取所有图块
                List<ObjectId> blockIds = new List<ObjectId>();
                var getAllBlocksResult = _acadService.ExecuteInTransaction<List<ObjectId>>(database, tr =>
                {
                    var result = new List<ObjectId>();
                    
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
                                result.Add(id);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!_suppressLogging)
                                _logger.LogError($"获取实体时出错: {ex.Message}", ex);
                            continue;
                        }
                    }
                    
                    return result;
                }, "获取所有图块");
                
                if (!getAllBlocksResult.Success)
                    return OperationResult.ErrorResult(getAllBlocksResult.ErrorMessage, TimeSpan.FromTicks((DateTime.Now - startTime).Ticks) {);}
                    
                blockIds = getAllBlocksResult.Data;
                
                // 对每个图块执行XClip操作
                foreach (ObjectId blockId in blockIds)
                {
                    try
                    {
                        var result = AutoXClipBlock(database, blockId);
                        if (result.Success)
                        {
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                            if (!string.IsNullOrEmpty(result.Message) && result.Message.Contains("'", StringComparison.Ordinal))
                            {
                                // 从错误消息中提取块名称
                                int start = result.Message.IndexOf("'", StringComparison.Ordinal) + 1;
                                int end = result.Message.IndexOf("'", start);
                                if (end > start)
                                {
                                    string blockName = result.Message.Substring(start, end - start);
                                    failedBlockNames.Add(blockName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_suppressLogging)
                            _logger.LogError($"处理图块时出错: {ex.Message}", ex);
                        failed++;
                    }
                }
                
                // 构建结果消息
                TimeSpan elapsedTime = DateTime.Now - startTime;
                string message = $"XClip操作完成: 成功{succeeded}个，失败{failed}个";
                
                if (failedBlockNames.Any())
                {
                    message += $"\n失败的图块: {string.Join(", ", failedBlockNames.Take(10))}";
                    if (failedBlockNames.Count > 10)
                        message += $" 等{failedBlockNames.Count}个";
                }
                
                return new OperationResult
                {
                    Success = true,
                    Message = message,
                    ExecutionTime = elapsedTime
                };
            }
            catch (Exception ex)
            {
                TimeSpan elapsedTime = DateTime.Now - startTime;
                return OperationResult.ErrorResult(
                    $"批量XClip操作出错: {ex.Message}. 已成功处理{succeeded}个块，失败{failed}个", 
                    elapsedTime);
            }
        }

        /// <summary>
        /// 自动XClip指定名称的所有块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockName">块名称</param>
        /// <returns>操作结果</returns>
        public OperationResult AutoXClipBlocksByName(Database database, string blockName)
        {
            // 检查参数有效性
            if (database == null) {return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);}
                
            if (string.IsNullOrWhiteSpace(blockName)) {return OperationResult.ErrorResult("块名称为空", TimeSpan.Zero);}
                
            DateTime startTime = DateTime.Now;
            int succeeded = 0;
            int failed = 0;
            
            try
            {
                // 获取指定名称的所有图块
                List<ObjectId> blockIds = new List<ObjectId>();
                var getBlocksResult = _acadService.ExecuteInTransaction<List<ObjectId>>(database, tr =>
                {
                    var result = new List<ObjectId>();
                    
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
                                    result.Add(id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!_suppressLogging)
                                _logger.LogError($"获取实体时出错: {ex.Message}", ex);
                            continue;
                        }
                    }
                    
                    return result;
                }, $"获取名称为{blockName}的图块");
                
                if (!getBlocksResult.Success)
                    return OperationResult.ErrorResult(getBlocksResult.ErrorMessage, TimeSpan.FromTicks((DateTime.Now - startTime).Ticks) {);}
                    
                blockIds = getBlocksResult.Data;
                
                // 如果未找到任何匹配的块
                if (blockIds.Count == 0)
                {
                    return OperationResult.ErrorResult($"未找到名称为'{blockName}'的图块", TimeSpan.FromTicks((DateTime.Now - startTime).Ticks));
                }
                
                // 对每个图块执行XClip操作
                foreach (ObjectId blockId in blockIds)
                {
                    try
                    {
                        var result = AutoXClipBlock(database, blockId);
                        if (result.Success)
                        {
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_suppressLogging)
                            _logger.LogError($"处理图块时出错: {ex.Message}", ex);
                        failed++;
                    }
                }
                
                // 构建结果消息
                TimeSpan elapsedTime = DateTime.Now - startTime;
                string message = $"对名称为'{blockName}'的图块执行XClip操作完成: 成功{succeeded}个，失败{failed}个";
                
                return new OperationResult
                {
                    Success = true,
                    Message = message,
                    ExecutionTime = elapsedTime
                };
            }
            catch (Exception ex)
            {
                TimeSpan elapsedTime = DateTime.Now - startTime;
                return OperationResult.ErrorResult(
                    $"对名称为'{blockName}'的图块执行XClip操作出错: {ex.Message}. 已成功处理{succeeded}个块，失败{failed}个", 
                    elapsedTime);
            }
        }
        
        /// <summary>
        /// 查找指定图形中的所有XClipped块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>包含XClip块列表的操作结果</returns>
        public OperationResult<List<XClippedBlockInfo>> FindAllXClippedBlocks(Database database)
        {
            if (database == null) {return OperationResult<List<XClippedBlockInfo>>.ErrorResult("数据库为空", TimeSpan.Zero);}
            
            DateTime startTime = DateTime.Now;
            var result = new List<XClippedBlockInfo>();
            
            // 执行事务获取所有XClipped块
            var operationResult = _acadService.ExecuteInTransaction<(List<XClippedBlockInfo> blocks, int processed, int skipped)>(
                database,
                tr => 
                {
                    var xclippedBlocks = new List<XClippedBlockInfo>();
                    int processedCount = 0;
                    int skippedCount = 0;
                    
                    // 获取模型空间中的所有块参照
                    BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    
                    foreach (ObjectId id in modelSpace)
                    {
                        try
                        {
                            // 只处理块参照
                            Entity entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (entity != null && entity is BlockReference)
                            {
                                processedCount++;
                                ProcessBlockReference(tr, id, xclippedBlocks, 0, ref processedCount, ref skippedCount);
                            }
                        }
                        catch
                        {
                            skippedCount++;
                        }
                    }
                    
                    return (xclippedBlocks, processedCount, skippedCount);
                },
                "查找所有XClipped块");
                
            if (!operationResult.Success)
            {
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult(
                    operationResult.ErrorMessage, 
                    DateTime.Now - startTime);
            }
            
            var (blocks, processed, skipped) = operationResult.Data;
            
            // 构建结果消息
            string statusMessage = $"找到{blocks.Count}个XClipped块，共处理{processed}个块，跳过{skipped}个";
            
            return new OperationResult<List<XClippedBlockInfo>>
            {
                Success = true,
                Data = blocks,
                Message = statusMessage,
                ExecutionTime = DateTime.Now - startTime
            };
        }
        
        /// <summary>
        /// 将找到的XCLIP图块移到顶层并隔离显示
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="xclippedBlocks">XClip图块列表</param>
        /// <returns>操作结果</returns>
        public OperationResult IsolateXClippedBlocks(Database database, List<XClippedBlockInfo> xclippedBlocks)
        {
            if (database == null) {return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);}
            
            if (xclippedBlocks == null || xclippedBlocks.Count == 0) {return OperationResult.SuccessResult(
                    TimeSpan.Zero, 
                    "未找到XClip图块，请先使用XCLIP命令创建被裁剪的图块");}
            
            DateTime startTime = DateTime.Now;
            
            try
            {
                // 对找到的XClip图块进行隔离处理
                var isolateResult = IsolateBlocksInView(database, xclippedBlocks);
                if (!isolateResult.Success) {return OperationResult.ErrorResult(
                        isolateResult.ErrorMessage, 
                        isolateResult.ExecutionTime);}
                
                return OperationResult.SuccessResult(
                    isolateResult.ExecutionTime,
                    $"成功隔离显示 {xclippedBlocks.Count} 个被XClip的图块。{isolateResult.Message}");
            }
            catch (Exception ex)
            {
                return OperationResult.ErrorResult(
                    $"隔离XClip图块操作失败: {ex.Message}", 
                    DateTime.Now - startTime);
            }
        }
        
        /// <summary>
        /// 在视图中隔离显示指定的图块
        /// </summary>
        /// <param name="db">当前CAD数据库</param>
        /// <param name="blocks">要隔离的图块列表</param>
        /// <returns>操作结果</returns>
        public OperationResult IsolateBlocksInView(Database db, List<XClippedBlockInfo> blocks)
        {
            DateTime startTime = DateTime.Now;
            
            try
            {
                // 使用AcadService执行事务操作
                return _acadService.ExecuteInTransaction<OperationResult>(db, tr => {
                    // 获取当前图形所有可见图层
                    var layerInfos = new Dictionary<string, bool>();
                    
                    // 1. 首先保存当前所有层的可见状态
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (ObjectId layerId in lt)
                    {
                        LayerTableRecord layer = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                        layerInfos[layer.Name] = !layer.IsOff;
                    }
                    
                    // 2. 关闭所有图层
                    foreach (ObjectId layerId in lt)
                    {
                        LayerTableRecord layer = tr.GetObject(layerId, OpenMode.ForWrite) as LayerTableRecord;
                        if (!layer.IsOff) // 只处理当前是打开的图层
                        {
                            layer.IsOff = true;
                        }
                    }
                    
                    // 3. 提取所有XClip块所在的图层并打开
                    var layersToTurnOn = new HashSet<string>();
                    foreach (var blockInfo in blocks)
                    {
                        try
                        {
                            BlockReference blockRef = tr.GetObject(blockInfo.BlockReferenceId, OpenMode.ForRead) as BlockReference;
                            if (blockRef != null)
                            {
                                // 获取块所在的图层
                                layersToTurnOn.Add(blockRef.Layer);
                                
                                // 获取块中的所有实体及其图层
                                BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                foreach (ObjectId entId in blockDef)
                                {
                                    Entity entity = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                    if (entity != null)
                                    {
                                        layersToTurnOn.Add(entity.Layer);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // 忽略单个块的错误
                        }
                    }
                    
                    // 4. 打开这些XClip块所在的图层
                    int turnedOnLayers = 0;
                    foreach (ObjectId layerId in lt)
                    {
                        LayerTableRecord layer = tr.GetObject(layerId, OpenMode.ForWrite) as LayerTableRecord;
                        if (layersToTurnOn.Contains(layer.Name) && layer.IsOff)
                        {
                            layer.IsOff = false;
                            turnedOnLayers++;
                        }
                    }
                    
                    return OperationResult.SuccessResult(
                        DateTime.Now - startTime,
                        $"打开了 {turnedOnLayers} 个相关图层用于显示XClip图块");
                }, "隔离XClip图块");
            }
            catch (Exception ex)
            {
                return OperationResult.ErrorResult(
                    $"隔离XClip图块操作失败: {ex.Message}", 
                    DateTime.Now - startTime);
            }
        }
    }
} 