using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using Autodesk.AutoCAD.DatabaseServices.Filters;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块服务实现类 - 处理XClip相关操作
    /// </summary>
    public class XClipBlockService : IXClipBlockService
    {
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
                
            var xclippedBlocks = new List<XClippedBlockInfo>();
            DateTime startTime = DateTime.Now;
            string statusMessage = string.Empty;
            
            try
            {
                // 开始事务
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 获取块表
                        BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                        if (bt == null)
                            return OperationResult<List<XClippedBlockInfo>>.ErrorResult("无法获取块表", DateTime.Now - startTime);
                            
                        // 使用改进的方法检查图块
                        var findResult = FindAllXClippedBlocks(tr, database, editor);
                        
                        if(!findResult.Success)
                            return OperationResult<List<XClippedBlockInfo>>.ErrorResult(findResult.ErrorMessage, DateTime.Now - startTime);
                            
                        xclippedBlocks = findResult.Data;
                        statusMessage = findResult.Message;
    
                        // 提交事务
                        tr.Commit();
                    }
                    catch (System.Exception ex)
                    {
                        tr.Abort(); // 确保事务被中止
                        throw; // 重新抛出以便外层捕获
                    }
                }
                
                TimeSpan duration = DateTime.Now - startTime;
                return OperationResult<List<XClippedBlockInfo>>.SuccessResult(
                    xclippedBlocks, 
                    duration, 
                    $"搜索完成，共找到 {xclippedBlocks.Count} 个被XClip的图块。{statusMessage}");
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult(errorMessage, duration);
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
            string uniqueBlockName = blockName;
            ObjectId blockRefId = ObjectId.Null;
                
            try
            {
                // 开始事务
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 获取块表
                        BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                        if (bt == null)
                            return OperationResult<ObjectId>.ErrorResult("无法获取块表", DateTime.Now - startTime);
                            
                        // 获取模型空间
                        BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                        if (modelSpace == null)
                            return OperationResult<ObjectId>.ErrorResult("无法获取模型空间", DateTime.Now - startTime);

                        // 检查块是否已存在，如果存在则生成唯一名称
                        if (bt.Has(blockName))
                        {
                            uniqueBlockName = $"{blockName}_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                        }
                        
                        // 打开块表为写
                        bt.UpgradeOpen();
                        
                        // 创建新的块表记录
                        BlockTableRecord btr = new BlockTableRecord();
                        btr.Name = uniqueBlockName;
                        
                        // 将块表记录添加到块表中
                        ObjectId blockDefId = bt.Add(btr);
                        tr.AddNewlyCreatedDBObject(btr, true);
                        
                        // 添加一个圆到块中
                        Circle circle = new Circle();
                        circle.Center = new Point3d(0, 0, 0);
                        circle.Radius = 5;
                        
                        // 添加实体到块定义中
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                        
                        // 添加一个文本标签以便识别
                        DBText text = new DBText();
                        text.Position = new Point3d(0, 0, 0);
                        text.Height = 1.0;
                        text.TextString = uniqueBlockName;
                        text.HorizontalMode = TextHorizontalMode.TextCenter;
                        text.VerticalMode = TextVerticalMode.TextVerticalMid;
                        text.AlignmentPoint = new Point3d(0, 0, 0);
                        
                        btr.AppendEntity(text);
                        tr.AddNewlyCreatedDBObject(text, true);
                        
                        // 创建块参照 - 使用安全的方法
                        Point3d insertionPoint = new Point3d(10, 10, 0);
                        BlockReference blockRef = new BlockReference(insertionPoint, blockDefId);
                        
                        // 添加到模型空间
                        modelSpace.AppendEntity(blockRef);
                        tr.AddNewlyCreatedDBObject(blockRef, true);
                        
                        blockRefId = blockRef.ObjectId;
                        
                        tr.Commit();
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult<ObjectId>.SuccessResult(
                            blockRefId, 
                            duration, 
                            $"成功创建测试块 '{uniqueBlockName}'，插入点: ({insertionPoint.X}, {insertionPoint.Y}, {insertionPoint.Z})");
                    }
                    catch (System.Exception ex)
                    {
                        tr.Abort(); // 确保事务被中止
                        throw; // 重新抛出以便外层捕获
                    }
                }
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"创建测试块失败: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                return OperationResult<ObjectId>.ErrorResult(errorMessage, duration);
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
        /// 实现查找所有被XClip的图块 - 此方法接收Editor作为参数，而不是直接获取
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="db">数据库</param>
        /// <param name="ed">编辑器</param>
        /// <returns>操作结果</returns>
        private OperationResult<List<XClippedBlockInfo>> FindAllXClippedBlocks(Transaction tr, Database db, Editor ed)
        {
            DateTime startTime = DateTime.Now;
            var xclippedBlocks = new List<XClippedBlockInfo>();
            string statusMessage = string.Empty;
            
            try
            {
                // 使用选择集过滤器 - 选择所有图块参照
                TypedValue[] tvs = new TypedValue[] { 
                    new TypedValue((int)DxfCode.Start, "INSERT") 
                };
                
                SelectionFilter filter = new SelectionFilter(tvs);
                PromptSelectionResult selRes = ed.SelectAll(filter);
                
                if (selRes.Status == PromptStatus.OK)
                {
                    SelectionSet ss = selRes.Value;
                    ObjectId[] ids = ss.GetObjectIds();
                    
                    int totalBlocks = ids.Length;
                    
                    int processed = 0;
                    int skipped = 0;
                    
                    // 处理所有顶层图块
                    foreach (ObjectId id in ids)
                    {
                        processed++;
                        // 使用修改后的方法，不需要日志记录
                        ProcessBlockReference(tr, id, xclippedBlocks, 0, ref processed, ref skipped);
                    }
                    
                    statusMessage = $"扫描完成: 处理了 {processed} 个图块, 跳过了 {skipped} 个图块";
                    
                    if (xclippedBlocks.Count == 0)
                    {
                        statusMessage += " 提示: 请确保您已经使用AutoCAD的XCLIP命令对块进行了裁剪。 " +
                                        "操作步骤: 输入XCLIP命令 -> 选择块 -> 输入N(新建) -> 输入R(矩形) -> 选择裁剪边界";
                    }
                }
                else
                {
                    return OperationResult<List<XClippedBlockInfo>>.ErrorResult($"选择图块失败，状态: {selRes.Status}", DateTime.Now - startTime);
                }
                
                TimeSpan duration = DateTime.Now - startTime;
                return OperationResult<List<XClippedBlockInfo>>.SuccessResult(xclippedBlocks, duration, statusMessage);
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"查找XClip图块过程中发生异常: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult(errorMessage, duration);
            }
        }
        
        /// <summary>
        /// 处理单个图块参照及其嵌套图块 - 移除所有日志输出
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
                BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                if (blockRef == null)
                {
                    skipped++;
                    return;
                }
                
                // 获取块定义名
                if (blockRef.BlockTableRecord == ObjectId.Null)
                {
                    skipped++;
                    return;
                }
                
                BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (blockDef == null)
                {
                    skipped++;
                    return;
                }
                
                string blockName = blockDef.Name;
                
                // 检查当前图块是否被XClip
                string detectionMethod;
                if (IsBlockXClipped(tr, blockRef, out detectionMethod))
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
        /// 检查图块是否被XClip裁剪 - 无需修改，因为没有IO
        /// </summary>
        private bool IsBlockXClipped(Transaction tr, BlockReference blockRef, out string method)
        {
            method = "";
            
            try
            {
                // 先进行安全检查
                if (blockRef == null || blockRef.ObjectId == ObjectId.Null)
                {
                    return false;
                }
                
                // 1. 检查扩展字典中的ACAD_FILTER和SPATIAL条目 (主要检测方法)
                if (blockRef.ExtensionDictionary != ObjectId.Null)
                {
                    DBDictionary extDict = tr.GetObject(blockRef.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                    if (extDict != null)
                    {
                        // 检查ACAD_FILTER条目
                        if (extDict.Contains("ACAD_FILTER"))
                        {
                            ObjectId filterId = extDict.GetAt("ACAD_FILTER");
                            if (filterId != ObjectId.Null)
                            {
                                DBDictionary filterDict = tr.GetObject(filterId, OpenMode.ForRead) as DBDictionary;
                                if (filterDict != null && filterDict.Contains("SPATIAL"))
                                {
                                    method = "ACAD_FILTER/SPATIAL";
                                    return true;
                                }
                            }
                        }
                        
                        // 扩展检查其他可能的条目
                        foreach (DBDictionaryEntry entry in extDict)
                        {
                            if (entry.Key.Contains("CLIP") || entry.Key.Contains("SPATIAL"))
                            {
                                method = $"扩展字典包含:{entry.Key}";
                                return true;
                            }
                        }
                    }
                }
                
                // 2. 检查是否有空间过滤器 (DrawableOverrule)
                // 注意：此处无法直接检测，但可以通过上面扩展字典方法大部分情况能检测到

                // 检查失败，未发现XClip
                return false;
            }
            catch
            {
                return false;
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
            
            try
            {
                // 开始事务来获取块信息
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 获取块参照对象
                        BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                        if (blockRef == null)
                            return OperationResult.ErrorResult("无法获取块参照对象", DateTime.Now - startTime);
                            
                        // 获取块定义名
                        if (blockRef.BlockTableRecord == ObjectId.Null)
                            return OperationResult.ErrorResult("无效的块表记录ID", DateTime.Now - startTime);
                            
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (blockDef == null)
                            return OperationResult.ErrorResult("无法获取块定义", DateTime.Now - startTime);
                            
                        string blockName = blockDef.Name;
                        
                        // 检查块是否已被XClip
                        string detectionMethod;
                        if (IsBlockXClipped(tr, blockRef, out detectionMethod))
                        {
                            return OperationResult.SuccessResult(
                                DateTime.Now - startTime,
                                $"块'{blockName}'已被XClip (方法: {detectionMethod})，无需重复裁剪");
                        }
                        
                        // 创建块的边界框
                        Extents3d? extents = null;
                        try
                        {
                            extents = blockRef.GeometricExtents;
                            if (!extents.HasValue)
                                return OperationResult.ErrorResult("无法获取块的几何边界", DateTime.Now - startTime);
                        }
                        catch (Exception ex)
                        {
                            return OperationResult.ErrorResult($"获取几何边界出错: {ex.Message}", DateTime.Now - startTime);
                        }
                        
                        // 设置裁剪点
                        Point3d min = extents.Value.MinPoint;
                        Point3d max = extents.Value.MaxPoint;
                        
                        // 稍微缩小边界以创建可见的裁剪效果 (原边界的90%)
                        double marginX = (max.X - min.X) * 0.05;
                        double marginY = (max.Y - min.Y) * 0.05;
                        
                        min = new Point3d(min.X + marginX, min.Y + marginY, min.Z);
                        max = new Point3d(max.X - marginX, max.Y - marginY, max.Z);
                        
                        // 提交事务，为命令执行准备
                        tr.Commit();
                        
                        // 获取当前文档和编辑器
                        Document doc = Application.DocumentManager.MdiActiveDocument;
                        if (doc == null)
                            return OperationResult.ErrorResult("无法获取当前文档", DateTime.Now - startTime);
                            
                        Editor ed = doc.Editor;
                        
                        // 使用命令行执行XCLIP（这是AutoCAD推荐的方式）
                        using (doc.LockDocument())
                        {
                            // 创建选择集
                            ObjectIdCollection ids = new ObjectIdCollection();
                            ids.Add(blockRefId);
                            
                            // 准备坐标点字符串
                            string minPointStr = $"{min.X},{min.Y}";
                            string maxPointStr = $"{max.X},{max.Y}";
                            
                            // 执行XCLIP命令
                            // 注意：命令行参数需要根据实际AutoCAD版本可能需要调整
                            try
                            {
                                ed.Command(
                                    "_XCLIP", // 命令名称
                                    blockRefId, // 块参照ID
                                    "", // 确认选择
                                    "_N", // 新建裁剪边界
                                    "_R", // 矩形
                                    minPointStr, // 第一个点
                                    maxPointStr, // 第二个点
                                    "" // 完成命令
                                );
                            }
                            catch (Exception ex)
                            {
                                return OperationResult.ErrorResult($"XCLIP命令执行失败: {ex.Message}", DateTime.Now - startTime);
                            }
                        }
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult.SuccessResult(
                            duration, 
                            $"块'{blockName}'已成功应用XClip裁剪，范围: ({min.X:F2}, {min.Y:F2}) 到 ({max.X:F2}, {max.Y:F2})");
                    }
                    catch (Exception ex)
                    {
                        tr.Abort(); // 确保事务被中止
                        throw; // 重新抛出以便外层捕获
                    }
                }
            }
            catch (Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"自动XClip操作失败: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                return OperationResult.ErrorResult(errorMessage, duration);
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
            // 这个方法保留原样，因为它没有IO操作，仅返回一个对象ID
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