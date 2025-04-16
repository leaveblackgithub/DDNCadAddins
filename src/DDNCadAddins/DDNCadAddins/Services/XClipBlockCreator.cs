using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using Exception = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块创建服务实现类 - 负责创建和应用XClip相关操作
    /// 从原始XClipBlockService拆分出来的独立实现
    /// </summary>
    public class XClipBlockCreator : IXClipBlockCreator
    {
        private readonly IAcadService _acadService;
        private readonly ILogger _logger;
        private bool _suppressLogging = false;
        
        /// <summary>
        /// 构造函数 - 注入所有依赖项
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口</param>
        /// <param name="logger">日志服务接口，可选</param>
        public XClipBlockCreator(IAcadService acadService, ILogger logger = null)
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
        /// 使用矩形裁剪块
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <param name="p1">矩形第一个点</param>
        /// <param name="p2">矩形第二个点</param>
        /// <returns>裁剪操作是否成功</returns>
        public bool ClipBlockWithRectangle(BlockReference blockRef, Point3d p1, Point3d p2)
        {
            if (blockRef == null || blockRef.IsDisposed)
                return false;
                
            try
            {
                // 获取当前数据库和文档
                Database db = blockRef.Database;
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (db == null || doc == null)
                    return false;
                
                // !!!重要提示!!! 
                // XClip操作应优先使用命令行方式执行，而非直接操作API
                // 原因：
                // 1. 命令行方式更稳定，与AutoCAD内部实现一致
                // 2. API中的SpatialFilter在不同版本中可能存在兼容性问题
                // 3. 命令行方式可以激活AutoCAD的撤销机制
                
                // 保存当前选择集状态
                doc.Editor.SetImpliedSelection(new ObjectId[] { blockRef.ObjectId });
                
                // 使用命令行执行XCLIP命令
                // 命令格式: XCLIP [块参照] [R(矩形)/P(多边形)/D(删除)/ON/OFF/C(裁剪前端)/B(裁剪后端)] [坐标]
                string xclipCommand = string.Format(
                    "_XCLIP _Select _R {0},{1} {2},{3} ",
                    Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y),
                    Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y)
                );
                
                // 执行命令
                doc.SendStringToExecute(xclipCommand, true, false, true);
                
                return true;
            }
            catch (Exception ex)
            {
                if (!_suppressLogging)
                    _logger.LogError("裁剪块失败: " + ex.Message, ex);
                return false;
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
            
            var result = _acadService.ExecuteInTransaction<ObjectId>(database, tr => {
                // 创建块定义
                BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForWrite) as BlockTable;
                if (bt == null)
                    throw new Exception("无法获取块表");
                    
                // 块名
                string blockName = "DDNTest";
                
                // 检查块是否已存在
                if (bt.Has(blockName))
                {
                    // 如果已存在，则获取其ID
                    return bt[blockName];
                }
                
                // 创建块表记录
                BlockTableRecord btr = new BlockTableRecord();
                btr.Name = blockName;
                
                // 添加几个实体到块中
                Line line1 = new Line(new Point3d(0, 0, 0), new Point3d(10, 10, 0));
                Line line2 = new Line(new Point3d(0, 10, 0), new Point3d(10, 0, 0));
                Circle circle = new Circle(new Point3d(5, 5, 0), Vector3d.ZAxis, 5);
                
                btr.AppendEntity(line1);
                btr.AppendEntity(line2);
                btr.AppendEntity(circle);
                
                // 将块表记录添加到块表中
                ObjectId btrId = bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
                
                // 添加新创建的实体
                tr.AddNewlyCreatedDBObject(line1, true);
                tr.AddNewlyCreatedDBObject(line2, true);
                tr.AddNewlyCreatedDBObject(circle, true);
                
                // 在模型空间中插入块参照
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (ms == null)
                    throw new Exception("无法获取模型空间");
                    
                BlockReference blockRef = new BlockReference(new Point3d(0, 0, 0), btrId);
                ObjectId blockRefId = ms.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                return blockRefId;
            }, "创建测试块");
            
            if (!result.Success)
            {
                return OperationResult<ObjectId>.ErrorResult(
                    result.ErrorMessage,
                    DateTime.Now - startTime
                );
            }
            
            return new OperationResult<ObjectId>(
                true,
                result.Data,
                "测试块创建成功",
                DateTime.Now - startTime
            );
        }
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        public OperationResult CreateTestBlock(Database database)
        {
            var result = CreateTestBlockWithId(database);
            
            return new OperationResult(
                result.Success,
                result.Message,
                result.ExecutionTime
            );
        }
        
        /// <summary>
        /// 自动对图块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockRefId">图块参照ID</param>
        /// <returns>操作结果</returns>
        public OperationResult AutoXClipBlock(Database database, ObjectId blockRefId)
        {
            if (database == null)
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
                
            if (blockRefId.IsNull)
                return OperationResult.ErrorResult("块参照ID无效", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            // 使用事务对块进行XClip
            var result = _acadService.ExecuteInTransaction<bool>(database, tr => {
                // 获取块参照
                BlockReference blockRef = tr.GetObject(blockRefId, OpenMode.ForWrite) as BlockReference;
                if (blockRef == null)
                    throw new Exception("无法获取块参照");
                    
                // 检查块是否已被XClip
                string detectionMethod;
                if (_acadService.IsBlockXClipped(tr, blockRef, out detectionMethod))
                {
                    return false; // 块已被XClip，不需要再次裁剪
                }
                
                // 获取块的边界框
                Extents3d extents = blockRef.GeometricExtents;
                
                // 创建稍微大一点的裁剪边界（边界框的110%）
                double margin = 0.1; // 10%的边距
                double width = extents.MaxPoint.X - extents.MinPoint.X;
                double height = extents.MaxPoint.Y - extents.MinPoint.Y;
                
                Point3d minPoint = new Point3d(
                    extents.MinPoint.X - width * margin,
                    extents.MinPoint.Y - height * margin,
                    0
                );
                
                Point3d maxPoint = new Point3d(
                    extents.MaxPoint.X + width * margin,
                    extents.MaxPoint.Y + height * margin,
                    0
                );
                
                // 应用裁剪
                return ClipBlockWithRectangle(blockRef, minPoint, maxPoint);
            }, "自动XClip块");
            
            if (!result.Success)
            {
                return OperationResult.ErrorResult(
                    result.ErrorMessage,
                    DateTime.Now - startTime
                );
            }
            
            string message = result.Data ? "块成功裁剪" : "块已被裁剪，跳过";
            
            return new OperationResult(
                true,
                message,
                DateTime.Now - startTime
            );
        }
    }
} 
