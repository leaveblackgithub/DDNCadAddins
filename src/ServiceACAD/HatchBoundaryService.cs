using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     Hatch 边界生成服务 — 根据 Hatch ObjectId 提取所有环的边界并生成实体.
    ///     从 <c>GenerateHatchBoundaryCommand</c> 提取的核心方法，无 UI 交互.
    /// </summary>
    public static class HatchBoundaryService
    {
        /// <summary>
        ///     生成 Hatch 边界结果.
        /// </summary>
        public sealed class GenerateHatchBoundaryResult
        {
            /// <summary>操作是否成功.</summary>
            public bool IsSuccess { get; set; }

            /// <summary>结果消息.</summary>
            public string Message { get; set; }

            /// <summary>环数量.</summary>
            public int LoopCount { get; set; }

            /// <summary>生成的实体数量.</summary>
            public int EntityCount { get; set; }

            /// <summary>类型日志.</summary>
            public string TypeLog { get; set; }

            /// <summary>TestRecorder UID.</summary>
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

                    // 生成所有环的边界实体（不论 HatchStyle），
                    // 让裁剪后的 Hatch 重建时由 HatchStyle 自动判断内外环关系.
                    int loopStart = 0;
                    int loopEnd = loopCount;
                    var style = hatch.HatchStyle;
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
            catch (Exception ex)
            {
                Logger._.Error($"GENERATEHATCHBOUNDARY 失败: {ex.Message}", ex);
                result.Message = $"GENERATEHATCHBOUNDARY 失败: {ex.Message}";
            }
            return result;
        }
    }
}
