using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using DDNCadAddins.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// AutoCAD用户界面服务实现
    /// </summary>
    public class AcadUserInterfaceService : IUserInterfaceService
    {
        private readonly ILogger _logger;
        private readonly Document _document;
        private readonly Editor _editor;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志接口</param>
        public AcadUserInterfaceService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _document = AcadApp.DocumentManager.MdiActiveDocument;
            if (_document == null)
                throw new InvalidOperationException("当前没有打开的CAD文档");
                
            _editor = _document.Editor;
        }
        
        /// <summary>
        /// 获取用户选择的图块对象ID集合
        /// </summary>
        /// <returns>图块对象ID集合，如果用户取消则返回null</returns>
        public IEnumerable<ObjectId> GetSelectedBlocks()
        {
            // 选择图块
            PromptSelectionOptions selOpts = new PromptSelectionOptions();
            selOpts.MessageForAdding = "\n请选择要导出信息的图块: ";
            selOpts.AllowDuplicates = false;
            
            // 创建图块过滤器
            TypedValue[] filterList = new TypedValue[] {
                new TypedValue((int)DxfCode.Start, "INSERT")
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            
            PromptSelectionResult selResult = _editor.GetSelection(selOpts, filter);
            if (selResult.Status != PromptStatus.OK)
                return null;
            
            SelectionSet ss = selResult.Value;
            if (ss == null || ss.Count == 0)
            {
                _editor.WriteMessage("\n未选择任何图块。");
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
        /// 获取用户选择的CSV文件保存路径
        /// </summary>
        /// <returns>CSV文件路径，如果用户取消则返回null</returns>
        public string GetCsvSavePath()
        {
            string initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultFileName = $"BlockData_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.csv";
            
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
        /// 输出结果消息
        /// </summary>
        /// <param name="message">消息内容</param>
        public void ShowResultMessage(string message)
        {
            _editor.WriteMessage(message);
            _logger.Log(message);
        }
        
        /// <summary>
        /// 询问用户是否打开生成的CSV文件
        /// </summary>
        /// <returns>如果用户选择打开，则返回true</returns>
        public bool AskToOpenCsvFile()
        {
            PromptKeywordOptions keyOpts = new PromptKeywordOptions("\n是否打开CSV文件? ");
            keyOpts.Keywords.Add("是");
            keyOpts.Keywords.Add("否");
            keyOpts.Keywords.Default = "是";
            keyOpts.AllowNone = true;
            
            PromptResult keyRes = _editor.GetKeywords(keyOpts);
            return keyRes.Status == PromptStatus.OK && 
                  (keyRes.StringResult == "是" || string.IsNullOrEmpty(keyRes.StringResult));
        }
        
        /// <summary>
        /// 打开指定路径的文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void OpenFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                Process.Start(filePath);
            }
            else
            {
                ShowErrorMessage($"文件不存在: {filePath}");
            }
        }
        
        /// <summary>
        /// 显示错误消息
        /// </summary>
        /// <param name="message">错误消息</param>
        public void ShowErrorMessage(string message)
        {
            _editor.WriteMessage("\n" + message);
            _logger.LogError(message, null);
        }
    }
} 