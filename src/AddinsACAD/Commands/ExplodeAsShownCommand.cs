using System.Collections.Generic;
using System.Linq;
using AddinsACAD.Commands;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
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
        ///     执行图块爆炸命令
        /// </summary>
        [CommandMethod("ExplodeAsShown")]
        public void ExplodeSelected()
        {
            try
            {
                // 使用事务服务执行操作
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    // 2. 主体逻辑：执行爆炸操作
                    var success = 0;
                    var totalExploded = 0;
                    var failedBlocks = new List<string>();

                    // 1. 输入获取：获取要爆炸的图块
                    var blockRefIds = CadServiceManager.ServiceEd.GetSelectedBlockReferences("选择要炸开的图块：");
                    if (blockRefIds.Count == 0 )
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n未选择图块或选择被取消。");
                        return;
                    }

                    
                    for (var i=0;i<blockRefIds.Count;i++)
                    {
                        var blockRefId = blockRefIds[i];
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

                        success++;
                        totalExploded += explodeResult.Data.Count;
                    }


                    CadServiceManager.ServiceEd.WriteMessage($"\n成功爆炸 {success} 个图块，生成了 {totalExploded} 个实体。");

                    // 3. 输出显示：显示操作结果
                    if (failedBlocks.Count > 0)
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n以下图块爆炸失败：");
                        foreach (var error in failedBlocks)
                        {
                            CadServiceManager.ServiceEd.WriteMessage($"\n{error}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                var message = $"执行图块爆炸命令时发生错误: {ex.Message}";
                CadServiceManager.ServiceEd.WriteMessage($"\n{message}");
                Logger._.Error(message);
            }
        }
    }
}
