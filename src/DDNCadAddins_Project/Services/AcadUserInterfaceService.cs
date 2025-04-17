using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;

// 使用别名解决命名冲突
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     AutoCAD用户界面服务实现.
    /// </summary>
    public class AcadUserInterfaceService : IUserInterfaceService
    {
        private readonly ILogger logger;
        private readonly IUserMessageService msgService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AcadUserInterfaceService"/> class.
        ///     构造函数.
        /// </summary>
        /// <param name="logger">日志接口.</param>
        /// <param name="msgService">消息服务接口.</param>
        public AcadUserInterfaceService(ILogger logger, IUserMessageService msgService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.msgService = msgService ?? throw new ArgumentNullException(nameof(msgService));
        }

        /// <summary>
        ///     获取当前活动文档.
        /// </summary>
        /// <returns>当前活动文档，如果没有则返回null.</returns>
        public Document GetActiveDocument()
        {
            return Application.DocumentManager.MdiActiveDocument;
        }

        /// <summary>
        ///     验证当前活动文档是否存在.
        /// </summary>
        /// <returns>如果当前有打开的文档则返回true.</returns>
        public bool ValidateActiveDocument()
        {
            Document doc = this.GetActiveDocument();
            if (doc == null)
            {
                this.msgService.ShowAlert("当前没有打开的CAD文档");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     获取用户选择的图块对象ID集合.
        /// </summary>
        /// <returns>图块对象ID集合，如果用户取消则返回null.</returns>
        public IEnumerable<ObjectId> GetSelectedBlocks()
        {
            // 选择图块
            Editor ed = GetCurrentEditor();
            if (ed == null)
            {
                return null;
            }

            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要导出信息的图块: ",
                AllowDuplicates = false,
            };

            // 创建图块过滤器
            TypedValue[] filterList =
            {
                new TypedValue((int)DxfCode.Start, "INSERT"),
            };
            SelectionFilter filter = new SelectionFilter(filterList);

            PromptSelectionResult selResult = ed.GetSelection(selOpts, filter);
            if (selResult.Status != PromptStatus.OK)
            {
                return null;
            }

            SelectionSet ss = selResult.Value;
            if (ss == null || ss.Count == 0)
            {
                this.msgService.ShowWarning("未选择任何图块");
                return null;
            }

            List<ObjectId> objectIds = new List<ObjectId>();
            foreach (SelectedObject selectedObject in ss)
            {
                objectIds.Add(selectedObject.ObjectId);
            }

            return objectIds;
        }

        /// <summary>
        ///     获取用户选择的CSV文件保存路径.
        /// </summary>
        /// <returns>CSV文件路径，如果用户取消则返回null.</returns>
        public string GetCsvSavePath()
        {
            string initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultFileName = $"BlockData_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            // 使用Windows表单的SaveFileDialog
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Title = "保存图块数据到CSV文件";
                saveDialog.Filter = "CSV文件 (*.csv)|*.csv";
                saveDialog.InitialDirectory = initialDir;
                saveDialog.FileName = defaultFileName;
                saveDialog.DefaultExt = "csv";

                // 在AutoCAD应用程序中显示对话框
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    return saveDialog.FileName;
                }

                return null; // 用户取消了保存操作
            }
        }

        /// <summary>
        ///     输出结果消息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void ShowResultMessage(string message)
        {
            this.msgService.ShowMessage(message);
        }

        /// <summary>
        ///     询问用户是否打开生成的CSV文件.
        /// </summary>
        /// <returns>如果用户选择打开，则返回true.</returns>
        public bool AskToOpenCsvFile()
        {
            Editor ed = GetCurrentEditor();
            if (ed == null)
            {
                return false;
            }

            PromptKeywordOptions keyOpts = new PromptKeywordOptions("\n是否打开CSV文件? ");
            keyOpts.Keywords.Add("是");
            keyOpts.Keywords.Add("否");
            keyOpts.Keywords.Default = "是";
            keyOpts.AllowNone = true;

            PromptResult keyRes = ed.GetKeywords(keyOpts);
            return keyRes.Status == PromptStatus.OK &&
                   (keyRes.StringResult == "是" || string.IsNullOrEmpty(keyRes.StringResult));
        }

        /// <summary>
        ///     询问用户是否要缩放到第一个XClip图块.
        /// </summary>
        /// <returns>如果用户选择是，则返回true.</returns>
        public bool AskToZoomToFirstBlock()
        {
            Editor ed = GetCurrentEditor();
            if (ed == null)
            {
                return false;
            }

            PromptKeywordOptions keyOpts = new PromptKeywordOptions("\n是否要缩放到第一个XClip图块? ");
            keyOpts.Keywords.Add("是");
            keyOpts.Keywords.Add("否");
            keyOpts.Keywords.Default = "是";
            keyOpts.AllowNone = true;

            PromptResult keyRes = ed.GetKeywords(keyOpts);
            return keyRes.Status == PromptStatus.OK &&
                   (keyRes.StringResult == "是" || string.IsNullOrEmpty(keyRes.StringResult));
        }

        /// <summary>
        ///     打开指定路径的文件.
        /// </summary>
        /// <param name="filePath">文件路径.</param>
        public void OpenFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                _ = Process.Start(filePath);
                this.msgService.ShowMessage($"已打开文件: {filePath}");
            }
            else
            {
                this.msgService.ShowError($"文件不存在: {filePath}");
            }
        }

        /// <summary>
        ///     显示错误消息.
        /// </summary>
        /// <param name="message">错误消息.</param>
        public void ShowErrorMessage(string message)
        {
            this.msgService.ShowError(message);
        }

        /// <summary>
        ///     获取当前编辑器.
        /// </summary>
        private static Editor GetCurrentEditor()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            return doc?.Editor;
        }
    }
}
