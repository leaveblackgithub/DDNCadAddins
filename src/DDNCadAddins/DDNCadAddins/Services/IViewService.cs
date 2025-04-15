using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Models;
using System.Collections.Generic;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// 视图服务接口 - 处理视图操作如缩放等
    /// </summary>
    public interface IViewService
    {
        /// <summary>
        /// 缩放到指定点
        /// </summary>
        /// <param name="doc">CAD文档</param>
        /// <param name="point">目标点</param>
        /// <param name="viewSize">视图大小</param>
        void ZoomToPoint(Document doc, Point3d point, double viewSize);
        
        /// <summary>
        /// 缩放到第一个被XClip的图块
        /// </summary>
        /// <param name="doc">CAD文档</param>
        /// <param name="blocks">XClip图块列表</param>
        void ZoomToFirstXClippedBlock(Document doc, List<XClippedBlockInfo> blocks);
        
        /// <summary>
        /// 处理并显示XClip图块查找结果
        /// </summary>
        /// <param name="xclippedBlocks">XClip图块列表</param>
        void ProcessAndDisplayXClipResults(List<XClippedBlockInfo> xclippedBlocks);
    }
} 