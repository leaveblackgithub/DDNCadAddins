using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropHatchCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPHATCH 命令 — 选择闭合边界曲线，再选择 Hatch，询问裁剪方向后
    ///     先调用 GenerateHatchBoundary 生成边界实体，后续裁剪逻辑待确认.
    ///     同时提供 CROPALLHATCHES 自动选择所有 Hatch.
    /// </summary>
    public class CropHatchCommand
    {
        /// <summary>
        ///     执行 CROPHATCH 命令：选择边界（单选），再手动选择 Hatch，询问裁剪方向.
        /// </summary>
        [CommandMethod("CROPHATCH")]
        public void Execute()
        {
            this.ExecuteCropHatch(selectAllHatches: false);
        }

        /// <summary>
        ///     执行 CROPALLHATCHES 命令：选择边界（单选），自动选择所有 Hatch，询问裁剪方向.
        /// </summary>
        [CommandMethod("CROPALLHATCHES")]
        public void ExecuteAll()
        {
            this.ExecuteCropHatch(selectAllHatches: true);
        }

        /// <summary>
        ///     核心执行逻辑.
        /// </summary>
        /// <param name="selectAllHatches">是否自动选择图纸中所有 Hatch，false 则让用户手动选择.</param>
        private void ExecuteCropHatch(bool selectAllHatches)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                // 1. 选择边界（单选）
                var boundaryPoints = this.SelectSingleBoundaryCurve(ed, out var boundaryId);
                if (boundaryPoints == null || boundaryPoints.Count < 3)
                    return;

                // 2. 选择或获取 Hatch
                List<ObjectId> hatchIds = null;
                if (selectAllHatches)
                {
                    ed.WriteMessage("\n正在自动选择图纸中所有 Hatch...");
                    List<ObjectId> autoHatchIds = null;
                    CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                    {
                        autoHatchIds = serviceTrans.GetChildObjectsFromModelspace<Hatch>();
                    });

                    if (autoHatchIds == null || autoHatchIds.Count == 0)
                    {
                        ed.WriteMessage("\n图纸中没有找到任何 Hatch。");
                        return;
                    }

                    // 排除边界自身
                    autoHatchIds.RemoveAll(id => id == boundaryId);
                    if (autoHatchIds.Count == 0)
                    {
                        ed.WriteMessage("\n排除边界后没有其他 Hatch。");
                        return;
                    }

                    ed.WriteMessage($"\n已排除边界实体，剩余 {autoHatchIds.Count} 个 Hatch。");
                    hatchIds = autoHatchIds;
                }
                else
                {
                    hatchIds = this.SelectHatchesToCrop(ed);
                    if (hatchIds == null || hatchIds.Count == 0)
                        return;
                    // 手动选择也排除边界自身
                    hatchIds.RemoveAll(id => id == boundaryId);
                }

                // 3. 询问裁剪方向：保留内部还是外部
                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue)
                    return; // 用户取消

                // ── 采集 UCS ──
                ServiceACAD.TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);
                var capturedBoundaryVerts = boundaryPoints;
                var capturedHatchIds = hatchIds;
                var capturedKeepInside = keepInside.Value;
                string directionLabel = capturedKeepInside ? "内部" : "外部";

                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   CROPHATCH 开始 — 裁剪方向: {directionLabel}");
                ed.WriteMessage($"\n═══════════════════════════════════════════");

                int totalBoundaryEntities = 0;
                int totalHatchesProcessed = 0;
                string commandName = selectAllHatches ? "CROPALLHATCHES" : "CROPHATCH";

                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        foreach (var hatchId in capturedHatchIds)
                        {
                            if (!hatchId.IsValid || hatchId.IsErased)
                                continue;

                            // ★ 调用 GenerateHatchBoundary 生成边界实体
                            var genResult = GenerateHatchBoundaryCommand.GenerateHatchBoundary(hatchId);
                            if (genResult.IsSuccess)
                            {
                                totalBoundaryEntities += genResult.EntityCount;
                                totalHatchesProcessed++;
                                ed.WriteMessage($"\n  Hatch {hatchId}: 生成 {genResult.EntityCount} 个边界实体 [{genResult.TypeLog}]");
                            }
                            else
                            {
                                ed.WriteMessage($"\n  Hatch {hatchId}: 边界生成失败 — {genResult.Message}");
                            }
                        }

                        // 构建 TestRecord
                        var record = new CropTestRecord
                        {
                            Command = commandName,
                            Direction = capturedKeepInside ? "Inside" : "Outside",
                            IsSuccess = true,
                            UcsOrigin = ucsOrigin,
                            UcsXAxis = ucsX,
                            UcsYAxis = ucsY,
                            BoundaryVertices = capturedBoundaryVerts,
                            BoundaryVertexCount = capturedBoundaryVerts.Count,
                            TotalEntityCount = capturedHatchIds.Count,
                            DeletedCount = 0,
                            KeptCount = totalBoundaryEntities,
                            SkippedCount = capturedHatchIds.Count - totalHatchesProcessed,
                        };

                        // 采集生成边界实体的快照（需要从模型空间获取所有 Curve）
                        var generatedEntityIds = new List<ObjectId>();
                        try
                        {
                            var allCurves = serviceTrans.GetChildObjectsFromModelspace<Curve>();
                            if (allCurves != null)
                            {
                                // 排除原有实体，仅保留新生成的边界实体
                                // 这里简化处理：采集所有 Curve 快照
                                generatedEntityIds = allCurves;
                            }
                        }
                        catch (System.Exception snapEx)
                        {
                            Logger._.Warn($"采集边界实体快照失败: {snapEx.Message}");
                        }

                        if (generatedEntityIds.Count > 0)
                        {
                            record.Entities = ServiceACAD.TestRecorder.CollectSnapshots(
                                serviceTrans, generatedEntityIds, capturedBoundaryVerts, new CropGeometryService());
                            record.TotalEntityCount = record.Entities?.Count ?? capturedHatchIds.Count;
                        }

                        var uid = ServiceACAD.TestRecorder.Record(record);
                        ed.WriteMessage($"\n[TestRecorder] UID: {uid}");

                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"ExecuteCropHatch 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"Hatch 裁剪失败: {ex.Message}");
                    }
                });

                ed.WriteMessage($"\n{'─',50}");
                ed.WriteMessage($"\n  {commandName} 汇总:");
                ed.WriteMessage($"\n{'─',50}");
                ed.WriteMessage($"\n  处理 Hatch: {totalHatchesProcessed,6}");
                ed.WriteMessage($"\n  生成边界实体: {totalBoundaryEntities,6}");
                ed.WriteMessage($"\n  跳过: {capturedHatchIds.Count - totalHatchesProcessed,6}");
                ed.WriteMessage($"\n{'─',50}");
                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   {commandName} 完成（边界已生成，后续裁剪待确认）");
                ed.WriteMessage($"\n═══════════════════════════════════════════");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPHATCH 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPHATCH 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     询问裁剪方向：保留边界内部还是外部.
        /// </summary>
        /// <returns>true 保留内部，false 保留外部，null 表示取消.</returns>
        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions("\n请选择裁剪方向 [内部(N)/外部(W)]: ", "内部 外部");
                options.Keywords.Add("内部", "内部(N)", "保留边界内部的 Hatch 边界部分");
                options.Keywords.Add("外部", "外部(W)", "保留边界外部的 Hatch 边界部分");
                options.Keywords.Default = "内部";
                options.AllowNone = true;

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                {
                    ed.WriteMessage("\n取消裁剪方向选择。");
                    return null;
                }

                if (result.StringResult == "内部")
                    return true;
                if (result.StringResult == "外部")
                    return false;

                // 默认使用"内部"
                return true;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"询问裁剪方向失败: {ex.Message}", ex);
                ed.WriteMessage($"\n询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     选择一条闭合曲线作为裁剪边界（单选）.
        /// </summary>
        /// <returns>边界顶点列表（WCS），如果取消或选择无效则返回 null.</returns>
        private List<CorePoint2D> SelectSingleBoundaryCurve(Editor ed, out ObjectId boundaryId)
        {
            try
            {
                var options = new PromptEntityOptions("\n选择裁剪边界曲线（单选）: ");
                options.SetRejectMessage("\n请选择圆、椭圆、闭合多段线或闭合样条线作为裁剪边界。");
                options.AddAllowedClass(typeof(Curve), false);

                var promptResult = ed.GetEntity(options);
                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择边界曲线或选择被取消。");
                    boundaryId = ObjectId.Null;
                    return null;
                }

                boundaryId = promptResult.ObjectId;
                var capturedId = boundaryId;
                var points = new List<CorePoint2D>();

                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var curve = serviceTrans.GetObject<Curve>(capturedId);
                    if (curve == null)
                        return;

                    if (!curve.Closed)
                    {
                        ed.WriteMessage("\n所选的边界曲线未闭合，请选择闭合曲线。");
                        return;
                    }

                    const int sampleCount = 64;
                    var startParam = curve.StartParam;
                    var endParam = curve.EndParam;

                    for (var i = 0; i < sampleCount; i++)
                    {
                        var param = startParam + (endParam - startParam) * i / sampleCount;
                        var pt = curve.GetPointAtParameter(Math.Min(param, endParam));
                        points.Add(new CorePoint2D(pt.X, pt.Y));
                    }

                    var deduped = new List<CorePoint2D>();
                    foreach (var p in points)
                    {
                        if (deduped.Count == 0)
                        {
                            deduped.Add(p);
                            continue;
                        }

                        var last = deduped[deduped.Count - 1];
                        var dx = Math.Abs(last.X - p.X);
                        var dy = Math.Abs(last.Y - p.Y);
                        if (dx > 1e-6 || dy > 1e-6)
                            deduped.Add(p);
                    }

                    points = deduped;
                });

                if (points.Count < 3)
                {
                    ed.WriteMessage("\n边界曲线顶点不足，请选择更大的闭合曲线。");
                    return null;
                }

                return points;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择边界曲线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择边界曲线失败: {ex.Message}");
                boundaryId = ObjectId.Null;
                return null;
            }
        }

        /// <summary>
        ///     选择要裁剪的 Hatch.
        /// </summary>
        private List<ObjectId> SelectHatchesToCrop(Editor ed)
        {
            try
            {
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要裁剪的 Hatch: ",
                    AllowDuplicates = false,
                };

                var promptResult = ed.GetSelection(options, new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "HATCH"),
                }));

                if (promptResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择 Hatch 或选择被取消。");
                    return null;
                }

                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in promptResult.Value)
                    ids.Add(selObj.ObjectId);

                ed.WriteMessage($"\n已选择 {ids.Count} 个 Hatch。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择待裁剪 Hatch 失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择待裁剪 Hatch 失败: {ex.Message}");
                return null;
            }
        }
    }
}
