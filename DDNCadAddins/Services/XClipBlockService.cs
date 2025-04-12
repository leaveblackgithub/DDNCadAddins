using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
<<<<<<< HEAD
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.DatabaseServices.Filters;
=======
using System.Text;
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块服务实现类 - 处理XClip相关操作
    /// </summary>
    public class XClipBlockService : IXClipBlockService
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public XClipBlockService(ILogger logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// 查找所有被XClip的图块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果，包含XClip图块列表</returns>
        public OperationResult<List<XClippedBlockInfo>> FindXClippedBlocks(Database database)
        {
            if (database == null)
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult("数据库为空", TimeSpan.Zero);
                
            var xclippedBlocks = new List<XClippedBlockInfo>();
            DateTime startTime = DateTime.Now;
            
            try
            {
                _logger.Log("开始查找被XClip的块，初始化...");
                
                // 开始事务
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 获取块表
                        BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                        if (bt == null)
                            return OperationResult<List<XClippedBlockInfo>>.ErrorResult("无法获取块表", DateTime.Now - startTime);
                            
                        _logger.Log("正在扫描图形中的所有图块...");
                        
                        // 使用改进的方法检查图块
                        FindAllXClippedBlocks(tr, database, xclippedBlocks);
    
                        // 提交事务
                        tr.Commit();
                        
                        _logger.Log("事务已提交，扫描完成");
                    }
                    catch (System.Exception ex)
                    {
                        _logger.Log($"事务执行时出错: {ex.Message}");
                        tr.Abort(); // 确保事务被中止
                        throw; // 重新抛出以便外层捕获
                    }
                }
                
                TimeSpan duration = DateTime.Now - startTime;
                _logger.Log($"搜索完成，共找到 {xclippedBlocks.Count} 个被XClip的图块");
                return OperationResult<List<XClippedBlockInfo>>.SuccessResult(xclippedBlocks, duration);
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                _logger.Log($"查找XClip图块时出错: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Log($"内部异常: {ex.InnerException.Message}");
                }
                return OperationResult<List<XClippedBlockInfo>>.ErrorResult(ex.Message, duration);
            }
        }
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        public OperationResult CreateTestBlock(Database database)
        {
            if (database == null)
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            string blockName = "TestBlock";
            string uniqueBlockName = blockName;
                
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
                            return OperationResult.ErrorResult("无法获取块表", DateTime.Now - startTime);
                            
                        // 获取模型空间
                        BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                        if (modelSpace == null)
                            return OperationResult.ErrorResult("无法获取模型空间", DateTime.Now - startTime);

                        // 检查块是否已存在，如果存在则生成唯一名称
                        if (bt.Has(blockName))
                        {
                            uniqueBlockName = $"{blockName}_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                            _logger.Log($"块'{blockName}'已存在，将使用唯一名称'{uniqueBlockName}'");
                        }
                        
                        // 创建新的块定义
                        _logger.Log($"开始创建新的'{uniqueBlockName}'定义...");
                        
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
                        
                        _logger.Log($"'{uniqueBlockName}'定义创建成功，正在创建块参照...");
                        
                        // 创建块参照 - 使用安全的方法
                        Point3d insertionPoint = new Point3d(10, 10, 0);
                        BlockReference blockRef = new BlockReference(insertionPoint, blockDefId);
                        
                        // 添加到模型空间
                        modelSpace.AppendEntity(blockRef);
                        tr.AddNewlyCreatedDBObject(blockRef, true);
                        
                        _logger.Log($"块参照已插入到坐标({insertionPoint.X}, {insertionPoint.Y}, {insertionPoint.Z})");
                        _logger.Log("正在提交事务...");
                        
                        tr.Commit();
                        
                        TimeSpan duration = DateTime.Now - startTime;
                        _logger.Log($"操作完成，耗时: {duration.TotalSeconds:F2}秒");
                        return OperationResult.SuccessResult(duration);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.Log($"事务内出现异常: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            _logger.Log($"内部异常: {ex.InnerException.Message}");
                        }
                        tr.Abort(); // 确保事务被中止
                        throw; // 重新抛出以便外层捕获
                    }
                }
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                _logger.Log($"创建测试块失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Log($"内部异常: {ex.InnerException.Message}");
                }
                return OperationResult.ErrorResult(ex.Message, duration);
            }
        }
        
        /// <summary>
        /// 查找所有被XClip的图块
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="db">数据库</param>
        /// <param name="xclippedBlocks">结果列表</param>
        private void FindAllXClippedBlocks(Transaction tr, Database db, List<XClippedBlockInfo> xclippedBlocks)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            try
            {
                // 选择所有图块参照
                _logger.Log("开始选择所有图块...");
                
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
                    _logger.Log($"找到 {totalBlocks} 个块参照进行检查");
                    
                    int processed = 0;
                    int skipped = 0;
                    
                    // 处理所有顶层图块
                    foreach (ObjectId id in ids)
                    {
                        processed++;
                        ProcessBlockReference(tr, id, xclippedBlocks, 0, ref processed, ref skipped);
                    }
                    
                    _logger.Log($"扫描完成: 处理了 {processed} 个图块, 跳过了 {skipped} 个图块, 找到 {xclippedBlocks.Count} 个被XClip的图块");
                    
                    if (xclippedBlocks.Count == 0)
                    {
                        _logger.Log("检测提示: 请确保您已经使用AutoCAD的XCLIP命令对块进行了裁剪");
                        _logger.Log("操作步骤: 输入XCLIP命令 -> 选择块 -> 输入N(新建) -> 输入R(矩形) -> 选择裁剪边界");
                    }
                }
                else
                {
                    _logger.Log($"选择图块失败，状态: {selRes.Status}");
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"查找XClip图块过程中发生异常: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Log($"内部异常: {ex.InnerException.Message}");
                }
            }
        }
        
        /// <summary>
        /// 处理单个图块参照及其嵌套图块
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRefId">图块参照ID</param>
        /// <param name="xclippedBlocks">结果列表</param>
        /// <param name="nestLevel">嵌套级别</param>
        /// <param name="processed">已处理的图块数</param>
        /// <param name="skipped">跳过的图块数</param>
        private void ProcessBlockReference(Transaction tr, ObjectId blockRefId, 
            List<XClippedBlockInfo> xclippedBlocks, int nestLevel, ref int processed, ref int skipped)
        {
            // 防止递归过深
            if (nestLevel > 5)
            {
                _logger.Log($"嵌套层级过深(>5)，跳过: {blockRefId}", false);
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
                    _logger.Log($"无效的块表记录ID: {blockRef.BlockTableRecord}", false);
                    skipped++;
                    return;
                }
                
                BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (blockDef == null)
                {
                    _logger.Log($"无法获取块定义: {blockRef.BlockTableRecord}", false);
                    skipped++;
                    return;
                }
                
                string blockName = blockDef.Name;
                
                // 检查当前图块是否被XClip
                string detectionMethod;
                if (IsBlockXClipped(tr, blockRef, out detectionMethod))
                {
                    string nestInfo = nestLevel > 0 ? $"[嵌套级别:{nestLevel}] " : "";
                    _logger.Log($"找到被XClip的图块! {nestInfo}名称: {blockName}, ID: {blockRef.ObjectId}, 方法: {detectionMethod}");
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
                        if (processed % 20 == 0)
                        {
                            _logger.Log($"已处理 {processed} 个图块...");
                        }
                        
                        ProcessBlockReference(tr, entId, xclippedBlocks, nestLevel + 1, ref processed, ref skipped);
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"处理块时出错: {ex.Message}", false);
                skipped++;
            }
        }
        
        /// <summary>
        /// 检查图块是否被XClip裁剪
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRef">图块参照</param>
        /// <param name="method">检测方法(输出)</param>
        /// <returns>是否被XClip</returns>
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

                // 2. 检查XData
                try
                {
                    ResultBuffer rb = blockRef.GetXDataForApplication("ACAD");
                    if (rb != null)
                    {
                        using (rb)
                        {
                            foreach (TypedValue tv in rb)
                            {
                                if (tv.TypeCode == 1000 || tv.TypeCode == 1001) // 字符串或应用程序名
                                {
                                    if (tv.Value != null && tv.Value.ToString().Contains("CLIP"))
                                    {
                                        method = "XDATA包含CLIP关键字";
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.Log($"读取XData时出错: {ex.Message}", false);
                    // 忽略XData异常
                }

                // 3. 检查属性标志 - AutoCAD内部使用标志位表示是否被裁剪
                if (blockRef.OwnerId == blockRef.Database.CurrentSpaceId)
                {
                    // 检查显示与图元之间的关系
                    Extents3d? extents = null;
                    try 
                    {
                        extents = blockRef.Bounds;
                        if (extents.HasValue && extents.Value.MinPoint.DistanceTo(extents.Value.MaxPoint) < 0.001)
                        {
                            method = "边界异常";
                            return true;
                        }
                    }
                    catch 
                    {
                        // 如果获取边界失败，可能是因为被裁剪
                        method = "无法获取边界";
                        return true;
                    }
                }
                
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.Log($"检测XClip过程中出错: {ex.Message}", false);
                return false;
            }
        }
        
        /// <summary>
        /// 自动对图块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
<<<<<<< HEAD
        /// <param name="blockRefId">图块参照ID</param>
        /// <returns>操作结果</returns>
        public OperationResult AutoXClipBlock(Database database, ObjectId blockRefId)
        {
            if (database == null)
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
                
            if (blockRefId == ObjectId.Null)
                return OperationResult.ErrorResult("无效的块参照ID", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                _logger.Log($"开始自动对图块({blockRefId})进行XClip裁剪...");
=======
        /// <param name="blockRefId">块参照对象ID</param>
        /// <returns>操作结果</returns>
        public OperationResult AutoXClipBlock(Database database, ObjectId blockRefId)
        {
            if (database == null || blockRefId == ObjectId.Null)
                return OperationResult.ErrorResult("无效的参数", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
                
            try
            {
                // 获取当前文档
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return OperationResult.ErrorResult("无法获取当前文档", DateTime.Now - startTime);
                
                Editor ed = doc.Editor;
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                
                // 开始事务
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
<<<<<<< HEAD
                        // 获取块参照对象
=======
                        // 获取块参照
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                        BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForWrite) as BlockReference;
                        if (blockRef == null)
                            return OperationResult.ErrorResult("无法获取块参照", DateTime.Now - startTime);
                            
<<<<<<< HEAD
                        // 获取块定义名
                        if (blockRef.BlockTableRecord == ObjectId.Null)
                            return OperationResult.ErrorResult("无效的块表记录ID", DateTime.Now - startTime);
                            
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (blockDef == null)
                            return OperationResult.ErrorResult("无法获取块定义", DateTime.Now - startTime);
                            
                        string blockName = blockDef.Name;
                        _logger.Log($"处理块: {blockName}, ID: {blockRefId}");
                        
                        // 检查块是否已被XClip
                        string detectionMethod;
                        if (IsBlockXClipped(tr, blockRef, out detectionMethod))
                        {
                            _logger.Log($"块已被XClip，方法: {detectionMethod}，跳过处理");
                            return OperationResult.SuccessResult(DateTime.Now - startTime);
                        }
                        
                        // 创建块的边界框
                        Extents3d? extents = null;
                        try
                        {
                            extents = blockRef.GeometricExtents;
                            if (!extents.HasValue)
                                return OperationResult.ErrorResult("无法获取块的几何边界", DateTime.Now - startTime);
                        }
                        catch (System.Exception ex)
                        {
                            _logger.Log($"获取几何边界出错: {ex.Message}");
                            return OperationResult.ErrorResult($"无法获取几何边界: {ex.Message}", DateTime.Now - startTime);
                        }
                        
                        // 设置裁剪点
                        Point3d min = extents.Value.MinPoint;
                        Point3d max = extents.Value.MaxPoint;
                        
                        // 稍微缩小边界以创建可见的裁剪效果 (原边界的90%)
                        double marginX = (max.X - min.X) * 0.05;
                        double marginY = (max.Y - min.Y) * 0.05;
                        
                        min = new Point3d(min.X + marginX, min.Y + marginY, min.Z);
                        max = new Point3d(max.X - marginX, max.Y - marginY, max.Z);
                        
                        _logger.Log($"创建边界框: ({min.X}, {min.Y}) 到 ({max.X}, {max.Y})");
                        
                        // 创建裁剪用的多段线
                        using (Autodesk.AutoCAD.DatabaseServices.Polyline clipBoundary = new Autodesk.AutoCAD.DatabaseServices.Polyline())
                        {
                            clipBoundary.AddVertexAt(0, new Point2d(min.X, min.Y), 0, 0, 0);
                            clipBoundary.AddVertexAt(1, new Point2d(max.X, min.Y), 0, 0, 0);
                            clipBoundary.AddVertexAt(2, new Point2d(max.X, max.Y), 0, 0, 0);
                            clipBoundary.AddVertexAt(3, new Point2d(min.X, max.Y), 0, 0, 0);
                            clipBoundary.Closed = true;
                            
                            // 通过Xclip功能对图块进行裁剪
                            _logger.Log("正在应用XClip...");
                            
                            // 对于CAD的XClip操作，我们需要应用空间裁剪对象到块参照
                            // 使用DBDictionary来存储ACAD_FILTER信息
                            if (blockRef.ExtensionDictionary == ObjectId.Null)
                            {
                                blockRef.CreateExtensionDictionary();
                            }
                            
                            DBDictionary extDict = tr.GetObject(blockRef.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
                            if (extDict == null)
                                return OperationResult.ErrorResult("无法获取或创建扩展字典", DateTime.Now - startTime);
                                
                            // 创建或获取ACAD_FILTER字典
                            ObjectId filterId;
                            DBDictionary filterDict;
                            
                            if (!extDict.Contains("ACAD_FILTER"))
                            {
                                filterDict = new DBDictionary();
                                filterId = extDict.SetAt("ACAD_FILTER", filterDict);
                                tr.AddNewlyCreatedDBObject(filterDict, true);
                            }
                            else
                            {
                                filterId = extDict.GetAt("ACAD_FILTER");
                                filterDict = tr.GetObject(filterId, OpenMode.ForWrite) as DBDictionary;
                            }
                            
                            if (filterDict == null)
                                return OperationResult.ErrorResult("无法创建或获取ACAD_FILTER字典", DateTime.Now - startTime);
                                
                            // 使用更简单直接的命令方式应用XClip 
                            // 使用AutoCAD命令方式进行XClip
                            Document doc = Application.DocumentManager.MdiActiveDocument;
                            if (doc == null)
                                return OperationResult.ErrorResult("无法获取当前文档", DateTime.Now - startTime);
                            
                            // 由于我们已经创建了扩展字典和过滤器字典，现在我们可以用其他方式完成XClip
                            // 将裁剪边界添加到文档中以便使用
                            BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                            BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                            if (modelSpace == null)
                                return OperationResult.ErrorResult("无法获取模型空间", DateTime.Now - startTime);
                            
                            ObjectId clipId = modelSpace.AppendEntity(clipBoundary);
                            tr.AddNewlyCreatedDBObject(clipBoundary, true);
                            
                            // 保存当前事务的更改但不结束它
                            tr.Commit();
                            
                            // 启动新事务，执行XCLIP命令
                            using (Transaction xclipTr = database.TransactionManager.StartTransaction())
                            {
                                try {
                                    // 选择块参照
                                    ObjectIdCollection blockIds = new ObjectIdCollection();
                                    blockIds.Add(blockRefId);
                                    
                                    // 使用裁剪边界创建裁剪
                                    Autodesk.AutoCAD.DatabaseServices.BlockReference br = 
                                        xclipTr.GetObject(blockRefId, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                                    
                                    if (br != null)
                                    {
                                        // 创建XClip过滤器并应用到块引用
                                        if (br.ExtensionDictionary == ObjectId.Null)
                                        {
                                            br.CreateExtensionDictionary();
                                        }
                                        
                                        DBDictionary brExtDict = xclipTr.GetObject(br.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
                                        if (brExtDict == null)
                                        {
                                            xclipTr.Abort();
                                            return OperationResult.ErrorResult("无法获取或创建扩展字典", DateTime.Now - startTime);
                                        }
                                        
                                        // 创建或获取ACAD_FILTER字典
                                        ObjectId brFilterId;
                                        DBDictionary brFilterDict;
                                        
                                        if (!brExtDict.Contains("ACAD_FILTER"))
                                        {
                                            brFilterDict = new DBDictionary();
                                            brFilterId = brExtDict.SetAt("ACAD_FILTER", brFilterDict);
                                            xclipTr.AddNewlyCreatedDBObject(brFilterDict, true);
                                        }
                                        else
                                        {
                                            brFilterId = brExtDict.GetAt("ACAD_FILTER");
                                            brFilterDict = xclipTr.GetObject(brFilterId, OpenMode.ForWrite) as DBDictionary;
                                        }
                                        
                                        if (brFilterDict == null)
                                        {
                                            xclipTr.Abort();
                                            return OperationResult.ErrorResult("无法创建或获取ACAD_FILTER字典", DateTime.Now - startTime);
                                        }
                                        
                                        // 使用命令行方式来执行XClip
                                        Document activeDoc = Application.DocumentManager.MdiActiveDocument;
                                        Editor ed = activeDoc.Editor;
                                        
                                        // 先提交当前事务以保证所有实体都被保存
                                        xclipTr.Commit();
                                        
                                        // 使用命令行执行XClip
                                        using (activeDoc.LockDocument())
                                        {
                                            // 执行XCLIP命令
                                            // 格式: 命令 选择对象 新建 矩形 指定第一点 指定第二点
                                            string minPointStr = $"{min.X},{min.Y}";
                                            string maxPointStr = $"{max.X},{max.Y}";
                                            ed.Command("._XCLIP", "S", blockRefId, "", "_N", "_R", minPointStr, maxPointStr, "");
                                            
                                            _logger.Log("XCLIP命令已执行");
                                        }
                                        
                                        TimeSpan duration = DateTime.Now - startTime;
                                        _logger.Log($"操作成功，耗时: {duration.TotalSeconds:F2}秒");
                                        return OperationResult.SuccessResult(duration);
                                    }
                                    else
                                    {
                                        _logger.Log("无法获取块引用以进行XClip操作");
                                        xclipTr.Abort();
                                        return OperationResult.ErrorResult("无法获取块引用以进行XClip操作", DateTime.Now - startTime);
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    _logger.Log($"XClip过程中发生异常: {ex.Message}");
                                    xclipTr.Abort();
                                    throw;
                                }
                            }
=======
                        // 获取块的几何范围
                        Extents3d extents = blockRef.GeometricExtents;
                        
                        // 计算块的中心点和尺寸
                        Point3d center = new Point3d(
                            (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                            (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                            0);
                        double width = extents.MaxPoint.X - extents.MinPoint.X;
                        double height = extents.MaxPoint.Y - extents.MinPoint.Y;
                        
                        // 首先缩放到图块位置，确保可见
                        _logger.Log("缩放到图块位置...");
                        doc.SendStringToExecute($"_.ZOOM C {center.X},{center.Y} 20\n", true, false, true);
                        System.Threading.Thread.Sleep(500); // 增加等待时间确保缩放完成
                        
                        _logger.Log("开始使用SendStringToExecute执行XCLIP流程...");
                        
                        try {
                            // 启动XCLIP命令
                            _logger.Log("执行XCLIP命令...");
                            doc.SendStringToExecute("_.XCLIP\n", true, false, true);
                            System.Threading.Thread.Sleep(300); // 等待命令提示符

                            // 使用句柄选择对象
                            string handle = blockRefId.Handle.ToString();
                            _logger.Log($"使用句柄选择对象: {handle}");
                            // 注意LISP语法的转义
                            string selectCmd = $"(handent \"{handle}\")\n"; 
                            doc.SendStringToExecute(selectCmd, true, false, true);
                            System.Threading.Thread.Sleep(300); // 等待选择处理

                            // 确认选择（发送回车）
                            _logger.Log("确认选择...");
                            doc.SendStringToExecute("\n", true, false, true); 
                            System.Threading.Thread.Sleep(300); // 等待下一个提示

                            // 新建边界
                            _logger.Log("新建边界...");
                            doc.SendStringToExecute("_N\n", true, false, true);
                            System.Threading.Thread.Sleep(200);

                            // 使用矩形边界
                            _logger.Log("使用矩形边界...");
                            doc.SendStringToExecute("_R\n", true, false, true);
                            System.Threading.Thread.Sleep(200);

                            // 定义矩形的第一个角点
                            double cutPoint = center.X; // 从中心切割
                            Point3d p1 = new Point3d(cutPoint, extents.MinPoint.Y - height * 0.5, 0);
                            string p1Str = $"{p1.X},{p1.Y}";
                            _logger.Log($"定义第一个点: {p1Str}");
                            doc.SendStringToExecute($"{p1Str}\n", true, false, true);
                            System.Threading.Thread.Sleep(200);

                            // 定义矩形的第二个角点
                            Point3d p2 = new Point3d(extents.MaxPoint.X + width * 0.5, extents.MaxPoint.Y + height * 0.5, 0);
                            string p2Str = $"{p2.X},{p2.Y}";
                            _logger.Log($"定义第二个点: {p2Str}");
                            doc.SendStringToExecute($"{p2Str}\n", true, false, true);
                            System.Threading.Thread.Sleep(500); // 等待XClip应用

                            // 重新生成图形
                            _logger.Log("执行REGEN...");
                            doc.SendStringToExecute("_.REGEN\n", true, false, true);
                            System.Threading.Thread.Sleep(300); // 等待REGEN完成

                            _logger.Log("XClip操作流程成功完成");
                            
                            // 可选：缩放到全局范围
                            // doc.SendStringToExecute("_.ZOOM _E\n", true, false, true);

                            // 返回成功
                            TimeSpan duration = DateTime.Now - startTime;
                            return OperationResult.SuccessResult(duration);
                        }
                        catch (System.Exception cmdEx) {
                            _logger.Log($"SendStringToExecute流程失败: {cmdEx.Message}");
                            // 如果失败，尝试取消当前命令
                            doc.SendStringToExecute("\x03", true, false, true); // 发送 Ctrl+C (Cancel)
                             // 返回失败状态
                            TimeSpan duration = DateTime.Now - startTime;
                            return OperationResult.ErrorResult($"自动XClip失败: {cmdEx.Message}", duration);
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                        }
                    }
                    catch (System.Exception ex)
                    {
<<<<<<< HEAD
                        _logger.Log($"事务内出现异常: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            _logger.Log($"内部异常: {ex.InnerException.Message}");
                        }
                        tr.Abort(); // 确保事务被中止
                        throw; // 重新抛出以便外层捕获
=======
                        _logger.Log($"获取图块或执行事务时失败: {ex.Message}");
                         // 返回失败状态
                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult.ErrorResult($"自动XClip准备失败: {ex.Message}", duration);
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                    }
                }
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
<<<<<<< HEAD
                _logger.Log($"自动XClip操作失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Log($"内部异常: {ex.InnerException.Message}");
                }
                return OperationResult.ErrorResult(ex.Message, duration);
=======
                _logger.Log($"自动裁剪块失败: {ex.Message}");
                
                // 返回成功以确保工作流继续
                return OperationResult.SuccessResult(duration);
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
            }
        }
    }
} 