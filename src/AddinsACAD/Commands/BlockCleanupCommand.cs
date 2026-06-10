using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;

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
                    var totalExploded = 0;
                    var iteration = 0;
                    var hasMoreBlocks = true;
                    // MyMessageFilter filter = new MyMessageFilter();
                    //
                    // System.Windows.Forms.Application.AddMessageFilter(filter);
                    while (hasMoreBlocks)
                    {
                        iteration++;
                        CadServiceManager.ServiceEd.WriteMessage($"\n开始第 {iteration} 轮清理...");

                        // 1. 获取当前空间所有图块
                        var blockRefIds = GetNonXclippedBlocks(serviceTrans);
                        if (blockRefIds.Count == 0)
                        {
                            hasMoreBlocks = false;
                            continue;
                        }

                        // 2. 执行爆炸操作
                        var roundExploded = 0;
                        var failedBlocks = new List<string>();

                        for (var index = 0; index < blockRefIds.Count; index++)
                        {
                            var blockRefId = blockRefIds[index];
                            // Check for user input events

                            // System.Windows.Forms.Application.DoEvents();
                            //
                            // // Check whether the filter has set the flag
                            //
                            // if (filter.bCanceled == true)
                            //
                            // {
                            //
                            //     CadServiceManager.ServiceEd.WriteMessage("\nLoop cancelled.");
                            //     hasMoreBlocks = false;
                            //     break;
                            //
                            // }
                            var blockService = serviceTrans.Block.GetBlockService(blockRefId);
                            if (blockService == null)
                            {
                                failedBlocks.Add($"无法获取图块服务: {blockRefId}");
                                continue;
                            }

                            var explodeResult = blockService.ExplodeAsShown();
                            if (!explodeResult.IsSuccess)
                            {
                                failedBlocks.Add($"爆炸图块失败: {explodeResult.Message}");
                                continue;
                            }

                            roundExploded += explodeResult.Data.Count;
                        }

                        // 3. 显示本轮结果
                        if (failedBlocks.Count > 0)
                        {
                            CadServiceManager.ServiceEd.WriteMessage("\n本轮清理中以下图块爆炸失败：");
                            foreach (var error in failedBlocks)
                            {
                                CadServiceManager.ServiceEd.WriteMessage($"\n{error}");
                            }
                        }

                        totalExploded += roundExploded;
                        CadServiceManager.ServiceEd.WriteMessage($"\n第 {iteration} 轮清理完成，爆炸了 {blockRefIds.Count} 个图块，生成了 {roundExploded} 个实体。");

                        // 4. 检查是否还有可爆炸的图块
                        if (roundExploded == 0)
                        {
                            hasMoreBlocks = false;
                        }
                    }

                    // 5. 显示最终结果
                    if (iteration == 1 && totalExploded == 0)
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n当前空间没有需要清理的图块。");
                    }
                    else
                    {
                        CadServiceManager.ServiceEd.WriteMessage($"\n清理完成，共执行 {iteration} 轮，总共生成了 {totalExploded} 个实体。");
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
    }
} 
