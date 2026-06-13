using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using Exception = System.Exception;

[assembly: CommandClass(typeof(AddinsACAD.Commands.ExplodeAsShownCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     图块爆炸命令，将选中的图块按照显示状态爆炸
    /// </summary>
    public class ExplodeAsShownCommand
    {
        /// <summary>
        ///     执行图块爆炸命令
        /// </summary>
        [CommandMethod("ExplodeAsShown")]
        public void ExplodeSelected()
        {
            try
            {
                var blockRefIds = CadServiceManager.ServiceEd.GetSelectedBlockReferences("\n选择要炸开的图块：");
                if (blockRefIds.Count == 0)
                {
                    WriteOutput("\n未选择图块或选择被取消。");
                    return;
                }

                using (var cancellation = new CommandCancellationScope())
                {
                    var successCount = 0;
                    var totalExploded = 0;
                    var failedBlocks = new List<string>();

                    var transactionResult = CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                    {
                        foreach (var blockRefId in blockRefIds)
                        {
                            if (cancellation.IsCancellationRequested)
                            {
                                return OpResult.Fail(CommandCancellationScope.UserCancelledMessage);
                            }

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

                            successCount++;
                            totalExploded += explodeResult.Data?.EntityIds?.Count ?? 0;
                            WriteOutput(ExplodeReportFormatter.FormatBlockLine(blockName, explodeResult.Data));

                            if (cancellation.IsCancellationRequested)
                            {
                                return OpResult.Fail(CommandCancellationScope.UserCancelledMessage);
                            }
                        }

                        return OpResult.Success();
                    });

                    if (!transactionResult.IsSuccess)
                    {
                        if (!string.IsNullOrEmpty(transactionResult.Message))
                        {
                            WriteOutput($"\n{transactionResult.Message}");
                        }

                        return;
                    }

                    WriteOutput($"\n成功爆炸 {successCount} 个图块，生成了 {totalExploded} 个实体。");
                    WriteFailureList(failedBlocks);
                }
            }
            catch (Exception ex)
            {
                var message = $"执行图块爆炸命令时发生错误: {ex.Message}";
                WriteOutput($"\n{message}");
                Logger._.Error(message);
            }
        }

        /// <summary>
        ///     输出失败列表
        /// </summary>
        /// <param name="failedBlocks">失败信息</param>
        private static void WriteFailureList(IReadOnlyList<string> failedBlocks)
        {
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
