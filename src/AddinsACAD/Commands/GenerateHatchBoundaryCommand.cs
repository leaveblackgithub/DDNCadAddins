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

                var result = GenerateHatchBoundary(hatchId);

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

        /// <summary>
        ///     生成 Hatch 边界结果.
        /// </summary>
        public sealed class GenerateHatchBoundaryResult
        {
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
            public int LoopCount { get; set; }
            public int EntityCount { get; set; }
            public string TypeLog { get; set; }
            public string Uid { get; set; }
            /// <summary>生成的实体 ObjectId 列表.</summary>
            public List<ObjectId> GeneratedEntityIds { get; set; } = new List<ObjectId>();
        }

        /// <summary>
        ///     核心方法：根据 Hatch ObjectId 提取所有环的边界并生成实体.
        ///     不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="hatchId">Hatch 实体的 ObjectId.</param>
        /// <returns>生成结果.</returns>
        public static GenerateHatchBoundaryResult GenerateHatchBoundary(ObjectId hatchId)
        {
            var result = new GenerateHatchBoundaryResult();
            try
            {
                if (hatchId.IsNull || hatchId.IsErased)
                {
                    result.Message = "Hatch 无效或已被删除。";
                    return result;
                }

                int loopCount = 0;
                int entityCount = 0;
                string typeLog = "";
                string uid = "";
                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                var generatedIds = new List<ObjectId>();

                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var hatch = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                    if (hatch == null) { result.Message = "无法打开 Hatch。"; return; }

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
                            loopStart = 0; loopEnd = 1; break;
                        case HatchStyle.Outer:
                            loopStart = 0; loopEnd = Math.Min(2, loopCount); break;
                        default:
                            loopStart = 0; loopEnd = loopCount; break;
                    }
                    typeLog += $"Style={style}|";

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
                            generatedIds.Add(objId);
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
                });

                result.IsSuccess = true;
                result.Message = "生成完成";
                result.LoopCount = loopCount;
                result.EntityCount = entityCount;
                result.TypeLog = typeLog;
                result.Uid = uid;
                result.GeneratedEntityIds = generatedIds;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"GENERATEHATCHBOUNDARY 失败: {ex.Message}", ex);
                result.Message = $"GENERATEHATCHBOUNDARY 失败: {ex.Message}";
            }
            return result;
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
