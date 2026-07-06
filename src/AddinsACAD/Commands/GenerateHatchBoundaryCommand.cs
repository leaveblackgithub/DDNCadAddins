using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.GenerateHatchBoundaryCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     GENERATEHATCHBOUNDARY 命令 — 提取 Hatch 边界环并生成实体.
    ///     UI 层委托给 <see cref="HatchBoundaryService.GenerateHatchBoundary"/> 实现.
    /// </summary>
    public class GenerateHatchBoundaryCommand
    {
        [CommandMethod("GENERATEHATCHBOUNDARY")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                var hatchId = this.SelectSingleHatch(ed);
                if (hatchId.IsNull) return;

                var result = HatchBoundaryService.GenerateHatchBoundary(hatchId);

                if (result.IsSuccess)
                {
                    ed.WriteMessage($"\n生成完成：{result.LoopCount} 个环，{result.EntityCount} 个实体 [{result.TypeLog}]");
                    if (!string.IsNullOrEmpty(result.Uid))
                        ed.WriteMessage($"\n[TestRecorder] UID: {result.Uid}");
                }
                else
                {
                    ed.WriteMessage($"\n{result.Message}");
                }
            }
            catch (System.Exception ex)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc.Editor.WriteMessage($"\nGENERATEHATCHBOUNDARY 失败: {ex.Message}");
                Logger._.Error($"GENERATEHATCHBOUNDARY 失败: {ex.Message}", ex);
            }
        }

        private ObjectId SelectSingleHatch(Editor ed)
        {
            try
            {
                var opt = new PromptEntityOptions("\n选择要提取边界的 Hatch: ");
                opt.SetRejectMessage("\n请选择 Hatch。");
                opt.AddAllowedClass(typeof(Hatch), false);
                var res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                { ed.WriteMessage("\n未选择 Hatch。"); return ObjectId.Null; }
                return res.ObjectId;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择 Hatch 失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }
    }
}
