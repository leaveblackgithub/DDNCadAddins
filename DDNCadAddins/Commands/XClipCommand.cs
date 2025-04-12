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

                // 准备输出信息
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"\n\n===== 查找结果: 共找到 {xclippedBlocks.Count} 个被XClip的图块 =====");
                sb.AppendLine($"搜索耗时: {result.ExecutionTime.TotalSeconds:F2} 秒");
                sb.AppendLine("列表如下:");
                
                // 分组统计
                var groupedBlocks = xclippedBlocks.GroupBy(b => b.BlockName)
                                                .OrderBy(g => g.Key);
                                                
                foreach (var group in groupedBlocks)
                {
                    sb.AppendLine($"\n图块名称: {group.Key} (共 {group.Count()} 个)");
                    int count = 0;
                    foreach (var block in group)
                    {
                        count++;
                        string nestInfo = block.NestLevel > 0 ? $"[嵌套级别:{block.NestLevel}] " : "";
                        sb.AppendLine($"  {count}. {nestInfo}ID: {block.BlockReferenceId}, 检测方法: {block.DetectionMethod}");
                    }
                }
                
                sb.AppendLine("\n===== 查找结束 =====");
                
                // 输出结果
                doc.Editor.WriteMessage(sb.ToString());
                _logger.Log("===== FindXClippedBlocks命令执行结束 =====");
            }
            catch (System.Exception ex)
            {
                _logger.LogError("执行FindXClippedBlocks命令时发生异常", ex);
                doc.Editor.WriteMessage($"\n执行命令时发生错误: {ex.Message}");
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

            // 收集所有提示信息
            StringBuilder messageBuilder = new StringBuilder();
            ObjectId blockRefId = ObjectId.Null;

            try
            {
                _logger.Log("===== 开始创建测试图块 =====");
                
                // 调用服务创建测试块
                OperationResult result = _xclipService.CreateTestBlock(doc.Database);
                
                if (result.Success)
                {
                    messageBuilder.AppendLine("\n测试块已创建成功! 位置在坐标(10,10)处");
                    
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        // 获取刚创建的测试块的ID
                        BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                        
                        // 遍历模型空间中的对象查找测试块
                        foreach (ObjectId id in ms)
                        {
                            BlockReference br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                            if (br != null)
                            {
                                BlockTableRecord btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                if (btr.Name.StartsWith("TestBlock"))
                                {
                                    blockRefId = id;
                                    _logger.Log($"找到测试块ID: {id}, 名称: {btr.Name}");
                                    break;
                                }
                            }
                        }
                        tr.Commit();
                    }
                    
                    if (blockRefId != ObjectId.Null)
                    {
                        _logger.Log("测试块已创建成功，现在自动执行XClip操作...");
                        
                        // 自动执行XClip
                        OperationResult xclipResult = _xclipService.AutoXClipBlock(doc.Database, blockRefId);
                        
                        if (xclipResult.Success)
                        {
                            _logger.Log("自动XClip操作成功完成！");
                            messageBuilder.AppendLine("\n自动XClip操作成功完成！");
                            messageBuilder.AppendLine($"操作耗时: {xclipResult.ExecutionTime.TotalSeconds:F2}秒");
                            
                            // 缩放到测试块位置
                            _logger.Log("正在缩放到测试块位置...");
                            ZoomToPoint(doc, new Point3d(10, 10, 0), 10);
                            
                            // 添加验证信息
                            messageBuilder.AppendLine("\n可使用FindXClippedBlocks命令验证XClip效果");
                        }
                        else
                        {
                            _logger.Log($"自动XClip操作失败: {xclipResult.ErrorMessage}");
                            messageBuilder.AppendLine($"\n尝试自动执行XClip操作失败: {xclipResult.ErrorMessage}");
                            messageBuilder.AppendLine("\n失败后可尝试手动执行XClip操作:");
                            messageBuilder.AppendLine("1. 输入XCLIP命令并按回车");
                            messageBuilder.AppendLine("2. 选择创建的测试块并按回车");
                            messageBuilder.AppendLine("3. 输入N并按回车(表示新建裁剪边界)");
                            messageBuilder.AppendLine("4. 输入R并按回车(表示使用矩形边界)");
                            messageBuilder.AppendLine("5. 绘制矩形裁剪边界");
                        }
                    }
                    else
                    {
                        _logger.Log("无法找到刚创建的测试块，无法执行自动XClip");
                        messageBuilder.AppendLine("\n无法找到刚创建的测试块，无法执行自动XClip");
                    }
                }
                else
                {
                    _logger.Log($"创建测试块失败: {result.ErrorMessage}");
                    messageBuilder.AppendLine($"\n创建测试块失败: {result.ErrorMessage}");
                    messageBuilder.AppendLine("\n可能原因：");
                    messageBuilder.AppendLine("1. 文件或目录权限问题");
                    messageBuilder.AppendLine("2. 块名冲突");
                    messageBuilder.AppendLine("3. CAD内部错误");
                }
                
                // 在所有操作完成后，统一输出消息
                doc.Editor.WriteMessage(messageBuilder.ToString());
                _logger.Log("===== CreateXClippedBlock命令执行结束 =====");
            }
            catch (System.Exception ex)
            {
                _logger.LogError("执行CreateXClippedBlock命令时发生异常", ex);
                doc.Editor.WriteMessage($"\n执行命令时发生错误: {ex.Message}");
            }
            finally
            {
                // 关闭日志文件
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 打开日志文件目录命令
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenLogFile()
        {
            try
            {
                string logFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DDNCadAddins", 
                    "DDNCadAddins_XClip.log"
                );
                
                string logDirectory = Path.GetDirectoryName(logFilePath);
                
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n已打开日志文件所在目录: {logDirectory}");
                }
                else
                {
                    Application.ShowAlertDialog($"日志目录不存在: {logDirectory}");
                }
            }
            catch (System.Exception ex)
            {
                Application.ShowAlertDialog($"打开日志目录时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 缩放到指定点
        /// </summary>
        private void ZoomToPoint(Document doc, Point3d point, double viewSize)
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
                view.CenterPoint = new Point2d(point.X, point.Y); // 设置中心点 (修正属性名)
                view.Width = viewSize;
                view.Height = viewSize * (view.Height / view.Width); // 保持高宽比
                
                ed.SetCurrentView(view);
                ed.Regen();
            }
        }
    }
}
