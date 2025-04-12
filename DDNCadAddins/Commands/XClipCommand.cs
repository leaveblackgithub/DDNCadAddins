using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// XClip相关命令类 - 仅包含命令实现
    /// </summary>
    public class XClipCommand
    {
        private readonly ILogger _logger;
        private readonly IXClipBlockService _xclipService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public XClipCommand()
        {
            _logger = new FileLogger();
            _xclipService = new XClipBlockService(_logger);
        }

        /// <summary>
        /// 查找所有被XClip的图块命令
        /// </summary>
        [CommandMethod("FindXClippedBlocks")]
        public void FindXClippedBlocks()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                Application.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            // 初始化日志
            _logger.Initialize("FindXClippedBlocks");
            
            try
            {
                _logger.Log("===== 开始查找被XClip的图块 =====");
                doc.Editor.WriteMessage("\n正在搜索被XClip的图块，请稍等...");
                
                // 调用服务方法执行查找
                OperationResult<List<XClippedBlockInfo>> result = 
                    _xclipService.FindXClippedBlocks(doc.Database);
                
                // 处理结果
                if (!result.Success)
                {
                    _logger.Log($"执行失败: {result.ErrorMessage}");
                    doc.Editor.WriteMessage($"\n查找失败: {result.ErrorMessage}");
                    return;
                }
                
                var xclippedBlocks = result.Data;
                
                // 如果找不到被xclip的图块，输出提示
                if (xclippedBlocks.Count == 0)
                {
                    _logger.Log("未找到被XClip的图块。");
                    _logger.Log("可能原因：");
                    _logger.Log("1. 图形中没有被XClip的图块");
                    _logger.Log("2. XClip信息无法被正确识别");
                    _logger.Log("建议：尝试使用CreateXClippedBlock命令创建测试块再进行检测");
                    
                    doc.Editor.WriteMessage("\n未找到被XClip的图块。");
                    doc.Editor.WriteMessage("\n请确认以下几点：");
                    doc.Editor.WriteMessage("\n1. 图形中是否存在块参照");
                    doc.Editor.WriteMessage("\n2. 是否已使用XCLIP命令对块进行了裁剪");
                    doc.Editor.WriteMessage("\n\n可尝试输入CreateXClippedBlock命令创建测试块，然后手动执行XCLIP进行裁剪测试");
                    return;
                }

                // 输出结果
                _logger.Log($"扫描完成，耗时: {result.ExecutionTime.TotalSeconds:F2}秒");
                _logger.Log($"共找到 {xclippedBlocks.Count} 个被XClip的图块：");
                
                doc.Editor.WriteMessage($"\n扫描完成，共找到 {xclippedBlocks.Count} 个被XClip的图块：");
                
                foreach (var blockInfo in xclippedBlocks)
                {
                    string nestInfo = blockInfo.NestLevel > 0 ? $"[嵌套级别:{blockInfo.NestLevel}] " : "";
                    
                    _logger.Log($"图块ID: {blockInfo.BlockReferenceId}");
                    _logger.Log($"图块定义名: {blockInfo.BlockName}");
                    _logger.Log($"检测方法: {blockInfo.DetectionMethod}");
                    _logger.Log($"嵌套级别: {blockInfo.NestLevel}");
                    _logger.Log("----------------------------");
                    
                    doc.Editor.WriteMessage($"\n- {nestInfo}图块名称: {blockInfo.BlockName}, 检测方法: {blockInfo.DetectionMethod}");
                }
                
                doc.Editor.WriteMessage("\n\n完整结果已写入日志文件，可使用OpenXClipLog命令查看");
            }
            catch (System.Exception ex)
            {
                // 只记录异常，不在CAD插件中抛出
                _logger.LogError($"未处理的异常: {ex.Message}", ex);
                doc.Editor.WriteMessage($"\n执行过程中出错: {ex.Message}");
                doc.Editor.WriteMessage("\n更多详细信息已记录到日志文件，使用OpenXClipLog命令查看");
            }
            finally
            {
                // 关闭日志文件
                _logger.Close();
            }
        }

        /// <summary>
        /// 创建测试图块命令
        /// </summary>
        [CommandMethod("CreateXClippedBlock")]
        public void CreateXClippedBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                Application.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            // 初始化日志
            _logger.Initialize("CreateXClippedBlock");

            try
            {
                _logger.Log("===== 开始创建测试图块 =====");
                doc.Editor.WriteMessage("\n正在创建测试图块，请稍等...");
                
                // 调用服务创建测试块
                OperationResult result = _xclipService.CreateTestBlock(doc.Database);
                
                if (result.Success)
                {
                    _logger.Log("已创建测试块，请手动执行XCLIP命令对其进行裁剪。");
                    _logger.Log("步骤: 输入XCLIP命令 -> 选择块 -> 输入N(新建) -> 输入R(矩形) -> 选择裁剪边界");
                    _logger.Log("完成后，请运行FindXClippedBlocks命令进行测试");
                    
                    doc.Editor.WriteMessage("\n测试块已创建成功! 位置在坐标(10,10)处");
                    doc.Editor.WriteMessage("\n请按以下步骤对块进行裁剪:");
                    doc.Editor.WriteMessage("\n1. 输入XCLIP命令并按回车");
                    doc.Editor.WriteMessage("\n2. 选择刚创建的测试块并按回车");
                    doc.Editor.WriteMessage("\n3. 输入N并按回车(表示新建裁剪边界)");
                    doc.Editor.WriteMessage("\n4. 输入R并按回车(表示使用矩形边界)");
                    doc.Editor.WriteMessage("\n5. 绘制矩形裁剪边界(不要覆盖整个块)");
                    doc.Editor.WriteMessage("\n6. 完成后输入FindXClippedBlocks命令检测效果");
                    
                    // 缩放到测试块位置
                    doc.Editor.WriteMessage("\n正在缩放到测试块位置...");
                    ZoomToPoint(doc, new Point3d(10, 10, 0), 10);
                }
                else
                {
                    _logger.Log($"创建测试块失败: {result.ErrorMessage}");
                    doc.Editor.WriteMessage($"\n创建测试块失败: {result.ErrorMessage}");
                    doc.Editor.WriteMessage("\n可能原因：");
                    doc.Editor.WriteMessage("\n1. 图形中已存在名为'TestBlock'的块");
                    doc.Editor.WriteMessage("\n2. 无法访问模型空间或块表");
                    doc.Editor.WriteMessage("\n详细错误信息已记录到日志文件");
                }
            }
            catch (System.Exception ex)
            {
                // 只记录异常，不在CAD插件中抛出
                _logger.LogError($"未处理的异常: {ex.Message}", ex);
                doc.Editor.WriteMessage($"\n执行过程中出错: {ex.Message}");
                doc.Editor.WriteMessage("\n更多详细信息已记录到日志文件，使用OpenXClipLog命令查看");
            }
            finally
            {
                // 关闭日志文件
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 打开最新XClip日志文件命令
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenXClipLog()
        {
            try
            {
                string logPath = FileLogger.GetLatestLogFile("XClipCommand");
                Document doc = Application.DocumentManager.MdiActiveDocument;
                
                if (!string.IsNullOrEmpty(logPath))
                {
                    System.Diagnostics.Process.Start("notepad.exe", logPath);
                    if (doc != null)
                    {
                        doc.Editor.WriteMessage($"\n已打开日志文件: {logPath}");
                    }
                    return;
                }
                
                if (doc != null)
                {
                    doc.Editor.WriteMessage("\n未找到日志文件。");
                    doc.Editor.WriteMessage("\n您需要先运行FindXClippedBlocks或CreateXClippedBlock命令以生成日志文件。");
                }
                else
                {
                    Application.ShowAlertDialog("未找到日志文件。\n\n您需要先运行FindXClippedBlocks或CreateXClippedBlock命令以生成日志文件。");
                }
            }
            catch (System.Exception ex)
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n打开日志文件时出错: {ex.Message}");
                }
                else
                {
                    Application.ShowAlertDialog($"打开日志文件时出错: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 缩放到指定点位置
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="center">中心点</param>
        /// <param name="size">显示范围大小</param>
        private void ZoomToPoint(Document doc, Point3d center, double size)
        {
            try
            {
                if (doc == null) return;
                
                Editor ed = doc.Editor;
                Matrix3d ucs2wcs = ed.CurrentUserCoordinateSystem;
                
                // 转换到世界坐标系
                center = center.TransformBy(ucs2wcs);
                
                // 计算视图范围
                Point3d min = new Point3d(center.X - size, center.Y - size, 0);
                Point3d max = new Point3d(center.X + size, center.Y + size, 0);
                
                // 设置视图范围
                using (ViewTableRecord view = doc.Editor.GetCurrentView())
                {
                    view.Width = 2 * size;
                    view.Height = 2 * size;
                    view.CenterPoint = new Point2d(center.X, center.Y);
                    ed.SetCurrentView(view);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"缩放视图时出错: {ex.Message}", false);
            }
        }
    }
}
