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
using DDNCadAddins.Models;

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
        private readonly IUserMessageService _msgService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public Blk2CsvCommand()
        {
            _logger = new FileLogger();
            _msgService = new AcadUserMessageService(_logger);
            _blockDataService = new BlockDataService();
            _csvExportService = new CsvExportService();
            _uiService = new AcadUserInterfaceService(_logger, _msgService);
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
                _logger.Log("开始执行Blk2Csv命令");
                
                // 执行导出操作
                ExecuteExport();
            }
            catch (SystemException ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                _uiService.ShowErrorMessage(errorMessage);
            }
            finally
            {
                _logger.Log("命令执行完成");
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
                _logger.Log("开始执行ExportBlocksWithAttributes命令");
                
                // 执行导出操作
                ExecuteExport();
            }
            catch (SystemException ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                _uiService.ShowErrorMessage(errorMessage);
            }
            finally
            {
                _logger.Log("命令执行完成");
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
                _logger.Log("当前没有打开的CAD文档");
                AcadApp.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            var db = doc.Database;
            
            // 获取用户选择的图块
            _logger.Log("请求用户选择图块");
            var selectedBlocks = _uiService.GetSelectedBlocks();
            if (selectedBlocks == null)
            {
                _logger.Log("用户取消了图块选择");
                return;
            }
            
            // 获取用户选择的CSV保存路径
            _logger.Log("请求用户选择CSV保存路径");
            string csvFilePath = _uiService.GetCsvSavePath();
            if (string.IsNullOrEmpty(csvFilePath))
            {
                _logger.Log("用户取消了文件保存");
                return;
            }
            
            _logger.Log($"用户选择了保存路径: {csvFilePath}");
            
            Dictionary<ObjectId, Dictionary<string, string>> blockData = null;
            HashSet<string> allAttribTags = null;
            
            // 提取图块数据
            _logger.Log("开始提取图块数据");
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var result = _blockDataService.ExtractBlockData(selectedBlocks, tr);
                
                if (result.Success)
                {
                    var resultData = result.Data;
                    blockData = resultData.BlockData;
                    allAttribTags = resultData.AllAttributeTags;
                    _logger.Log($"成功提取 {blockData.Count} 个图块数据");
                }
                else
                {
                    _logger.Log("提取图块数据失败: " + result.ErrorMessage);
                    _uiService.ShowErrorMessage("提取图块数据失败: " + result.ErrorMessage);
                    return;
                }
                
                tr.Commit();
            }
            
            // 导出到CSV
            try
            {
                _logger.Log("开始导出到CSV文件");
                var exportResult = _csvExportService.ExportToCsv(blockData, allAttribTags, csvFilePath);
                
                if (exportResult.Success)
                {
                    int blockCount = exportResult.Data;
                    _logger.Log($"成功导出 {blockCount} 个图块数据到: {csvFilePath}");
                    
                    // 显示结果
                    string resultMessage = $"\n已导出 {blockCount} 个图块数据到: {csvFilePath}";
                    _uiService.ShowResultMessage(resultMessage);
                    
                    // 询问是否打开CSV文件
                    if (_uiService.AskToOpenCsvFile())
                    {
                        _logger.Log("用户选择打开CSV文件");
                        _uiService.OpenFile(csvFilePath);
                    }
                }
                else
                {
                    _logger.LogError($"导出CSV失败: {exportResult.ErrorMessage}", null);
                    _uiService.ShowErrorMessage($"导出CSV失败: {exportResult.ErrorMessage}");
                }
            }
            catch (SystemException ex)
            {
                string errorMessage = $"写入CSV文件时出错: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                _uiService.ShowErrorMessage(errorMessage);
            }
        }
    }
} 