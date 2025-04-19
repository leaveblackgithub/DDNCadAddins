using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    ///     创建XClip块命令 - 专用于创建并XClip测试块.
    /// </summary>
    public class CreateXClippedBlockCommand : CommandBase
    {
        private readonly IXClipBlockService xclipService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateXClippedBlockCommand"/> class.
        ///     构造函数 - 使用依赖注入.
        /// </summary>
        /// <param name="logger">日志记录器.</param>
        /// <param name="messageService">消息服务.</param>
        /// <param name="acadService">AutoCAD服务.</param>
        /// <param name="uiService">用户界面服务.</param>
        /// <param name="xclipService">XClip服务.</param>
        public CreateXClippedBlockCommand(
            ILogger logger,
            IUserMessageService messageService,
            IAcadService acadService,
            IUserInterfaceService uiService,
            IXClipBlockService xclipService)
            : base(logger, messageService, acadService, uiService)
        {
            this.xclipService = xclipService ?? throw new ArgumentNullException(nameof(xclipService));
            this.CommandName = "CreateXClippedBlock";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateXClippedBlockCommand"/> class.
        ///     无参构造函数 - 创建默认依赖项（用于向后兼容）.
        /// </summary>
        public CreateXClippedBlockCommand()
        {
            this.xclipService = new XClipBlockService(this.AcadService, this.Logger);
            this.CommandName = "CreateXClippedBlock";
        }

        /// <summary>
        ///     命令入口点.
        /// </summary>
        [CommandMethod("CreateXClippedBlock")]
        public void CreateXClippedBlock()
        {
            this.Execute();
        }

        /// <summary>
        ///     执行命令的具体实现.
        /// </summary>
        protected override void ExecuteCommand()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc = this.GetDocument();

            // 处理：创建测试块
            this.MessageService.ShowMessage("正在创建测试块...");

            Models.OperationResult<ObjectId> result = this.xclipService.CreateTestBlockWithId(doc.Database);

            // 输出：处理创建结果
            if (result.Success)
            {
                this.MessageService.ShowSuccess("测试块创建成功");

                // 执行自动XClip操作
                this.ExecuteAutoXClip(doc.Database, result.Data);
            }
            else
            {
                this.MessageService.ShowError($"创建测试块失败: {result.ErrorMessage}");
                this.MessageService.ShowMessage("可能原因:");
                this.MessageService.ShowMessage("1. 文件或目录权限问题");
                this.MessageService.ShowMessage("2. 块名冲突");
                this.MessageService.ShowMessage("3. CAD内部错误");
            }
        }

        /// <summary>
        ///     执行自动XClip操作.
        /// </summary>
        private void ExecuteAutoXClip(Database database, ObjectId blockRefId)
        {
            this.MessageService.ShowMessage("正在执行自动XClip操作...");

            Models.OperationResult xclipResult = this.xclipService.AutoXClipBlock(database, blockRefId);

            if (xclipResult.Success)
            {
                this.MessageService.ShowSuccess("自动XClip操作成功完成");
            }
            else
            {
                this.MessageService.ShowError($"自动XClip操作失败: {xclipResult.ErrorMessage}");
                this.ShowManualXClipInstructions();
            }
        }

        /// <summary>
        ///     显示手动XClip操作说明.
        /// </summary>
        private void ShowManualXClipInstructions()
        {
            this.MessageService.ShowMessage("失败后可尝试手动执行XClip操作:");
            this.MessageService.ShowMessage("1. 输入XCLIP命令并按回车");
            this.MessageService.ShowMessage("2. 选择创建的测试块并按回车");
            this.MessageService.ShowMessage("3. 输入N并按回车(表示新建裁剪边界)");
            this.MessageService.ShowMessage("4. 输入R并按回车(表示使用矩形边界)");
            this.MessageService.ShowMessage("5. 绘制矩形裁剪边界");
        }
    }
}
