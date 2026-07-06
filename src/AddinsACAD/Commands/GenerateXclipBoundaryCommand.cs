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
    ///     生成 Xclip 边界命令，为选中的图块生成 Xclip 边界.
    ///     输入获取 → 委托 <see cref="XClipBoundaryService.GenerateBatch"/> → 输出显示.
    /// </summary>
    public class GenerateXclipBoundaryCommand
    {
        /// <summary>
        ///     执行生成 Xclip 边界命令.
        /// </summary>
        [CommandMethod("GenerateXclipBoundary")]
        public void Execute()
        {
            try
            {
                // ── 1. 输入获取：选择要处理的图块 ──
                var blockRefIds = CadServiceManager.ServiceEd.GetSelectedBlockReferences(
                    "\n请选择要生成 xclip 边界的图块");
                if (blockRefIds.Count == 0)
                {
                    CadServiceManager.ServiceEd.WriteMessage("\n未选择图块或选择被取消。");
                    return;
                }

                // ── 2. 主体逻辑：批量生成 XClip 边界（委托到 XClipBoundaryService） ──
                XClipBoundaryService.BatchResult batchResult = null;
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var result = XClipBoundaryService.GenerateBatch(serviceTrans, blockRefIds);
                    if (result.IsSuccess)
                    {
                        batchResult = result.Data;
                    }
                    else
                    {
                        CadServiceManager.ServiceEd.WriteMessage($"\n{result.Message}");
                    }
                });

                // ── 3. 输出显示 ──
                if (batchResult == null)
                    return;

                CadServiceManager.ServiceEd.WriteMessage(
                    $"\n成功为 {batchResult.SuccessCount} 个图块生成 Xclip 边界。");

                if (batchResult.FailedMessages.Count > 0)
                {
                    CadServiceManager.ServiceEd.WriteMessage("\n以下图块处理失败：");
                    foreach (var error in batchResult.FailedMessages)
                    {
                        CadServiceManager.ServiceEd.WriteMessage($"\n{error}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                var message = $"执行生成 Xclip 边界命令时发生错误: {ex.Message}";
                CadServiceManager.ServiceEd.WriteMessage($"\n{message}");
                Logger._.Error(message);
            }
        }
    }
}
