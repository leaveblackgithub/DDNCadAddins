using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// AutoCAD视图服务实现
    /// </summary>
    public class AcadViewService : IViewService
    {
        private readonly IUserMessageService _msgService;
        private readonly ILogger _logger;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="msgService">用户消息服务</param>
        /// <param name="logger">日志服务</param>
        public AcadViewService(IUserMessageService msgService, ILogger logger)
        {
            _msgService = msgService ?? throw new ArgumentNullException(nameof(msgService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 缩放到指定点
        /// </summary>
        /// <param name="doc">CAD文档</param>
        /// <param name="point">目标点</param>
        /// <param name="viewSize">视图大小</param>
        public void ZoomToPoint(Document doc, Point3d point, double viewSize)
        {
            if (doc == null)
                return;
                
            Editor ed = doc.Editor;
            Database db = doc.Database;
            
            using (ViewTableRecord view = ed.GetCurrentView())
            {
                Extents3d extents = new Extents3d(
                    new Point3d(point.X - viewSize / 2, point.Y - viewSize / 2, 0),
                    new Point3d(point.X + viewSize / 2, point.Y + viewSize / 2, 0)
                );
                
                // 如果当前视图是 UCS 视图，需要转换坐标
                Matrix3d ucs = ed.CurrentUserCoordinateSystem;
                if (!ucs.IsEqualTo(Matrix3d.Identity))
                {
                    extents.TransformBy(ucs.Inverse());
                }

                view.ViewDirection = Vector3d.ZAxis; // 设置为俯视
                view.CenterPoint = new Point2d(point.X, point.Y); // 设置中心点
                view.Width = viewSize;
                view.Height = viewSize * (view.Height / view.Width); // 保持高宽比
                
                ed.SetCurrentView(view);
                ed.Regen();
                
                _logger.Log($"已缩放视图到点({point.X:F2}, {point.Y:F2}, {point.Z:F2}), 视图大小: {viewSize}", false);
            }
        }
        
        /// <summary>
        /// 缩放到第一个被XClip的图块
        /// </summary>
        /// <param name="doc">CAD文档</param>
        /// <param name="blocks">XClip图块列表</param>
        public void ZoomToFirstXClippedBlock(Document doc, List<XClippedBlockInfo> blocks)
        {
            if (blocks == null || blocks.Count == 0 || doc == null)
                return;
                
            // 优先选择顶层图块
            var topLevelBlocks = blocks.Where(b => b.NestLevel == 0).ToList();
            XClippedBlockInfo targetBlock = topLevelBlocks.Count > 0 ? topLevelBlocks[0] : blocks[0];
            
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                BlockReference blockRef = tr.GetObject(targetBlock.BlockReferenceId, OpenMode.ForRead) as BlockReference;
                if (blockRef != null)
                {
                    ZoomToPoint(doc, blockRef.Position, 100); // 使用适当的视图大小
                    _msgService.ShowMessage($"已缩放到图块 '{targetBlock.BlockName}'");
                }
                else
                {
                    _msgService.ShowWarning("无法缩放到选定图块：图块不存在或已被删除");
                }
                tr.Commit();
            }
        }
        
        /// <summary>
        /// 处理并显示XClip图块查找结果
        /// </summary>
        /// <param name="xclippedBlocks">XClip图块列表</param>
        public void ProcessAndDisplayXClipResults(List<XClippedBlockInfo> xclippedBlocks)
        {
            if (xclippedBlocks == null || xclippedBlocks.Count == 0)
            {
                _msgService.ShowWarning("未找到任何被XClip的图块");
                return;
            }
            
            // 按照嵌套级别分组显示结果
            var groupedBlocks = xclippedBlocks.GroupBy(b => b.NestLevel)
                                           .OrderBy(g => g.Key)
                                           .ToList();
            
            _msgService.ShowSuccess($"找到 {xclippedBlocks.Count} 个被XClip的图块");
            
            foreach (var group in groupedBlocks)
            {
                string nestLevel = group.Key == 0 ? "顶层图块" : $"嵌套级别 {group.Key}";
                _msgService.ShowMessage($"- {nestLevel}: {group.Count()} 个图块");
                
                // 在日志中详细记录图块信息，但不显示在命令行
                foreach (var block in group)
                {
                    _logger.Log($"  图块名称: {block.BlockName}, 检测方法: {block.DetectionMethod}", false);
                }
            }
        }
    }
} 