using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.GenerateXclipBoundaryCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     生成Xclip边界命令，为选中的图块生成Xclip边界
    /// </summary>
    public class GenerateXclipBoundaryCommand
    {
        /// <summary>
        ///     执行生成Xclip边界命令
        /// </summary>
        [CommandMethod("GenerateXclipBoundary")]
        public void Execute()
        {
            try
            {
                // 使用事务服务执行操作
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    // 1. 输入获取：获取要处理的图块
                    var blockRefIds = CadServiceManager.ServiceEd.GetSelectedBlockReferences("\n请选择要生成xclip边界的图块");
                    if (blockRefIds.Count == 0)
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n未选择图块或选择被取消。");
                        return;
                    }

                    // 2. 主体逻辑：生成Xclip边界
                    var success = 0;
                    var failedBlocks = new List<string>();

                    foreach (var blockRefId in blockRefIds)
                    {
                        var blockService = serviceTrans.Block.GetBlockService(blockRefId);
                        if (blockService == null)
                        {
                            failedBlocks.Add($"无法获取图块服务: {blockRefId}");
                            continue;
                        }

                        // 检查图块是否已经有Xclip边界
                        if (!blockService.IsXclipped())
                        {
                            failedBlocks.Add($"图块不存在XClip边界 (名称: {blockService.Name})");
                            continue;
                        }

                        // 生成Xclip边界
                        var result = blockService.GenerateXclipBoundary();
                        if (!result.IsSuccess)
                        {
                            failedBlocks.Add($"生成XClip边界失败: {result.Message} (名称: {blockService.Name})");
                            continue;
                        }

                        CadServiceManager.ServiceEd.WriteMessage($"\n已为图块 {blockService.Name} 创建XClip边界");
                        success++;
                    }

                    // 3. 输出显示：显示操作结果
                    CadServiceManager.ServiceEd.WriteMessage($"\n成功为 {success} 个图块生成Xclip边界。");

                    if (failedBlocks.Count > 0)
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n以下图块处理失败：");
                        foreach (var error in failedBlocks)
                        {
                            CadServiceManager.ServiceEd.WriteMessage($"\n{error}");
                        }
                    }
                });
            }
            catch (System.Exception ex)
            {
                var message = $"执行生成Xclip边界命令时发生错误: {ex.Message}";
                CadServiceManager.ServiceEd.WriteMessage($"\n{message}");
                Logger._.Error(message);
            }
        }

        

       
    }
} 
