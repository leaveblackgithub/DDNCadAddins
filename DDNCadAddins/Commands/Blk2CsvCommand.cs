using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
// 添加Windows表单用于文件对话框
using System.Windows.Forms;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 提取图块信息到CSV文件命令类
    /// </summary>
    public class Blk2CsvCommand
    {
        private readonly ILogger _logger;
        private readonly IBlockDataService _blockDataService;
        private readonly ICsvExportService _csvExportService;
        private readonly IUserInterfaceService _uiService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public Blk2CsvCommand()
        {
            _logger = new FileLogger();
            _blockDataService = new BlockDataService();
            _csvExportService = new CsvExportService(_logger);
            _uiService = new AcadUserInterfaceService(_logger);
        }
        
        /// <summary>
        /// 图块信息提取到CSV命令
        /// </summary>
        [CommandMethod("Blk2Csv")]
        public void Execute()
        {
            try
            {
                // 初始化日志
                _logger.Initialize("Blk2Csv");
                
                // 执行导出操作
                ExecuteExport();
            }
            catch (SystemException ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                _uiService.ShowErrorMessage(errorMessage);
            }
            finally
            {
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 图块坐标和属性导出到CSV命令
        /// </summary>
        [CommandMethod("ExportBlocksWithAttributes")]
        public void ExportBlocksWithAttributes()
        {
            try
            {
                // 初始化日志
                _logger.Initialize("ExportBlocksWithAttributes");
                
                // 执行导出操作
                ExecuteExport();
            }
            catch (SystemException ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                _uiService.ShowErrorMessage(errorMessage);
            }
            finally
            {
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 执行导出操作的核心方法
        /// </summary>
        private void ExecuteExport()
        {
            // 获取当前文档和数据库
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                AcadApp.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            var db = doc.Database;
            
            // 获取用户选择的图块
            var selectedBlocks = _uiService.GetSelectedBlocks();
            if (selectedBlocks == null)
                return;
            
            // 获取用户选择的CSV保存路径
            string csvFilePath = _uiService.GetCsvSavePath();
            if (string.IsNullOrEmpty(csvFilePath))
                return;
            
            Dictionary<ObjectId, Dictionary<string, string>> blockData = null;
            HashSet<string> allAttribTags = null;
            
            // 提取图块数据
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var result = _blockDataService.ExtractBlockData(selectedBlocks, tr);
                blockData = result.BlockData;
                allAttribTags = result.AllAttributeTags;
                
                tr.Commit();
            }
            
            // 导出到CSV
            try
            {
                int blockCount = _csvExportService.ExportToCsv(blockData, allAttribTags, csvFilePath);
                
                // 显示结果
                string resultMessage = $"\n已导出 {blockCount} 个图块数据到: {csvFilePath}";
                _uiService.ShowResultMessage(resultMessage);
                
                // 询问是否打开CSV文件
                if (_uiService.AskToOpenCsvFile())
                {
                    _uiService.OpenFile(csvFilePath);
                }
            }
            catch (SystemException ex)
            {
                string errorMessage = $"写入CSV文件时出错: {ex.Message}";
                _uiService.ShowErrorMessage(errorMessage);
            }
        }
        
        /// <summary>
        /// 转义CSV字段，确保包含逗号、引号或换行符的字段正确格式化
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
                
            // 检查是否需要转义
            bool needsEscaping = field.Contains(",") || field.Contains("\"") || 
                                 field.Contains("\r") || field.Contains("\n");
                                 
            if (needsEscaping)
            {
                // 将字段中的双引号替换为两个双引号
                field = field.Replace("\"", "\"\"");
                // 在字段两端添加双引号
                field = "\"" + field + "\"";
            }
            
            return field;
        }
    }
} 