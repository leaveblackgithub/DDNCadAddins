// 添加Windows表单用于文件对话框
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Services;
using Exception = System.Exception;

namespace DDNCadAddins.Commands
{
    /// <summary>
    ///     提取图块信息到CSV文件命令类.
    /// </summary>
    public class Blk2CsvCommand : CommandBase
    {
        private readonly IBlockDataService blockDataService;
        private readonly ICsvExportService csvExportService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Blk2CsvCommand"/> class.
        ///     构造函数.
        /// </summary>
        public Blk2CsvCommand()
        {
            this.blockDataService = new BlockDataService();
            this.csvExportService = new CsvExportService();
        }

        /// <summary>
        ///     图块信息提取到CSV命令.
        /// </summary>
        [CommandMethod("Blk2Csv")]
        public void ExecuteBlk2Csv()
        {
            this.CommandName = "Blk2Csv";
            this.Execute();
        }

        /// <summary>
        ///     图块坐标和属性导出到CSV命令.
        /// </summary>
        [CommandMethod("ExportBlocksWithAttributes")]
        public void ExportBlocksWithAttributes()
        {
            this.CommandName = "ExportBlocksWithAttributes";
            this.Execute();
        }

        /// <summary>
        ///     执行命令的具体实现.
        /// </summary>
        protected override void ExecuteCommand()
        {
            this.Logger.Log($"开始执行{this.CommandName}命令");

            // 执行导出操作
            this.ExecuteExport();

            this.Logger.Log("命令执行完成");
        }

        /// <summary>
        ///     执行导出操作的核心方法.
        /// </summary>
        private void ExecuteExport()
        {
            // 获取当前文档和数据库
            Autodesk.AutoCAD.ApplicationServices.Document doc = this.GetDocument();
            if (doc == null)
            {
                this.Logger.Log("当前没有打开的CAD文档");
                this.AcadService.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }

            Database db = doc.Database;

            // 获取用户选择的图块
            this.Logger.Log("请求用户选择图块");
            IEnumerable<ObjectId> selectedBlocks = this.UiService.GetSelectedBlocks();
            if (selectedBlocks == null)
            {
                this.Logger.Log("用户取消了图块选择");
                return;
            }

            // 获取用户选择的CSV保存路径
            this.Logger.Log("请求用户选择CSV保存路径");
            string csvFilePath = this.UiService.GetCsvSavePath();
            if (string.IsNullOrEmpty(csvFilePath))
            {
                this.Logger.Log("用户取消了文件保存");
                return;
            }

            this.Logger.Log($"用户选择了保存路径: {csvFilePath}");

            Dictionary<ObjectId, Dictionary<string, string>> blockData = null;
            HashSet<string> allAttribTags = null;

            // 提取图块数据
            this.Logger.Log("开始提取图块数据");
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Models.OperationResult<(Dictionary<ObjectId, Dictionary<string, string>> BlockData, HashSet<string> AllAttributeTags)> result = this.blockDataService.ExtractBlockData(selectedBlocks, tr);

                if (result.Success)
                {
                    (Dictionary<ObjectId, Dictionary<string, string>> blockData1, HashSet<string> allAttributeTags) = result.Data;
                    blockData = blockData1;
                    allAttribTags = allAttributeTags;
                    this.Logger.Log($"成功提取 {blockData.Count} 个图块数据");
                }
                else
                {
                    this.Logger.Log("提取图块数据失败: " + result.ErrorMessage);
                    this.UiService.ShowErrorMessage("提取图块数据失败: " + result.ErrorMessage);
                    return;
                }

                tr.Commit();
            }

            // 导出到CSV
            try
            {
                this.Logger.Log("开始导出到CSV文件");
                Models.OperationResult<int> exportResult = this.csvExportService.ExportToCsv(blockData, allAttribTags, csvFilePath);

                if (exportResult.Success)
                {
                    int blockCount = exportResult.Data;
                    this.Logger.Log($"成功导出 {blockCount} 个图块数据到: {csvFilePath}");

                    // 显示结果
                    string resultMessage = $"\n已导出 {blockCount} 个图块数据到: {csvFilePath}";
                    this.UiService.ShowResultMessage(resultMessage);

                    // 询问是否打开CSV文件
                    if (this.UiService.AskToOpenCsvFile())
                    {
                        this.Logger.Log("用户选择打开CSV文件");
                        this.UiService.OpenFile(csvFilePath);
                    }
                }
                else
                {
                    this.Logger.LogError($"导出CSV失败: {exportResult.ErrorMessage}", null);
                    this.UiService.ShowErrorMessage($"导出CSV失败: {exportResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"写入CSV文件时出错: {ex.Message}";
                this.Logger.LogError(errorMessage, ex);
                this.UiService.ShowErrorMessage(errorMessage);
            }
        }
    }
}
