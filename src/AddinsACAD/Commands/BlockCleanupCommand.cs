using System;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;
using ServiceACAD.Adapters;

[assembly: CommandClass(typeof(AddinsACAD.Commands.BlockCleanupCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     图块清理命令，自动爆炸当前空间中所有非XCLIP的图块
    /// </summary>
    public class BlockCleanupCommand
    {
        /// <summary>
        ///     执行图块清理命令
        /// </summary>
        [CommandMethod("BlockCleanup")]
        public void Execute()
        {
            try
            {
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var layerRepo = new AutoCadLayerRepository(serviceTrans);
                    var layerService = new LayerManagementService(layerRepo);

                    var snapshotResult = layerService.CaptureAllLayerStates();
                    if (!snapshotResult.IsSuccess)
                    {
                        CadServiceManager.ServiceEd.WriteMessage($"\n无法记录图层状态: {snapshotResult.Message}");
                        return;
                    }

                    var layerSnapshot = snapshotResult.Data;
                    try
                    {
                        var unlockResult = layerService.UnlockAndThawAllLayers();
                        if (!unlockResult.IsSuccess)
                        {
                            CadServiceManager.ServiceEd.WriteMessage($"\n无法解锁解冻图层: {unlockResult.Message}");
                            return;
                        }

                        var blockRepo = new AutoCadBlockRepository(serviceTrans);
                        var blockCleanupService = new BlockCleanupService(blockRepo);
                        var cleanupResult = blockCleanupService.CleanupNonXclippedBlocks();
                        if (!cleanupResult.IsSuccess)
                        {
                            CadServiceManager.ServiceEd.WriteMessage($"\n图块清理失败: {cleanupResult.Message}");
                            return;
                        }

                        WriteCleanupSummary(cleanupResult.Data);
                    }
                    finally
                    {
                        var restoreResult = layerService.RestoreLayerStates(layerSnapshot);
                        if (!restoreResult.IsSuccess)
                        {
                            CadServiceManager.ServiceEd.WriteMessage($"\n恢复图层状态失败: {restoreResult.Message}");
                        }
                    }
                });
            }
            catch (System.Exception ex)
            {
                var message = $"执行图块清理命令时发生错误: {ex.Message}";
                CadServiceManager.ServiceEd.WriteMessage($"\n{message}");
                Logger._.Error(message);
            }
        }

        /// <summary>
        ///     将清理结果输出到命令行
        /// </summary>
        /// <param name="result">清理结果</param>
        private static void WriteCleanupSummary(BlockCleanupResult result)
        {
            if (result == null)
            {
                return;
            }

            foreach (var round in result.Rounds)
            {
                if (round.AttemptedCount == 0 && round.ExplodedEntityCount == 0 && round.FailureCounts.Count == 0)
                {
                    continue;
                }

                CadServiceManager.ServiceEd.WriteMessage($"\n开始第 {round.Iteration} 轮清理...");
                WriteFailureSummary($"\n第 {round.Iteration} 轮跳过", round.FailureCounts);
                CadServiceManager.ServiceEd.WriteMessage(
                    $"\n第 {round.Iteration} 轮清理完成，尝试爆炸 {round.AttemptedCount} 个图块，成功生成 {round.ExplodedEntityCount} 个实体。");
            }

            if (result.IterationCount == 1
                && result.TotalExplodedEntityCount == 0
                && result.TotalErasedEmptyBlockCount == 0)
            {
                CadServiceManager.ServiceEd.WriteMessage("\n当前空间没有需要清理的图块。");
                return;
            }

            CadServiceManager.ServiceEd.WriteMessage(
                $"\n清理完成，共执行 {result.IterationCount} 轮，总共生成了 {result.TotalExplodedEntityCount} 个实体。");
            if (result.TotalErasedEmptyBlockCount > 0)
            {
                CadServiceManager.ServiceEd.WriteMessage($"\n已删除 {result.TotalErasedEmptyBlockCount} 个空定义图块。");
            }

            WriteFailureSummary("\n以下图块未能爆炸", result.FailureCounts);
        }

        /// <summary>
        ///     将失败汇总信息输出到命令行
        /// </summary>
        /// <param name="title">汇总标题</param>
        /// <param name="failureCounts">失败统计</param>
        private static void WriteFailureSummary(string title, System.Collections.Generic.Dictionary<string, int> failureCounts)
        {
            if (failureCounts == null || failureCounts.Count == 0)
            {
                return;
            }

            CadServiceManager.ServiceEd.WriteMessage(title + "：");
            foreach (var failure in failureCounts.OrderByDescending(item => item.Value))
            {
                CadServiceManager.ServiceEd.WriteMessage($"\n  {failure.Value} 个: {failure.Key}");
            }
        }
    }
}
