using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using DDNCadAddins.Core.Models;

[assembly: CommandClass(typeof(AddinsACAD.Commands.GenerateHatchBoundaryCommand))]

namespace AddinsACAD.Commands
{
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

                int loopCount = 0;
                int entityCount = 0;
                string uid = "";
                string typeLog = "";
                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var hatch = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                    if (hatch == null) { ed.WriteMessage("\n无法打开 Hatch。"); return; }

                    var plane = new Plane(
                        Point3d.Origin + hatch.Normal * hatch.Elevation,
                        hatch.Normal);
                    loopCount = hatch.NumberOfLoops;

                    // 根据 HatchStyle 确定要处理的环范围
                    int loopStart, loopEnd;
                    var style = hatch.HatchStyle;
                    switch (style)
                    {
                        case HatchStyle.Ignore:
                            loopStart = 0; loopEnd = 1; break;        // 仅最外层
                        case HatchStyle.Outer:
                            loopStart = 0; loopEnd = Math.Min(2, loopCount); break; // 外侧两个环
                        default: // NORMAL
                            loopStart = 0; loopEnd = loopCount; break; // 所有环
                    }
                    typeLog += $"Style={style}|";

                    // 使用 CurveToPolygonConverter 统一处理每个环
                    var generator = new CurveToPolygonConverter();

                    for (int li = loopStart; li < loopEnd; li++)
                    {
                        var loop = hatch.GetLoopAt(li);
                        if (loop == null) continue;

                        bool isOuter = (li == 0);
                        int color = isOuter ? 2 : 4;

                        var objId = generator.CreateEntityFromLoop(loop, plane, color, hatch.Layer, ts);
                        if (!objId.IsNull)
                        {
                            entityCount++;
                            typeLog += $"Entity|";
                        }
                    }

                    var record = new CropTestRecord
                    {
                        Command = "GENERATEHATCHBOUNDARY",
                        IsSuccess = true,
                        UcsOrigin = ucsO, UcsXAxis = ucsX, UcsYAxis = ucsY,
                        TotalEntityCount = loopCount,
                        DeletedCount = 0,
                        KeptCount = entityCount,
                        SkippedCount = 0,
                        Entities = new List<CropEntitySnapshot>(),
                    };
                    uid = TestRecorder.Record(record);
                    ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                });

                ed.WriteMessage($"\n生成完成：{loopCount} 个环，{entityCount} 个实体 [{typeLog}]");
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
