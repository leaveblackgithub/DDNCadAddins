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
                // ── 1. 输入获取 ──
                var blockRefIds = CadServiceManager.ServiceEd.GetSelectedBlockReferences("\n选择要炸开的图块：");
                if (blockRefIds.Count == 0)
                {
                    WriteOutput("\n未选择图块或选择被取消。");
                    return;
                }

                // ── 2. 主体逻辑（委托给 BlockExploder.ExplodeMultiple） ──
                using (var cancellation = new CommandCancellationScope())
                {
                    OpResult<ExplodeMultipleResult> explodeResult = null;
                    var transactionResult = CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                    {
                        explodeResult = BlockExploder.ExplodeMultiple(blockRefIds, serviceTrans, cancellation);
                        return explodeResult.IsSuccess
                            ? ServiceACAD.OpResult.Success()
                            : ServiceACAD.OpResult.Fail(explodeResult.Message);
                    });

                    if (!transactionResult.IsSuccess || explodeResult == null || !explodeResult.IsSuccess)
                    {
                        WriteOutput($"\n{explodeResult?.Message ?? transactionResult.Message ?? "未知错误"}");
                        return;
                    }

                    var data = explodeResult.Data;

                    // ── 3. 输出显示 ──
                    foreach (var detail in data.Details)
                    {
                        WriteOutput(ExplodeReportFormatter.FormatBlockLine(detail.BlockName, detail.Stats));
                    }

                    WriteOutput($"\n成功爆炸 {data.SuccessCount} 个图块，生成了 {data.TotalExploded} 个实体。");
                    WriteFailureList(data.FailedBlocks);
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
        private static void WriteFailureList(System.Collections.Generic.IReadOnlyList<string> failedBlocks)
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
