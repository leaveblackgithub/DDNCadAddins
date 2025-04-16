using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;
using DDNCadAddins.Commands;
using DDNCadAddins.Models;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// XClip相关命令类 - 实现XClip相关的CAD命令
    /// </summary>
    public class XClipCommand : CommandBase
    {
        private readonly IXClipBlockService _xclipService;
        private readonly IViewService _viewService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public XClipCommand()
        {
            // 创建XClipBlockService实例，传入依赖项
            _xclipService = new XClipBlockService(AcadService, Logger);
            _viewService = new AcadViewService(MessageService, Logger);
        }

        /// <summary>
        /// 查找所有被XClip的图块命令
        /// </summary>
        [CommandMethod("FindXClippedBlocks")]
        public void FindXClippedBlocks()
        {
            CommandName = "FindXClippedBlocks";
            Execute();
        }
        
        /// <summary>
        /// 创建测试图块命令
        /// </summary>
        [CommandMethod("CreateXClippedBlock")]
        public void CreateXClippedBlock()
        {
            CommandName = "CreateXClippedBlock";
            Execute();
        }
        
        /// <summary>
        /// 打开日志文件目录命令
        /// </summary>
        [CommandMethod("OpenXClipLog")]
        public void OpenLogFile()
        {
            CommandName = "OpenXClipLog";
            Execute();
        }
        
        /// <summary>
        /// 将找到的XCLIPPEDBLOCK移到顶层并隔离显示命令
        /// </summary>
        [CommandMethod("IsolateXClippedBlocks")]
        public void IsolateXClippedBlocks()
        {
            CommandName = "IsolateXClippedBlocks";
            Execute();
        }
        
        /// <summary>
        /// 命令具体实现 - 根据CommandName执行对应的命令逻辑
        /// </summary>
        protected override void ExecuteCommand()
        {
            switch (CommandName)
            {
                case "FindXClippedBlocks":
                    ExecuteFindXClippedBlocks();
                    break;
                case "CreateXClippedBlock":
                    ExecuteCreateXClippedBlock();
                    break;
                case "OpenXClipLog":
                    ExecuteOpenLogFile();
                    break;
                case "IsolateXClippedBlocks":
                    ExecuteIsolateXClippedBlocks();
                    break;
                default:
                    MessageService.ShowError($"未知命令: {CommandName}");
                    break;
            }
        }
        
        /// <summary>
        /// 执行查找所有被XClip的图块命令
        /// </summary>
        private void ExecuteFindXClippedBlocks()
        {
            var doc = GetDocument();
            
            // 处理：调用服务查找XClip图块
            MessageService.ShowMessage("正在搜索被XClip的图块，请稍等...");
            
            var result = _xclipService.FindXClippedBlocks(doc.Database, doc.Editor);
            
            if (!result.Success)
            {
                MessageService.ShowError($"查找失败: {result.ErrorMessage}");
                return;
            }
            
            // 输出：显示结果和询问用户
            _viewService.ProcessAndDisplayXClipResults(result.Data);
            
            if (result.Data.Count > 0 && UiService.AskToZoomToFirstBlock())
            {
                _viewService.ZoomToFirstXClippedBlock(doc, result.Data);
            }
        }
        
        /// <summary>
        /// 执行创建测试图块命令
        /// </summary>
        private void ExecuteCreateXClippedBlock()
        {
            var doc = GetDocument();
            
            // 处理：创建测试块
            MessageService.ShowMessage("正在创建测试块...");
            
            var result = _xclipService.CreateTestBlockWithId(doc.Database);
            
            // 输出：处理创建结果
            if (result.Success)
            {
                MessageService.ShowSuccess("测试块创建成功");
                
                // 执行自动XClip操作
                ExecuteAutoXClip(doc.Database, result.Data);
            }
            else
            {
                MessageService.ShowError($"创建测试块失败: {result.ErrorMessage}");
                MessageService.ShowMessage("可能原因:");
                MessageService.ShowMessage("1. 文件或目录权限问题");
                MessageService.ShowMessage("2. 块名冲突");
                MessageService.ShowMessage("3. CAD内部错误");
            }
        }
        
        /// <summary>
        /// 执行打开日志文件目录命令
        /// </summary>
        private void ExecuteOpenLogFile()
        {
            string logDirectory = FileLogger.LogDirectory;
            
            if (Directory.Exists(logDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                MessageService.ShowMessage($"已打开日志文件所在目录: {logDirectory}");
            }
            else
            {
                MessageService.ShowAlert($"日志目录不存在: {logDirectory}");
            }
        }
        
        /// <summary>
        /// 执行隔离XClip图块命令
        /// </summary>
        private void ExecuteIsolateXClippedBlocks()
        {
            var doc = GetDocument();
            
            // 处理：调用服务查找XClip图块
            MessageService.ShowMessage("正在搜索被XClip的图块，请稍等...");
            
            var result = _xclipService.FindXClippedBlocks(doc.Database, doc.Editor);
            
            if (!result.Success)
            {
                MessageService.ShowError($"查找失败: {result.ErrorMessage}");
                return;
            }
            
            if (result.Data.Count == 0)
            {
                MessageService.ShowWarning("未找到任何被XClip的图块");
                return;
            }
            
            // 处理：将找到的XClip图块移到顶层并隔离
            MessageService.ShowMessage($"找到 {result.Data.Count} 个被XClip的图块，正在处理...");
            
            // 明确指定类型，避免编译器混淆
            List<XClippedBlockInfo> xclippedBlocks = result.Data;
            var isolateResult = _xclipService.IsolateXClippedBlocks(doc.Database, xclippedBlocks);
            
            if (isolateResult.Success)
            {
                MessageService.ShowSuccess(isolateResult.Message);
            }
            else
            {
                MessageService.ShowError($"隔离XClip图块失败: {isolateResult.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// 执行自动XClip操作
        /// </summary>
        private void ExecuteAutoXClip(Database database, ObjectId blockRefId)
        {
            MessageService.ShowMessage("正在执行自动XClip操作...");
            
            var xclipResult = _xclipService.AutoXClipBlock(database, blockRefId);
            
            if (xclipResult.Success)
            {
                MessageService.ShowSuccess("自动XClip操作成功完成");
            }
            else
            {
                MessageService.ShowError($"自动XClip操作失败: {xclipResult.ErrorMessage}");
                ShowManualXClipInstructions();
            }
        }
        
        /// <summary>
        /// 显示手动XClip操作说明
        /// </summary>
        private void ShowManualXClipInstructions()
        {
            MessageService.ShowMessage("失败后可尝试手动执行XClip操作:");
            MessageService.ShowMessage("1. 输入XCLIP命令并按回车");
            MessageService.ShowMessage("2. 选择创建的测试块并按回车");
            MessageService.ShowMessage("3. 输入N并按回车(表示新建裁剪边界)");
            MessageService.ShowMessage("4. 输入R并按回车(表示使用矩形边界)");
            MessageService.ShowMessage("5. 绘制矩形裁剪边界");
        }
    }
}
