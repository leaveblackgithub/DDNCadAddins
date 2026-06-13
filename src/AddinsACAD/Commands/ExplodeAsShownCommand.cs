using System.Collections.Generic;
using AddinsACAD.Commands;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using Exception = System.Exception;

[assembly: CommandClass(typeof(ExplodeAsShownCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     图块爆炸命令，将选中的图块按照显示状态爆炸
    /// </summary>
    public class ExplodeAsShownCommand
    {
        /// <summary>
        ///     单个图块爆炸报告
        /// </summary>
        private sealed class BlockExplodeReport
        {
            public string BlockName { get; set; }
            public ExplodeAsShownResult Stats { get; set; }
        }

        /// <summary>
        ///     执行图块爆炸命令
        /// </summary>
        [CommandMethod("ExplodeAsShown")]
        public void ExplodeSelected()
        {
            try
            {
                // 1. 输入获取：在事务外选择图块，避免选择提示与命令行输出被事务锁定影响
                var blockRefIds = CadServiceManager.ServiceEd.GetSelectedBlockReferences("\n选择要炸开的图块：");
                if (blockRefIds.Count == 0)
                {
                    WriteOutput("\n未选择图块或选择被取消。");
                    return;
                }

                var successReports = new List<BlockExplodeReport>();
                var failedBlocks = new List<string>();

                // 2. 主体逻辑：在事务内执行爆炸
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    foreach (var blockRefId in blockRefIds)
                    {
                        var blockService = serviceTrans.Block.GetBlockService(blockRefId);
                        if (blockService == null)
                        {
                            failedBlocks.Add($"无法获取图块服务: {blockRefId}");
                            continue;
                        }

                        var blockName = blockService.Name;
                        var explodeResult = blockService.ExplodeAsShown();
                        if (!explodeResult.IsSuccess)
                        {
                            failedBlocks.Add($"爆炸图块 {blockName} 失败: {explodeResult.Message}");
                            continue;
                        }

                        successReports.Add(new BlockExplodeReport
                        {
                            BlockName = blockName,
                            Stats = explodeResult.Data
                        });
                    }
                });

                // 3. 输出显示：事务提交后再写入命令行
                WriteExplodeReports(successReports, failedBlocks);
            }
            catch (Exception ex)
            {
                var message = $"执行图块爆炸命令时发生错误: {ex.Message}";
                WriteOutput($"\n{message}");
                Logger._.Error(message);
            }
        }

        /// <summary>
        ///     将爆炸结果输出到命令行
        /// </summary>
        /// <param name="successReports">成功爆炸的图块报告</param>
        /// <param name="failedBlocks">失败信息列表</param>
        private static void WriteExplodeReports(
            IReadOnlyList<BlockExplodeReport> successReports,
            IReadOnlyList<string> failedBlocks)
        {
            var totalExploded = 0;
            foreach (var report in successReports)
            {
                var stats = report.Stats;
                var entityCount = stats?.EntityIds?.Count ?? 0;
                totalExploded += entityCount;
                WriteOutput(
                    $"\n{report.BlockName}: 属性转文字 {stats?.AttributeTextCount ?? 0} 个，" +
                    $"图层继承 {stats?.LayerAdjustedCount ?? 0} 个，颜色继承 {stats?.ColorAdjustedCount ?? 0} 个。");
            }

            WriteOutput($"\n成功爆炸 {successReports.Count} 个图块，生成了 {totalExploded} 个实体。");

            if (failedBlocks == null || failedBlocks.Count == 0)
            {
                return;
            }

            WriteOutput("\n以下图块爆炸失败：");
            foreach (var error in failedBlocks)
            {
                WriteOutput($"\n{error}");
            }
        }

        /// <summary>
        ///     写入命令行并刷新显示
        /// </summary>
        /// <param name="message">要输出的消息</param>
        private static void WriteOutput(string message)
        {
            CadServiceManager.ServiceEd.WriteMessage(message);
            CadServiceManager.ServiceEd.Update();
        }
    }
}
