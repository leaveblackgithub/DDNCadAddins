using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// 扩展方法类
    /// </summary>
    public static class AcadExtensions
    {
        /// <summary>
        /// 扩展方法：处理Windows消息队列并检查用户是否按下了ESC键
        /// </summary>
        /// <param name="hostapp">HostApplicationServices实例</param>
        /// <returns>用户是否按下了ESC键</returns>
        public static bool UserBreakWithMessagePump(this HostApplicationServices hostapp)
        {
            System.Windows.Forms.Application.DoEvents();
            return hostapp.UserBreak();
        }
    }

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
        /// 获取当前活动文档对象
        /// </summary>
        /// <returns>当前活动文档对象，如果没有打开的文档则返回null</returns>
        public Document GetMdiActiveDocument()
        {
            try
            {
                return AcadApp.DocumentManager.MdiActiveDocument;
            }
            catch (SystemException ex)
            {
                _logger.LogError($"获取当前文档失败: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 显示警告对话框
        /// </summary>
        /// <param name="message">显示的消息</param>
        public void ShowAlertDialog(string message)
        {
            try
            {
                AcadApp.ShowAlertDialog(message);
            }
            catch (SystemException ex)
            {
                _logger.LogError($"显示警告对话框失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 获取当前活动文档
        /// </summary>
        /// <returns>当前文档是否可用</returns>
        public bool GetActiveDocument(out Database database, out Editor editor)
        {
            database = null;
            editor = null;
            
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
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
                    catch (SystemException ex)
                    {
                        tr.Abort();
                        throw new SystemException($"{errorMessagePrefix}: {ex.Message}", ex);
                    }
                }
            }
            catch (SystemException ex)
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
                    catch (SystemException ex)
                    {
                        tr.Abort();
                        throw new SystemException($"{errorMessagePrefix}: {ex.Message}", ex);
                    }
                }
            }
            catch (SystemException ex)
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
            catch (SystemException ex)
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
            catch (SystemException ex)
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
            catch (SystemException ex)
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
                    Document doc = AcadApp.DocumentManager.MdiActiveDocument;
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
            catch (SystemException ex)
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
            catch (SystemException ex)
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
            catch (SystemException ex)
            {
                _logger.LogError($"检查块是否被XClip失败: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 执行XClip命令 - 必须使用命令行方式执行
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
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return OperationResult.ErrorResult("无法获取当前文档", TimeSpan.Zero);
                    
                Editor ed = doc.Editor;
                
                // 验证块引用是否有效
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 确保块引用存在并且类型正确
                        BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                        if (blockRef == null)
                            return OperationResult.ErrorResult("无效的块引用或已被删除", TimeSpan.Zero);
                        
                        // 检查块是否已被XClip
                        string checkMethod;
                        if (IsBlockXClipped(tr, blockRef, out checkMethod))
                        {
                            _logger.Log($"块已被XClip (检测方法: {checkMethod})，将先执行取消XClip");
                            // 注意：此处不返回，而是继续执行，将覆盖现有的XClip
                        }
                        
                        tr.Commit();
                    }
                    catch (SystemException ex)
                    {
                        _logger.LogError($"验证块引用失败: {ex.Message}", ex);
                        return OperationResult.ErrorResult($"验证块引用失败: {ex.Message}", DateTime.Now - startTime);
                    }
                }
                
                // 准备坐标点字符串，使用固定格式化以避免科学计数法和确保正确解析
                // 注意：使用不变区域性以确保小数点格式一致
                string minPointStr = $"{minPoint.X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{minPoint.Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
                string maxPointStr = $"{maxPoint.X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{maxPoint.Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
                
                // 使用命令行执行XCLIP（注意：必须使用命令行方式）
                using (doc.LockDocument())
                {
                    try
                    {
                        _logger.Log("=== 开始命令行执行XCLIP ===");
                        
                        // 添加命令取消监听
                        bool commandCancelled = false;
                        bool commandRunning = false;
                        CommandEventHandler commandCancelHandler = null;
                        CommandEventHandler commandEndHandler = null;
                        CommandEventHandler commandStartHandler = null;
                        
                        try
                        {
                            // 注册命令取消事件处理
                            commandCancelHandler = (sender, args) => 
                            {
                                _logger.Log($"检测到命令取消: {args.GlobalCommandName}");
                                commandCancelled = true;
                                commandRunning = false;
                                
                                // 确保后续处理能正确识别取消状态
                                try 
                                {
                                    ed.WriteMessage("\n取消检测: 命令已被取消");
                                    // 尝试进一步清理环境
                                    doc.SendStringToExecute("_CANCEL ", true, false, true);
                                } 
                                catch { }
                            };
                            
                            // 注册命令结束事件处理
                            commandEndHandler = (sender, args) => 
                            {
                                _logger.Log($"检测到命令结束: {args.GlobalCommandName}");
                                commandRunning = false;
                            };
                            
                            // 注册命令开始事件处理
                            commandStartHandler = (sender, args) => 
                            {
                                _logger.Log($"检测到命令开始: {args.GlobalCommandName}");
                                if (args.GlobalCommandName.ToUpper().Contains("XCLIP"))
                                {
                                    commandRunning = true;
                                }
                            };
                            
                            // 注册事件
                            doc.CommandCancelled += commandCancelHandler;
                            doc.CommandEnded += commandEndHandler;
                            doc.CommandWillStart += commandStartHandler;
                            
                            // 步骤1: 强化清理环境 - 确保任何可能正在执行的命令都被彻底取消
                            try 
                            {
                                _logger.Log("步骤1: 清理环境，取消可能活动的命令");
                                // 清理环境但不显示在命令行上
                                ed.WriteMessage("\n正在准备环境...");
                                
                                // 使用静默方式发送取消命令
                                doc.SendStringToExecute("\x1B\x1B", false, false, false);
                                System.Threading.Thread.Sleep(100);
                                doc.SendStringToExecute("_CANCEL ", false, false, false);
                                System.Threading.Thread.Sleep(100);
                                
                                // 尝试使用LISP方式取消命令（但不显示在命令行）
                                try {
                                    doc.SendStringToExecute("(progn (command \"\\U0003\\U0003\") (command \"CANCEL\"))", false, false, false);
                                    System.Threading.Thread.Sleep(100);
                                } catch {}
                            }
                            catch (SystemException ex)
                            {
                                _logger.LogError($"清理环境出错 (非致命): {ex.Message}", ex);
                                // 这是非致命错误，可以继续执行
                            }
                            
                            // 步骤2: 准备选择集
                            _logger.Log("步骤2: 准备选择集");
                            // 清除当前选择集
                            ed.SetImpliedSelection(new ObjectId[0]);
                            
                            // 创建一个只包含目标块的选择集
                            ObjectId[] blockSelectionIds = new ObjectId[] { blockRefId };
                            
                            // 将目标块设置为当前选择
                            ed.SetImpliedSelection(blockSelectionIds);
                            
                            // 确保块被正确选择
                            PromptSelectionResult selRes = ed.SelectImplied();
                            if (selRes.Status != PromptStatus.OK || selRes.Value.Count != 1)
                            {
                                _logger.LogError("选择块失败: 无法选择目标块", null);
                                ed.SetImpliedSelection(new ObjectId[0]);
                                return OperationResult.ErrorResult("无法选择目标块", DateTime.Now - startTime);
                            }
                            
                            _logger.Log($"已选择块 (ID:{blockRefId})");
                            
                            // 步骤3: 执行XCLIP命令
                            _logger.Log("步骤3: 执行XCLIP命令序列");
                            
                            // 为用户提供更好的视觉反馈
                            ed.WriteMessage("\n执行XCLIP命令中，如需取消请按ESC键...");
                            
                            // 方法一：使用单一命令行形式，利用AutoCAD的命令缓冲机制
                            _logger.Log("尝试执行单一命令行形式");
                            
                            // 执行XCLIP命令，然后分别发送每个选项，避免_NEW被误解
                            _logger.Log("执行完整XCLIP命令");
                            commandRunning = false; // 重置命令状态
                            commandCancelled = false;
                            
                            // 使用更加安全的方式执行XCLIP命令序列
                            try
                            {
                                // 第一步：确保选择对象可用
                                PromptSelectionResult checkSelection = ed.SelectImplied();
                                if (checkSelection.Status != PromptStatus.OK || checkSelection.Value.Count != 1)
                                {
                                    // 重新尝试设置选择集
                                    _logger.Log("重新尝试设置选择集");
                                    ed.SetImpliedSelection(blockSelectionIds);
                                    checkSelection = ed.SelectImplied();
                                }
                                
                                if (checkSelection.Status != PromptStatus.OK || checkSelection.Value.Count != 1)
                                {
                                    _logger.LogError("执行XCLIP前选择集验证失败", null);
                                    return OperationResult.ErrorResult("选择对象失败，无法执行XCLIP命令", DateTime.Now - startTime);
                                }
                                
                                // 第二步：在同一个命令操作中执行完整的XCLIP序列
                                // 使用空格分隔而不是换行符，确保命令上下文正确
                                string fullXclipCommand = $"_XCLIP _NEW _RECTANGULAR {minPointStr} {maxPointStr} ";
                                
                                _logger.Log($"发送命令: {fullXclipCommand}");
                                doc.SendStringToExecute(fullXclipCommand, true, false, false);
                                
                                // 初始化命令监控时间
                                DateTime commandStartTime = DateTime.Now;
                                TimeSpan commandTimeout = TimeSpan.FromSeconds(10);
                                int feedbackInterval = 2000; // 2秒反馈一次
                                DateTime lastFeedbackTime = DateTime.Now;
                                
                                // 等待命令完成或超时
                                while (commandRunning && !commandCancelled && (DateTime.Now - commandStartTime < commandTimeout))
                                {
                                    // 短暂等待，减少CPU使用
                                    System.Threading.Thread.Sleep(100);
                                    
                                    // 使用UserBreakWithMessagePump检测ESC键
                                    if (HostApplicationServices.Current.UserBreakWithMessagePump())
                                    {
                                        _logger.Log("检测到用户按下ESC键 (UserBreakWithMessagePump)");
                                        commandCancelled = true;
                                        break;
                                    }
                                    
                                    // 定期给用户反馈
                                    if ((DateTime.Now - lastFeedbackTime).TotalMilliseconds >= feedbackInterval)
                                    {
                                        ed.WriteMessage($"\nXCLIP命令执行中...已等待{(DateTime.Now - commandStartTime).TotalSeconds:F1}秒 (按ESC取消)");
                                        lastFeedbackTime = DateTime.Now;
                                    }
                                }
                                
                                // 检查命令状态
                                if (commandCancelled)
                                {
                                    _logger.Log("XCLIP命令已被用户取消");
                                    ed.WriteMessage("\nXCLIP命令已取消");
                                    
                                    // 静默清理环境
                                    doc.SendStringToExecute("\x1B\x1B", false, false, false);
                                    System.Threading.Thread.Sleep(50);
                                    doc.SendStringToExecute("_CANCEL ", false, false, false);
                                    
                                    return OperationResult.WarningResult("XCLIP命令已被用户取消", DateTime.Now - startTime);
                                }
                                else if (commandRunning && DateTime.Now - commandStartTime >= commandTimeout)
                                {
                                    _logger.Log("XCLIP命令执行超时");
                                    ed.WriteMessage("\nXCLIP命令执行时间过长，尝试取消...");
                                    
                                    // 静默取消命令
                                    doc.SendStringToExecute("\x1B\x1B", false, false, false);
                                    System.Threading.Thread.Sleep(100);
                                    doc.SendStringToExecute("_CANCEL ", false, false, false);
                                    
                                    commandRunning = false;
                                    return OperationResult.WarningResult("XCLIP命令执行超时，已自动取消", DateTime.Now - startTime);
                                }
                                else
                                {
                                    _logger.Log("XCLIP命令已完成");
                                }
                            }
                            catch (SystemException ex)
                            {
                                _logger.LogError($"执行XCLIP命令时发生异常: {ex.Message}", ex);
                                
                                // 清理环境
                                doc.SendStringToExecute("\x1B\x1B", false, false, false);
                                System.Threading.Thread.Sleep(100);
                                doc.SendStringToExecute("_CANCEL ", false, false, false);
                                
                                return OperationResult.ErrorResult($"执行XCLIP命令时发生异常: {ex.Message}", DateTime.Now - startTime);
                            }
                        }
                        finally
                        {
                            // 清理事件订阅
                            if (commandCancelHandler != null)
                            {
                                try
                                {
                                    doc.CommandCancelled -= commandCancelHandler;
                                    doc.CommandEnded -= commandEndHandler;
                                    doc.CommandWillStart -= commandStartHandler;
                                }
                                catch (SystemException ex)
                                {
                                    _logger.LogError($"清理命令事件失败 (非致命): {ex.Message}", ex);
                                }
                            }
                        }
                        
                        // 步骤4: 清理选择集并确保命令结束
                        _logger.Log("步骤4: 清理选择集并确保命令结束");
                        ed.SetImpliedSelection(new ObjectId[0]);
                        
                        // 添加额外的取消命令确保无命令在活动 - 静默方式
                        doc.SendStringToExecute("\x1B", false, false, false);
                        System.Threading.Thread.Sleep(50);
                        doc.SendStringToExecute("_CANCEL ", false, false, false);
                        
                        // 给用户明确提示命令已完成
                        ed.WriteMessage("\nXCLIP命令执行完毕");
                        
                        // 步骤5: 验证操作结果
                        _logger.Log("步骤5: 验证XClip操作结果");
                        bool xclipSucceeded = false;
                        string detectionMethod = "未检测到";
                        int retryCount = 0;
                        const int MAX_RETRIES = 5; // 增加验证尝试次数
                        
                        // 多次尝试检验结果，有时命令可能需要时间生效
                        while (!xclipSucceeded && retryCount < MAX_RETRIES)
                        {
                            using (Transaction trVerify = doc.Database.TransactionManager.StartTransaction())
                            {
                                BlockReference blockRef = trVerify.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                                xclipSucceeded = IsBlockXClipped(trVerify, blockRef, out detectionMethod);
                                _logger.Log($"XCLIP验证结果 (尝试 {retryCount+1}/{MAX_RETRIES}): {(xclipSucceeded ? "成功" : "失败")}，检测方法: {detectionMethod}");
                                trVerify.Commit();
                            }
                            
                            if (!xclipSucceeded && retryCount < MAX_RETRIES - 1)
                            {
                                _logger.Log($"等待验证XClip效果...");
                                // 逐渐增加等待时间, 使用退避策略
                                int waitTime = 500 * (retryCount + 1); 
                                System.Threading.Thread.Sleep(waitTime); 
                            }
                            
                            retryCount++;
                        }
                        
                        if (!xclipSucceeded)
                        {
                            _logger.LogError($"XCLIP命令执行后未检测到XClip效果 (检测方法: {detectionMethod})", null);
                            // 返回警告而非错误，因为命令可能已执行但检测失败
                            return OperationResult.WarningResult(
                                $"XCLIP命令似乎已执行，但未能检测到块被XClip (检测方法: {detectionMethod})", 
                                DateTime.Now - startTime);
                        }
                        
                        _logger.Log("=== XCLIP命令成功执行 ===");
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult.SuccessResult(
                            duration, 
                            $"成功应用XClip裁剪，范围: ({minPoint.X:F2}, {minPoint.Y:F2}) 到 ({maxPoint.X:F2}, {maxPoint.Y:F2})");
                    }
                    catch (SystemException ex)
                    {
                        // 确保清理选择集
                        try { ed.SetImpliedSelection(new ObjectId[0]); } catch { }
                        
                        // 尝试取消可能还在活动的命令（增强版）
                        try { 
                            ed.WriteMessage("\n*取消命令*");
                            // 多次发送ESC以确保命令被取消
                            for (int i = 0; i < 3; i++) {
                                doc.SendStringToExecute("\x1B", true, false, true);
                                System.Threading.Thread.Sleep(50);
                            }
                            doc.SendStringToExecute("_CANCEL ", true, false, true);
                        } catch { }
                        
                        _logger.LogError($"XCLIP命令执行失败: {ex.Message}", ex);
                        return OperationResult.ErrorResult($"XCLIP命令执行失败: {ex.Message}", DateTime.Now - startTime);
                    }
                }
            }
            catch (SystemException ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"执行XClip命令失败: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }
                _logger.LogError(errorMessage, ex);
                return OperationResult.ErrorResult(errorMessage, duration);
            }
        }
        
        /// <summary>
        /// 写入消息到命令行
        /// </summary>
        /// <param name="message">消息内容</param>
        public void WriteMessage(string message)
        {
            try
            {
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n{message}");
                }
            }
            catch (SystemException ex)
            {
                _logger.LogError($"写入命令行消息失败: {ex.Message}", ex);
            }
        }
    }
} 