using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// AutoCAD API服务实现类 - 所有与CAD API的交互都通过此类实现
    /// </summary>
    public class AcadService : IAcadService
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录接口</param>
        public AcadService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 获取当前活动文档
        /// </summary>
        /// <returns>当前文档是否可用</returns>
        public bool GetActiveDocument(out Database database, out Editor editor)
        {
            database = null;
            editor = null;
            
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;
                
            database = doc.Database;
            editor = doc.Editor;
            return true;
        }
        
        /// <summary>
        /// 执行事务操作
        /// </summary>
        /// <typeparam name="T">返回数据类型</typeparam>
        /// <param name="database">数据库</param>
        /// <param name="action">要在事务中执行的操作</param>
        /// <param name="errorMessagePrefix">错误消息前缀</param>
        /// <returns>操作结果</returns>
        public OperationResult<T> ExecuteInTransaction<T>(Database database, Func<Transaction, T> action, string errorMessagePrefix)
        {
            if (database == null)
                return OperationResult<T>.ErrorResult("数据库为空", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        T result = action(tr);
                        tr.Commit();
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult<T>.SuccessResult(result, duration);
                    }
                    catch (Exception ex)
                    {
                        tr.Abort();
                        throw new Exception($"{errorMessagePrefix}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"{errorMessagePrefix}: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                
                _logger.LogError(errorMessage, ex);
                return OperationResult<T>.ErrorResult(errorMessage, duration);
            }
        }
        
        /// <summary>
        /// 执行事务操作（无返回值）
        /// </summary>
        /// <param name="database">数据库</param>
        /// <param name="action">要在事务中执行的操作</param>
        /// <param name="errorMessagePrefix">错误消息前缀</param>
        /// <returns>操作结果</returns>
        public OperationResult ExecuteInTransaction(Database database, Action<Transaction> action, string errorMessagePrefix)
        {
            if (database == null)
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        action(tr);
                        tr.Commit();
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult.SuccessResult(duration);
                    }
                    catch (Exception ex)
                    {
                        tr.Abort();
                        throw new Exception($"{errorMessagePrefix}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"{errorMessagePrefix}: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                
                _logger.LogError(errorMessage, ex);
                return OperationResult.ErrorResult(errorMessage, duration);
            }
        }
        
        /// <summary>
        /// 获取块参照对象
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRefId">块参照ID</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块参照对象，如果获取失败则返回null</returns>
        public BlockReference GetBlockReference(Transaction tr, ObjectId blockRefId, OpenMode openMode = OpenMode.ForRead)
        {
            if (tr == null || blockRefId == ObjectId.Null)
                return null;
                
            try
            {
                return tr.GetObject(blockRefId, openMode) as BlockReference;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取块参照对象失败: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 获取块的几何边界
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <returns>几何边界，如果获取失败则返回null</returns>
        public Extents3d? GetBlockGeometricExtents(BlockReference blockRef)
        {
            if (blockRef == null)
                return null;
                
            try
            {
                return blockRef.GeometricExtents;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取块几何边界失败: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 获取块的属性信息
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRef">块参照</param>
        /// <returns>块名称和定义的元组，如果获取失败则返回null</returns>
        public (string BlockName, BlockTableRecord BlockDef)? GetBlockInfo(Transaction tr, BlockReference blockRef)
        {
            if (tr == null || blockRef == null || blockRef.BlockTableRecord == ObjectId.Null)
                return null;
                
            try
            {
                BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (blockDef == null)
                    return null;
                    
                return (blockDef.Name, blockDef);
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取块信息失败: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockName">块名称</param>
        /// <param name="insertionPoint">插入点</param>
        /// <returns>创建的块参照ID</returns>
        public ObjectId CreateTestBlock(Transaction tr, string blockName, Point3d insertionPoint)
        {
            if (tr == null || string.IsNullOrEmpty(blockName))
                return ObjectId.Null;
            
            try
            {
                // 获取数据库
                Database db = null;
                
                // 尝试获取事务管理器的数据库
                if (tr.TransactionManager != null && 
                    tr.TransactionManager.GetType().GetProperty("Database") != null)
                {
                    db = tr.TransactionManager.GetType().GetProperty("Database").GetValue(tr.TransactionManager) as Database;
                }
                
                // 如果上面的方法失败，尝试使用事务获取当前文档数据库
                if (db == null)
                {
                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        db = doc.Database;
                    }
                }
                
                if (db == null)
                    return ObjectId.Null;
                    
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null)
                    return ObjectId.Null;
                
                // 获取模型空间
                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (modelSpace == null)
                    return ObjectId.Null;
                
                // 检查块是否已存在，生成唯一名称
                string uniqueBlockName = blockName;
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
                
                // 创建块参照
                BlockReference blockRef = new BlockReference(insertionPoint, blockDefId);
                
                // 添加到模型空间
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                return blockRef.ObjectId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"创建测试块失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }
        
        /// <summary>
        /// 查找所有块参照
        /// </summary>
        /// <param name="editor">编辑器</param>
        /// <returns>块参照ID数组</returns>
        public ObjectId[] FindAllBlockReferences(Editor editor)
        {
            if (editor == null)
                return new ObjectId[0];
                
            try
            {
                // 使用选择集过滤器 - 选择所有图块参照
                TypedValue[] tvs = new TypedValue[] { 
                    new TypedValue((int)DxfCode.Start, "INSERT") 
                };
                
                SelectionFilter filter = new SelectionFilter(tvs);
                PromptSelectionResult selRes = editor.SelectAll(filter);
                
                if (selRes.Status == PromptStatus.OK)
                {
                    SelectionSet ss = selRes.Value;
                    return ss.GetObjectIds();
                }
                
                return new ObjectId[0];
            }
            catch (Exception ex)
            {
                _logger.LogError($"查找所有块参照失败: {ex.Message}", ex);
                return new ObjectId[0];
            }
        }
        
        /// <summary>
        /// 检查块是否被XClip
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRef">块参照</param>
        /// <param name="detectionMethod">检测方法</param>
        /// <returns>是否被XClip</returns>
        public bool IsBlockXClipped(Transaction tr, BlockReference blockRef, out string detectionMethod)
        {
            detectionMethod = "";
            
            if (tr == null || blockRef == null || blockRef.ObjectId == ObjectId.Null)
                return false;
                
            try
            {
                // 检查扩展字典中的ACAD_FILTER和SPATIAL条目 (主要检测方法)
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
                                    detectionMethod = "ACAD_FILTER/SPATIAL";
                                    return true;
                                }
                            }
                        }
                        
                        // 扩展检查其他可能的条目
                        foreach (DBDictionaryEntry entry in extDict)
                        {
                            if (entry.Key.Contains("CLIP") || entry.Key.Contains("SPATIAL"))
                            {
                                detectionMethod = $"扩展字典包含:{entry.Key}";
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"检查块是否被XClip失败: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 执行XClip命令
        /// </summary>
        /// <param name="blockRefId">块参照ID</param>
        /// <param name="minPoint">裁剪边界最小点</param>
        /// <param name="maxPoint">裁剪边界最大点</param>
        /// <returns>操作结果</returns>
        public OperationResult ExecuteXClipCommand(ObjectId blockRefId, Point3d minPoint, Point3d maxPoint)
        {
            if (blockRefId == ObjectId.Null)
                return OperationResult.ErrorResult("无效的块参照ID", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                // 获取当前文档和编辑器
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return OperationResult.ErrorResult("无法获取当前文档", TimeSpan.Zero);
                    
                Editor ed = doc.Editor;
                
                // 准备坐标点字符串
                string minPointStr = $"{minPoint.X},{minPoint.Y}";
                string maxPointStr = $"{maxPoint.X},{maxPoint.Y}";
                
                // 使用命令行执行XCLIP（这是AutoCAD推荐的方式）
                using (doc.LockDocument())
                {
                    try
                    {
                        // 执行XCLIP命令
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
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult.SuccessResult(
                            duration, 
                            $"成功应用XClip裁剪，范围: ({minPoint.X:F2}, {minPoint.Y:F2}) 到 ({maxPoint.X:F2}, {maxPoint.Y:F2})");
                    }
                    catch (Exception ex)
                    {
                        return OperationResult.ErrorResult($"XCLIP命令执行失败: {ex.Message}", DateTime.Now - startTime);
                    }
                }
            }
            catch (Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"执行XClip命令失败: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                return OperationResult.ErrorResult(errorMessage, duration);
            }
        }
    }
} 