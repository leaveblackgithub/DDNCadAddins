using System;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;
using ServiceACAD.Adapters;
using Exception = System.Exception;

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
            BlockCleanupResult cleanupResult = null;
            var statusMessage = string.Empty;

            try
            {
                using (var cancellation = new CommandCancellationScope())
                {
                    var cleanupOptions = new BlockCleanupOptions
                    {
                        IsCancellationRequested = () => cancellation.IsCancellationRequested,
                        OnRoundStarted = iteration =>
                        {
                            if (iteration > 1)
                            {
                                WriteOutput($"\n开始第 {iteration} 轮清理...");
                            }
                        },
                        OnBlockExploded = report => WriteOutput(ExplodeReportFormatter.FormatBlockLine(report))
                    };

                    var transactionResult = CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                    {
                        var layerRepo = new AutoCadLayerRepository(serviceTrans);
                        var layerService = new LayerManagementService(layerRepo);

                        var snapshotResult = layerService.CaptureAllLayerStates();
                        if (!snapshotResult.IsSuccess)
                        {
                            statusMessage = $"\n无法记录图层状态: {snapshotResult.Message}";
                            return ServiceACAD.OpResult.Fail(snapshotResult.Message);
                        }

                        var layerSnapshot = snapshotResult.Data;
                        try
                        {
                            var unlockResult = layerService.UnlockAndThawAllLayers();
                            if (!unlockResult.IsSuccess)
                            {
                                statusMessage = $"\n无法解锁解冻图层: {unlockResult.Message}";
                                return ServiceACAD.OpResult.Fail(unlockResult.Message);
                            }

                            var blockRepo = new AutoCadBlockRepository(serviceTrans);
                            var blockCleanupService = new BlockCleanupService(blockRepo);
                            var result = blockCleanupService.CleanupNonXclippedBlocks(cleanupOptions);
                            if (!result.IsSuccess)
                            {
                                statusMessage = $"\n{result.Message}";
                                return ServiceACAD.OpResult.Fail(result.Message);
                            }

                            cleanupResult = result.Data;
                            return ServiceACAD.OpResult.Success();
                        }
                        finally
                        {
                            var restoreResult = layerService.RestoreLayerStates(layerSnapshot);
                            if (!restoreResult.IsSuccess)
                            {
                                var restoreMessage = $"\n恢复图层状态失败: {restoreResult.Message}";
                                statusMessage = string.IsNullOrEmpty(statusMessage)
                                    ? restoreMessage
                                    : statusMessage + restoreMessage;
                            }
                        }
                    });

                    if (!transactionResult.IsSuccess && string.IsNullOrEmpty(statusMessage))
                    {
                        statusMessage = $"\n{transactionResult.Message}";
                    }
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    WriteOutput(statusMessage);
                }

                if (cleanupResult != null)
                {
                    WriteCleanupSummary(cleanupResult);
                }
            }
            catch (Exception ex)
            {
                var message = $"执行图块清理命令时发生错误: {ex.Message}";
                WriteOutput($"\n{message}");
                Logger._.Error(message);
            }
        }

        /// <summary>
        ///     将清理汇总输出到命令行（逐块明细已在处理过程中实时输出）
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

                WriteFailureSummary($"\n第 {round.Iteration} 轮跳过", round.FailureCounts);
                WriteOutput(
                    $"\n第 {round.Iteration} 轮清理完成，尝试爆炸 {round.AttemptedCount} 个图块，成功生成 {round.ExplodedEntityCount} 个实体。");
            }

            if (result.IterationCount == 1
                && result.TotalExplodedEntityCount == 0
                && result.TotalErasedEmptyBlockCount == 0)
            {
                WriteOutput("\n当前空间没有需要清理的图块。");
                return;
            }

            WriteOutput(
                $"\n清理完成，共执行 {result.IterationCount} 轮，总共生成了 {result.TotalExplodedEntityCount} 个实体。");
            if (result.TotalErasedEmptyBlockCount > 0)
            {
                WriteOutput($"\n已删除 {result.TotalErasedEmptyBlockCount} 个空定义图块。");
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

            WriteOutput(title + "：");
            foreach (var failure in failureCounts.OrderByDescending(item => item.Value))
            {
                WriteOutput($"\n  {failure.Value} 个: {failure.Key}");
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
