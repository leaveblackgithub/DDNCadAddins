using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 创建XClip块命令 - 专用于创建并XClip测试块
    /// </summary>
    public class CreateXClippedBlockCommand : CommandBase
    {
        private readonly IXClipBlockService _xclipService;
        
        /// <summary>
        /// 构造函数 - 使用依赖注入
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="messageService">消息服务</param>
        /// <param name="acadService">AutoCAD服务</param>
        /// <param name="uiService">用户界面服务</param>
        /// <param name="xclipService">XClip服务</param>
        public CreateXClippedBlockCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService,
            IXClipBlockService xclipService)
            : base(logger, messageService, acadService, uiService)
        {
            _xclipService = xclipService ?? throw new ArgumentNullException(nameof(xclipService));
            CommandName = "CreateXClippedBlock";
        }
        
        /// <summary>
        /// 无参构造函数 - 创建默认依赖项（用于向后兼容）
        /// </summary>
        public CreateXClippedBlockCommand() : base()
        {
            _xclipService = new XClipBlockService(AcadService, Logger);
            CommandName = "CreateXClippedBlock";
        }
        
        /// <summary>
        /// 命令入口点
        /// </summary>
        [CommandMethod("CreateXClippedBlock")]
        public void CreateXClippedBlock()
        {
            Execute();
        }
        
        /// <summary>
        /// 执行命令的具体实现
        /// </summary>
        protected override void ExecuteCommand()
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