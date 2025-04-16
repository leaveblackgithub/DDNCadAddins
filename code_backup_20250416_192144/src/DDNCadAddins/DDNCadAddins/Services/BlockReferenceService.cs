using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using SystemException = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// 块参照服务实现类 - 负责块参照操作的所有功能
    /// </summary>
    public class BlockReferenceService : IBlockReferenceService
    {
        private readonly ILogger _logger;
        private readonly ITransactionService _transactionService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录接口</param>
        /// <param name="transactionService">事务服务</param>
        public BlockReferenceService(ILogger logger, ITransactionService transactionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
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
            if (tr == null || blockRefId == ObjectId.Null) {return null;}
                
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
            if (blockRef == null) {return null;}
                
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
            if (tr == null || blockRef == null || blockRef.BlockTableRecord == ObjectId.Null) {return null;}
                
            try
            {
                BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (blockDef == null) {return null;}
                    
                return (blockDef.Name, blockDef);
            }
            catch (SystemException ex)
            {
                _logger.LogError($"获取块属性信息失败: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="tr">事务</param>
        /// <param name="blockName">块名称</param>
        /// <param name="insertionPoint">插入点</param>
        /// <returns>创建的块参照ID</returns>
        public ObjectId CreateTestBlock(Database db, Transaction tr, string blockName, Point3d insertionPoint)
        {
            if (tr == null || db == null) {return ObjectId.Null;}
                
            try
            {
                // 获取块表
                BlockTable blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                // 定义块内容
                using (BlockTableRecord blockDef = new BlockTableRecord())
                {
                    blockDef.Name = blockName;
                    
                    // 添加一些几何图形到块定义
                    Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 5);
                    blockDef.AppendEntity(circle);
                    
                    Line line1 = new Line(new Point3d(-5, 0, 0), new Point3d(5, 0, 0));
                    blockDef.AppendEntity(line1);
                    
                    Line line2 = new Line(new Point3d(0, -5, 0), new Point3d(0, 5, 0));
                    blockDef.AppendEntity(line2);
                    
                    // 将块定义添加到块表
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                    ObjectId blockDefId = bt.Add(blockDef);
                    tr.AddNewlyCreatedDBObject(blockDef, true);
                    
                    // 创建块参照
                    BlockReference blockRef = new BlockReference(insertionPoint, blockDefId);
                    
                    // 将块参照添加到模型空间
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    ObjectId blockRefId = modelSpace.AppendEntity(blockRef);
                    tr.AddNewlyCreatedDBObject(blockRef, true);
                    
                    return blockRefId;
                }
            }
            catch (SystemException ex)
            {
                _logger.LogError($"创建测试块失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }
        
        /// <summary>
        /// 创建测试块 - 接口实现的简化版本
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockName">块名称</param>
        /// <param name="insertionPoint">插入点</param>
        /// <returns>创建的块参照ID</returns>
        public ObjectId CreateTestBlock(Transaction tr, string blockName, Point3d insertionPoint)
        {
            if (tr == null) {return ObjectId.Null;}
                
            try
            {
                // 通过当前文档获取数据库
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) {return ObjectId.Null;}
                    
                Database db = doc.Database;
                return CreateTestBlock(db, tr, blockName, insertionPoint);
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
            if (editor == null) {return new ObjectId[0];}
                
            try
            {
                // 创建块参照过滤器
                TypedValue[] filterList = new TypedValue[] {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };
                
                SelectionFilter filter = new SelectionFilter(filterList);
                
                // 选择所有块参照
                PromptSelectionResult selResult = editor.SelectAll(filter);
                if (selResult.Status == PromptStatus.OK)
                {
                    return selResult.Value.GetObjectIds();
                }
                
                return new ObjectId[0];
            }
            catch (SystemException ex)
            {
                _logger.LogError($"查找块参照失败: {ex.Message}", ex);
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
            detectionMethod = "未检测";
            
            if (tr == null || blockRef == null) {return false;}
                
            try
            {
                // 方法1: 检查块是否有边界
                try
                {
                    if (blockRef.Bounds != null && blockRef.ExtensionDictionary != ObjectId.Null)
                    {
                        detectionMethod = "Bounds检测";
                        return true;
                    }
                }
                catch { }
                
                // 方法2: 使用扩展数据
                try
                {
                    ResultBuffer xdata = blockRef.XData;
                    if (xdata != null)
                    {
                        foreach (TypedValue value in xdata)
                        {
                            if (value.TypeCode == (int)DxfCode.ExtendedDataRegAppName && 
                                value.Value.ToString().Contains("ACAD_FILTER", StringComparison.Ordinal))
                            {
                                detectionMethod = "扩展数据";
                                return true;
                            }
                        }
                    }
                }
                catch { }
                
                // 方法3: 检查扩展字典
                if (blockRef.ExtensionDictionary != ObjectId.Null)
                {
                    DBDictionary extDict = tr.GetObject(blockRef.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                    if (extDict != null && extDict.Contains("ACAD_FILTER", StringComparison.Ordinal))
                    {
                        detectionMethod = "扩展字典";
                        return true;
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
        /// 执行XClip命令
        /// </summary>
        /// <param name="blockRefId">块参照ID</param>
        /// <param name="minPoint">裁剪边界最小点</param>
        /// <param name="maxPoint">裁剪边界最大点</param>
        /// <returns>操作结果</returns>
        public OperationResult ExecuteXClipCommand(ObjectId blockRefId, Point3d minPoint, Point3d maxPoint)
        {
            if (blockRefId == ObjectId.Null) {return OperationResult.ErrorResult("无效的块参照ID", TimeSpan.Zero);}
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor editor = doc.Editor;
                
                // 确保用户可以看到块参照
                ObjectId[] objIds = { blockRefId };
                editor.SetImpliedSelection(objIds);
                
                // 使用AutoCAD命令执行XClip
                doc.SendStringToExecute($"_.XCLIP _Select ", true, false, true);
                doc.SendStringToExecute($"_.XCLIP _New _Rectangular {minPoint.X},{minPoint.Y} {maxPoint.X},{maxPoint.Y} ", true, false, true);
                
                TimeSpan duration = DateTime.Now - startTime;
                return OperationResult.SuccessResult(duration, "XClip命令成功执行");
            }
            catch (SystemException ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"执行XClip命令失败: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                return OperationResult.ErrorResult(errorMessage, duration);
            }
        }
    }
} 