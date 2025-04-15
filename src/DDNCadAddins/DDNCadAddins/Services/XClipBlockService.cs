using System;
using System.Collections.Generic;
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
    /// XClip块服务实现类 - 处理XClip相关操作
    /// </summary>
    public class XClipBlockService : IXClipBlockService
    {
        private readonly IAcadService _acadService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口</param>
        public XClipBlockService(IAcadService acadService)
        {
            _acadService = acadService ?? throw new ArgumentNullException(nameof(acadService));
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
            if (database == null)
                return OperationResult<ObjectId>.ErrorResult("数据库为空", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            string blockName = "DDNTest";
            
            // 使用AcadService执行事务操作
            var transactionResult = _acadService.ExecuteInTransaction<(ObjectId blockRefId, Point3d insertionPoint)>(database, tr => {
                // 创建测试块
                Point3d insertionPoint = new Point3d(10, 10, 0);
                ObjectId blockRefId = _acadService.CreateTestBlock(tr, blockName, insertionPoint);
                
                if (blockRefId == ObjectId.Null)
                    throw new Exception("创建测试块失败");
                    
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
            
            if (result.Success)
                return OperationResult.SuccessResult(result.ExecutionTime, result.Message);
            else
                return OperationResult.ErrorResult(result.ErrorMessage, result.ExecutionTime);
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
                    Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference)
                    {
                        // 找到嵌套的图块参照，递归处理
                        processed++;
                        ProcessBlockReference(tr, entId, xclippedBlocks, nestLevel + 1, ref processed, ref skipped);
                    }
                }
            }
            catch (System.Exception)
            {
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
            if (database == null)
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
                
            if (blockRefId == ObjectId.Null)
                return OperationResult.ErrorResult("无效的块参照ID", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            // 使用AcadService执行事务操作
            var blockInfoResult = _acadService.ExecuteInTransaction<(string blockName, string detectionMethod, Point3d? min, Point3d? max, bool alreadyClipped)>(database, tr => {
                // 获取块参照对象
                BlockReference blockRef = _acadService.GetBlockReference(tr, blockRefId);
                if (blockRef == null)
                    throw new Exception("无法获取块参照对象");
                    
                // 获取块定义名
                var blockInfo = _acadService.GetBlockInfo(tr, blockRef);
                if (!blockInfo.HasValue)
                    throw new Exception("无法获取块定义");
                    
                string blockNameResult = blockInfo.Value.BlockName;
                
                // 检查块是否已被XClip
                string detectionMethodResult;
                if (_acadService.IsBlockXClipped(tr, blockRef, out detectionMethodResult))
                {
                    return (blockNameResult, detectionMethodResult, null, null, true);
                }
                
                // 创建块的边界框
                Extents3d? extents = _acadService.GetBlockGeometricExtents(blockRef);
                if (!extents.HasValue)
                    throw new Exception("无法获取块的几何边界");
                
                // 设置裁剪点
                Point3d minPoint = extents.Value.MinPoint;
                Point3d maxPoint = extents.Value.MaxPoint;
                
                // 稍微缩小边界以创建可见的裁剪效果 (原边界的90%)
                double marginX = (maxPoint.X - minPoint.X) * 0.05;
                double marginY = (maxPoint.Y - minPoint.Y) * 0.05;
                
                minPoint = new Point3d(minPoint.X + marginX, minPoint.Y + marginY, minPoint.Z);
                maxPoint = new Point3d(maxPoint.X - marginX, maxPoint.Y - marginY, maxPoint.Z);
                
                return (blockNameResult, null, minPoint, maxPoint, false);
            }, "获取块信息");
            
            // 处理事务结果
            if (!blockInfoResult.Success)
                return OperationResult.ErrorResult(blockInfoResult.ErrorMessage, blockInfoResult.ExecutionTime);
                
            var (blockName, detectionMethod, min, max, alreadyClipped) = blockInfoResult.Data;
            
            // 如果块已被XClip，直接返回成功
            if (alreadyClipped)
            {
                return OperationResult.SuccessResult(
                    blockInfoResult.ExecutionTime,
                    $"块'{blockName}'已被XClip (方法: {detectionMethod})，无需重复裁剪");
            }
            
            // 执行XClip命令
            var xclipResult = _acadService.ExecuteXClipCommand(blockRefId, min.Value, max.Value);
            
            if (xclipResult.Success)
            {
                return OperationResult.SuccessResult(
                    TimeSpan.FromTicks(blockInfoResult.ExecutionTime.Ticks + xclipResult.ExecutionTime.Ticks),
                    $"块'{blockName}'已成功应用XClip裁剪，范围: ({min.Value.X:F2}, {min.Value.Y:F2}) 到 ({max.Value.X:F2}, {max.Value.Y:F2})");
            }
            else
            {
                return OperationResult.ErrorResult(
                    xclipResult.ErrorMessage,
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
            // 这个方法使用事务直接操作数据库，但不涉及IO或命令
            try
            {
                if (tr == null || db == null)
                    return ObjectId.Null;
                    
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null)
                    return ObjectId.Null;
                    
                // 获取模型空间
                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (modelSpace == null)
                    return ObjectId.Null;
                
                // 遍历模型空间中的所有实体
                foreach (ObjectId id in modelSpace)
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForRead);
                    if (obj is BlockReference)
                    {
                        BlockReference blockRef = obj as BlockReference;
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        
                        // 检查块名称是否匹配
                        if (blockDef.Name.StartsWith(blockName))
                        {
                            return blockRef.ObjectId;
                        }
                    }
                }
                
                // 未找到匹配的块
                return ObjectId.Null;
            }
            catch
            {
                return ObjectId.Null;
            }
        }
        
        /// <summary>
        /// 设置是否抑制日志输出 - 此方法在重构后不再需要，但为了接口兼容保留
        /// </summary>
        /// <param name="suppress">是否抑制</param>
        public void SetLoggingSuppression(bool suppress)
        {
            // 方法不再需要，但为了接口兼容保留
        }
    }
} 