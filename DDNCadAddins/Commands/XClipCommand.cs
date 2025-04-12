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
        [CommandMethod("DDN_XCLIP_CreateXClippedBlock")]
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
<<<<<<< HEAD
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
=======
                    // 获取创建的块参照ID
                    // 因为CreateTestBlock方法不返回创建的块参照ID，所以我们需要找到最近创建的块
                    ObjectId blockRefId = FindLastCreatedBlock(doc.Database, "TestBlock");
                    
                    if (blockRefId != ObjectId.Null)
                    {
                        _logger.Log($"找到创建的测试块ID: {blockRefId}，正在执行自动XClip...");
                        doc.Editor.WriteMessage("\n测试块已创建成功，正在自动执行XClip...");
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                        
                        // 自动执行XClip
                        OperationResult xclipResult = _xclipService.AutoXClipBlock(doc.Database, blockRefId);
                        
                        if (xclipResult.Success)
                        {
<<<<<<< HEAD
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
=======
                            _logger.Log("自动XClip执行成功!");
                            doc.Editor.WriteMessage("\nXClip自动裁剪成功! 块位置在坐标(10,10)处");
                            doc.Editor.WriteMessage("\n您可以运行FindXClippedBlocks命令检测效果");
                            
                            // 缩放到测试块位置
                            _logger.Log("正在缩放到测试块位置...");
                            doc.Editor.WriteMessage("\n正在缩放到测试块位置...");
                            ZoomToPoint(doc, new Point3d(10, 10, 0), 20);
                        }
                        else
                        {
                            _logger.Log($"自动XClip执行失败: {xclipResult.ErrorMessage}");
                            doc.Editor.WriteMessage($"\n自动XClip执行失败: {xclipResult.ErrorMessage}");
                            doc.Editor.WriteMessage("\n请手动执行XClip命令进行裁剪");
                            
                            // 提供手动裁剪的指示
                            doc.Editor.WriteMessage("\n手动裁剪步骤:");
                            doc.Editor.WriteMessage("\n1. 输入XCLIP命令并按回车");
                            doc.Editor.WriteMessage("\n2. 选择刚创建的测试块并按回车");
                            doc.Editor.WriteMessage("\n3. 输入N并按回车(表示新建裁剪边界)");
                            doc.Editor.WriteMessage("\n4. 输入R并按回车(表示使用矩形边界)");
                            doc.Editor.WriteMessage("\n5. 绘制矩形裁剪边界(不要覆盖整个块)");
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                        }
                    }
                    else
                    {
<<<<<<< HEAD
                        _logger.Log("无法找到刚创建的测试块，无法执行自动XClip");
                        messageBuilder.AppendLine("\n无法找到刚创建的测试块，无法执行自动XClip");
=======
                        _logger.Log("找不到刚创建的测试块，无法执行自动XClip");
                        doc.Editor.WriteMessage("\n测试块已创建成功，但无法找到块ID执行自动XClip");
                        doc.Editor.WriteMessage("\n请手动执行XClip命令对其进行裁剪:");
                        doc.Editor.WriteMessage("\n1. 输入XCLIP命令并按回车");
                        doc.Editor.WriteMessage("\n2. 选择刚创建的测试块并按回车");
                        doc.Editor.WriteMessage("\n3. 输入N并按回车(表示新建裁剪边界)");
                        doc.Editor.WriteMessage("\n4. 输入R并按回车(表示使用矩形边界)");
                        doc.Editor.WriteMessage("\n5. 绘制矩形裁剪边界(不要覆盖整个块)");
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                    }
                }
                else
                {
                    _logger.Log($"创建测试块失败: {result.ErrorMessage}");
<<<<<<< HEAD
                    messageBuilder.AppendLine($"\n创建测试块失败: {result.ErrorMessage}");
                    messageBuilder.AppendLine("\n可能原因：");
                    messageBuilder.AppendLine("1. 文件或目录权限问题");
                    messageBuilder.AppendLine("2. 块名冲突");
                    messageBuilder.AppendLine("3. CAD内部错误");
=======
                    doc.Editor.WriteMessage($"\n创建测试块失败: {result.ErrorMessage}");
                    doc.Editor.WriteMessage("\n可能原因：");
                    doc.Editor.WriteMessage("\n1. 图形中已存在同名的测试块");
                    doc.Editor.WriteMessage("\n2. 无法访问模型空间或块表");
                    doc.Editor.WriteMessage("\n详细错误信息已记录到日志文件");
>>>>>>> ca08728bf88372dd2cc5851c1f0e469fb4dfc75e
                }
                
                // 在所有操作完成后，统一输出消息
                doc.Editor.WriteMessage(messageBuilder.ToString());
                _logger.Log("===== CreateXClippedBlock命令执行结束 =====");
            }
            catch (System.Exception ex)
            {
                // 只记录异常，不在CAD插件中抛出
                _logger.LogError($"未处理的异常: {ex.Message}", ex);
                messageBuilder.AppendLine($"\n执行过程中出错: {ex.Message}");
                messageBuilder.AppendLine("\n更多详细信息已记录到日志文件，使用OpenXClipLog命令查看");
            }
            finally
            {
                // 关闭日志文件
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 查找最近创建的指定名称的块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockNamePrefix">块名称前缀</param>
        /// <returns>块参照ID</returns>
        private ObjectId FindLastCreatedBlock(Database database, string blockNamePrefix)
        {
            ObjectId result = ObjectId.Null;
            
            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    // 获取模型空间
                    BlockTable bt = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    
                    // 找到具有特定名称前缀的最新块
                    DateTime latestTime = DateTime.MinValue;
                    string latestName = string.Empty;
                    
                    foreach (ObjectId id in modelSpace)
                    {
                        BlockReference blockRef = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (blockRef != null)
                        {
                            // 获取块定义名称
                            BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                            if (blockDef != null && blockDef.Name.StartsWith(blockNamePrefix))
                            {
                                // 检查创建时间
                                if (blockRef.Database.Tdcreate > latestTime)
                                {
                                    latestTime = blockRef.Database.Tdcreate;
                                    result = id;
                                    latestName = blockDef.Name;
                                }
                            }
                        }
                    }
                    
                    if (result != ObjectId.Null)
                    {
                        _logger.Log($"找到最近创建的块: {latestName}, ID: {result}");
                    }
                    
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log($"查找最近创建的块时出错: {ex.Message}");
                result = ObjectId.Null;
            }
            
            return result;
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
        /// 缩放视图到指定点位置
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="center">中心点</param>
        /// <param name="height">视图高度</param>
        private void ZoomToPoint(Document doc, Point3d center, double height)
        {
            try
            {
                // 获取当前视图
                Database db = doc.Database;
                using (ViewTableRecord view = doc.Editor.GetCurrentView())
                {
                    // 计算新视图范围
                    double width = height * view.Width / view.Height;
                    
                    // 设置新视图
                    view.CenterPoint = new Point2d(center.X, center.Y);
                    view.Height = height;
                    view.Width = width;
                    
                    // 更新视图
                    doc.Editor.SetCurrentView(view);
                }
                _logger.Log($"已缩放到点 ({center.X}, {center.Y}, {center.Z})");
            }
            catch (System.Exception ex)
            {
                _logger.Log($"缩放视图失败: {ex.Message}");
            }
        }
    }
}
