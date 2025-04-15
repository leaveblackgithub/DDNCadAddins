using System;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// XClip相关命令类 - 仅包含命令实现
    /// </summary>
    public class XClipCommand
    {
        private readonly ILogger _logger;
        private readonly IXClipBlockService _xclipService;
        private readonly IUserMessageService _msgService;
        private readonly IUserInterfaceService _uiService;
        private readonly IViewService _viewService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public XClipCommand()
        {
            _logger = new FileLogger();
            _msgService = new AcadUserMessageService(_logger);
            _uiService = new AcadUserInterfaceService(_logger, _msgService);
            // 创建IAcadService实现并传递给XClipBlockService
            IAcadService acadService = new AcadService(_logger);
            _xclipService = new XClipBlockService(acadService);
            _viewService = new AcadViewService(_msgService, _logger);
        }

        /// <summary>
        /// 查找所有被XClip的图块命令
        /// </summary>
        [CommandMethod("FindXClippedBlocks")]
        public void FindXClippedBlocks()
        {
            // 初始化日志
            _logger.Initialize("FindXClippedBlocks");
            
            try
            {
                // 输入：验证文档
                if (!_uiService.ValidateActiveDocument())
                    return;
                
                var doc = _uiService.GetActiveDocument();
                
                // 处理：调用服务查找XClip图块
                _msgService.ShowMessage("正在搜索被XClip的图块，请稍等...");
                
                var result = _xclipService.FindXClippedBlocks(doc.Database, doc.Editor);
                
                if (!result.Success)
                {
                    _msgService.ShowError($"查找失败: {result.ErrorMessage}");
                    return;
                }
                
                // 输出：显示结果和询问用户
                _viewService.ProcessAndDisplayXClipResults(result.Data);
                
                if (result.Data.Count > 0 && _uiService.AskToZoomToFirstBlock())
                {
                    _viewService.ZoomToFirstXClippedBlock(doc, result.Data);
                }
            }
            catch (SystemException ex)
            {
                _msgService.ShowError("查找XClip图块时发生错误", ex);
            }
            finally
            {
                _logger.Close();
            }
        }

        /// <summary>
        /// 创建测试图块命令
        /// </summary>
        [CommandMethod("CreateXClippedBlock")]
        public void CreateXClippedBlock()
        {
            // 初始化日志
            _logger.Initialize("CreateXClippedBlock");
            
            try
            {
                // 输入：验证文档
                if (!_uiService.ValidateActiveDocument())
                    return;
                
                var doc = _uiService.GetActiveDocument();
                
                // 处理：创建测试块
                _msgService.ShowMessage("正在创建测试块...");
                
                var result = _xclipService.CreateTestBlockWithId(doc.Database);
                
                // 输出：处理创建结果
                if (result.Success)
                {
                    _msgService.ShowSuccess("测试块创建成功");
                    
                    // 执行自动XClip操作
                    ExecuteAutoXClip(doc.Database, result.Data);
                }
                else
                {
                    _msgService.ShowError($"创建测试块失败: {result.ErrorMessage}");
                    _msgService.ShowMessage("可能原因:");
                    _msgService.ShowMessage("1. 文件或目录权限问题");
                    _msgService.ShowMessage("2. 块名冲突");
                    _msgService.ShowMessage("3. CAD内部错误");
                }
            }
            catch (SystemException ex)
            {
                _msgService.ShowError("执行CreateXClippedBlock命令时发生错误", ex);
            }
            finally
            {
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 执行自动XClip操作
        /// </summary>
        private void ExecuteAutoXClip(Database database, ObjectId blockRefId)
        {
            _msgService.ShowMessage("正在执行自动XClip操作...");
            
            var xclipResult = _xclipService.AutoXClipBlock(database, blockRefId);
            
            if (xclipResult.Success)
            {
                _msgService.ShowSuccess("自动XClip操作成功完成");
            }
            else
            {
                _msgService.ShowError($"自动XClip操作失败: {xclipResult.ErrorMessage}");
                ShowManualXClipInstructions();
            }
        }
        
        /// <summary>
        /// 显示手动XClip操作说明
        /// </summary>
        private void ShowManualXClipInstructions()
        {
            _msgService.ShowMessage("失败后可尝试手动执行XClip操作:");
            _msgService.ShowMessage("1. 输入XCLIP命令并按回车");
            _msgService.ShowMessage("2. 选择创建的测试块并按回车");
            _msgService.ShowMessage("3. 输入N并按回车(表示新建裁剪边界)");
            _msgService.ShowMessage("4. 输入R并按回车(表示使用矩形边界)");
            _msgService.ShowMessage("5. 绘制矩形裁剪边界");
        }
        
        /// <summary>
        /// 打开日志文件目录命令
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenLogFile()
        {
            try
            {
                string logDirectory = FileLogger.LogDirectory;
                
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                    _msgService.ShowMessage($"已打开日志文件所在目录: {logDirectory}");
                }
                else
                {
                    _msgService.ShowAlert($"日志目录不存在: {logDirectory}");
                }
            }
            catch (SystemException ex)
            {
                _msgService.ShowError("打开日志目录时出错", ex);
            }
        }
    }
}
