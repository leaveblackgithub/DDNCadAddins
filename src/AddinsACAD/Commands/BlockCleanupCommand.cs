using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
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
                // 使用事务服务执行操作
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

                        RunCleanupLoop(serviceTrans);
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
        ///     执行多轮图块清理主循环
        /// </summary>
        /// <param name="serviceTrans">事务服务</param>
        private void RunCleanupLoop(ITransactionService serviceTrans)
        {
            var totalExploded = 0;
            var totalErasedEmpty = 0;
            var iteration = 0;
            var hasMoreBlocks = true;
            var skippedBlockIds = new HashSet<ObjectId>();
            var totalFailureCounts = new Dictionary<string, int>();

            while (hasMoreBlocks)
            {
                iteration++;
                CadServiceManager.ServiceEd.WriteMessage($"\n开始第 {iteration} 轮清理...");

                var blockRefIds = GetNonXclippedBlocks(serviceTrans);
                if (blockRefIds.Count == 0)
                {
                    hasMoreBlocks = false;
                    continue;
                }

                var roundExploded = 0;
                var roundAttempted = 0;
                var roundFailureCounts = new Dictionary<string, int>();

                for (var index = 0; index < blockRefIds.Count; index++)
                {
                    var blockRefId = blockRefIds[index];
                    if (skippedBlockIds.Contains(blockRefId))
                    {
                        continue;
                    }

                    roundAttempted++;
                    var blockService = serviceTrans.Block.GetBlockService(blockRefId);
                    if (blockService == null)
                    {
                        RecordFailure("无法获取图块服务", roundFailureCounts, totalFailureCounts);
                        skippedBlockIds.Add(blockRefId);
                        continue;
                    }

                    var explodeResult = blockService.ExplodeAsShown();
                    if (!explodeResult.IsSuccess)
                    {
                        if (explodeResult.Message == "块定义不含实体")
                        {
                            var eraseResult = blockService.EraseIfEmptyDefinition();
                            if (eraseResult.IsSuccess)
                            {
                                totalErasedEmpty++;
                                continue;
                            }

                            RecordFailure(eraseResult.Message, roundFailureCounts, totalFailureCounts);
                        }
                        else
                        {
                            RecordFailure(explodeResult.Message, roundFailureCounts, totalFailureCounts);
                        }

                        skippedBlockIds.Add(blockRefId);
                        continue;
                    }

                    roundExploded += explodeResult.Data.Count;
                }

                WriteFailureSummary($"\n第 {iteration} 轮跳过", roundFailureCounts);

                totalExploded += roundExploded;
                CadServiceManager.ServiceEd.WriteMessage(
                    $"\n第 {iteration} 轮清理完成，尝试爆炸 {roundAttempted} 个图块，成功生成 {roundExploded} 个实体。");

                var remainingBlocks = GetNonXclippedBlocks(serviceTrans)
                    .Where(id => !skippedBlockIds.Contains(id))
                    .ToList();
                if (remainingBlocks.Count == 0 || roundExploded == 0)
                {
                    hasMoreBlocks = false;
                }
            }

            if (iteration == 1 && totalExploded == 0 && totalErasedEmpty == 0)
            {
                CadServiceManager.ServiceEd.WriteMessage("\n当前空间没有需要清理的图块。");
            }
            else
            {
                CadServiceManager.ServiceEd.WriteMessage($"\n清理完成，共执行 {iteration} 轮，总共生成了 {totalExploded} 个实体。");
                if (totalErasedEmpty > 0)
                {
                    CadServiceManager.ServiceEd.WriteMessage($"\n已删除 {totalErasedEmpty} 个空定义图块。");
                }

                WriteFailureSummary("\n以下图块未能爆炸", totalFailureCounts);
            }
        }

        /// <summary>
        ///     获取当前空间中所有非XCLIP的图块
        /// </summary>
        /// <param name="serviceTrans">事务服务</param>
        /// <returns>非XCLIP图块的ObjectId列表</returns>
        private List<ObjectId> GetNonXclippedBlocks(ITransactionService serviceTrans)
        {
            var result = new List<ObjectId>();

            // 获取当前空间所有图块
            var allBlocks = serviceTrans.GetChildObjectsFromCurrentSpace<BlockReference>();
            if (allBlocks.Count == 0)
            {
                return result;
            }

            // 过滤出非XCLIP的图块
            foreach (var blockId in allBlocks)
            {
                var blockService = serviceTrans.Block.GetBlockService(blockId);
                if (blockService == null)
                {
                    continue;
                }

                if (blockService.IsXclipped())
                {
                    continue;
                }
                result.Add(blockId);
            }

            return result;
        }

        /// <summary>
        ///     记录失败原因并按原因汇总数量
        /// </summary>
        /// <param name="reason">失败原因</param>
        /// <param name="roundFailureCounts">本轮失败统计</param>
        /// <param name="totalFailureCounts">累计失败统计</param>
        private static void RecordFailure(
            string reason,
            Dictionary<string, int> roundFailureCounts,
            Dictionary<string, int> totalFailureCounts)
        {
            var message = string.IsNullOrWhiteSpace(reason) ? "未知错误" : reason;
            IncrementCount(roundFailureCounts, message);
            IncrementCount(totalFailureCounts, message);
        }

        /// <summary>
        ///     将失败汇总信息输出到命令行
        /// </summary>
        /// <param name="title">汇总标题</param>
        /// <param name="failureCounts">失败统计</param>
        private static void WriteFailureSummary(string title, Dictionary<string, int> failureCounts)
        {
            if (failureCounts.Count == 0)
            {
                return;
            }

            CadServiceManager.ServiceEd.WriteMessage(title + "：");
            foreach (var failure in failureCounts.OrderByDescending(item => item.Value))
            {
                CadServiceManager.ServiceEd.WriteMessage($"\n  {failure.Value} 个: {failure.Key}");
            }
        }

        /// <summary>
        ///     增加字典中指定键的计数
        /// </summary>
        /// <param name="counts">计数字典</param>
        /// <param name="key">键</param>
        private static void IncrementCount(Dictionary<string, int> counts, string key)
        {
            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
            else
            {
                counts[key] = 1;
            }
        }
    }
} 
