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
    ///     先调用 GenerateHatchBoundary 生成边界实体，然后批量裁剪并重建填充.
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
                string directionLabel = capturedKeepInside ? "减掉外部-保留内部" : "减掉内部-保留外部";
                string commandName = selectAllHatches ? "CROPALLHATCHES" : "CROPHATCH";

                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   CROPHATCH 开始 — 裁剪方向: {directionLabel}");
                ed.WriteMessage($"\n═══════════════════════════════════════════");

                // ★ 调用统一的 ProcessHatches 方法
                var result = ProcessHatches(ed, hatchIds, boundaryId, capturedKeepInside);

                // ── TestRecorder 记录 ──
                try
                {
                    var record = new CropTestRecord
                    {
                        Command = commandName,
                        Direction = capturedKeepInside ? "Inside" : "Outside",
                        IsSuccess = result.IsSuccess,
                        UcsOrigin = ucsOrigin,
                        UcsXAxis = ucsX,
                        UcsYAxis = ucsY,
                        BoundaryVertices = capturedBoundaryVerts,
                        BoundaryVertexCount = capturedBoundaryVerts.Count,
                        TotalEntityCount = capturedHatchIds.Count,
                        DeletedCount = 0,
                        KeptCount = result.TotalBoundaryEntities,
                        SkippedCount = capturedHatchIds.Count - result.TotalHatchesProcessed,
                    };
                    var uid = ServiceACAD.TestRecorder.Record(record);
                    ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                }
                catch (System.Exception recEx)
                {
                    Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                }

                ed.WriteMessage($"\n{'─',50}");
                ed.WriteMessage($"\n  {commandName} 汇总:");
                ed.WriteMessage($"\n{'─',50}");
                ed.WriteMessage($"\n  处理 Hatch: {result.TotalHatchesProcessed,6}");
                ed.WriteMessage($"\n  生成边界实体: {result.TotalBoundaryEntities,6}");
                ed.WriteMessage($"\n  新填充: {result.NewHatchesCreated,6}");
                ed.WriteMessage($"\n  跳过: {capturedHatchIds.Count - result.TotalHatchesProcessed,6}");
                ed.WriteMessage($"\n{'─',50}");
                ed.WriteMessage($"\n═══════════════════════════════════════════");
                ed.WriteMessage($"\n   {commandName} 完成");
                ed.WriteMessage($"\n═══════════════════════════════════════════");
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPHATCH 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPHATCH 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     Hatch 裁剪处理结果.
        /// </summary>
        public sealed class ProcessHatchesResult
        {
            /// <summary>操作是否成功.</summary>
            public bool IsSuccess { get; set; }
            /// <summary>成功处理的 Hatch 数量.</summary>
            public int TotalHatchesProcessed { get; set; }
            /// <summary>生成的边界实体总数.</summary>
            public int TotalBoundaryEntities { get; set; }
            /// <summary>新创建的 Hatch 数量.</summary>
            public int NewHatchesCreated { get; set; }
        }

        /// <summary>
        ///     批量处理 Hatch 裁剪（GenerateHatchBoundary → CropClosedCurveMulti → CloneHatch）.
        ///     此方法可被 CROPINSIDE/CROPOUTSIDE 等命令直接调用.
        /// </summary>
        /// <param name="ed">编辑器（用于输出日志）.</param>
        /// <param name="hatchIds">待裁剪的 Hatch ObjectId 列表.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="keepInside">true=保留内部(CROPOUTSIDE)，false=保留外部(CROPINSIDE).</param>
        /// <returns>处理结果.</returns>
        public static ProcessHatchesResult ProcessHatches(
            Editor ed, IReadOnlyList<ObjectId> hatchIds, ObjectId boundaryId, bool keepInside)
        {
            var result = new ProcessHatchesResult();
            try
            {
                if (hatchIds == null || hatchIds.Count == 0)
                {
                    result.IsSuccess = true;
                    return result;
                }
                if (boundaryId.IsNull || boundaryId.IsErased)
                {
                    result.IsSuccess = false;
                    return result;
                }

                int totalHatchesProcessed = 0;
                int totalBoundaryEntities = 0;
                var allGeneratedIds = new List<ObjectId>();
                var clippedCurveIds = new List<ObjectId>();

                // ★ 第一步+第二步：逐 Hatch 生成边界并裁剪.
                //    外环+单内环(2环)且非 Ignore 样式 → 用 CROPTWOCLOSEDCURVE(CropRingWithHole)
                //    正确处理裁剪边界同时与外环、内环相交的凹字形场景（内环孔洞区域始终不属于结果）；
                //    Ignore 样式忽略所有内环，只用外环裁剪；
                //    其余环结构（单环 / 3 环以上）沿用 CROPCLOSEDCURVE(CropClosedCurveMulti) 逐环独立裁剪.
                foreach (var hatchId in hatchIds)
                {
                    if (!hatchId.IsValid || hatchId.IsErased)
                        continue;

                    var genResult = HatchBoundaryService.GenerateHatchBoundary(hatchId);
                    if (!genResult.IsSuccess)
                    {
                        ed.WriteMessage($"\n  Hatch {hatchId}: 边界生成失败 — {genResult.Message}");
                        continue;
                    }

                    totalBoundaryEntities += genResult.EntityCount;
                    totalHatchesProcessed++;
                    allGeneratedIds.AddRange(genResult.GeneratedEntityIds);
                    ed.WriteMessage($"\n  Hatch {hatchId}: 生成 {genResult.EntityCount} 个边界实体 [{genResult.TypeLog}]");

                    var hatchStyle = GetHatchStyle(hatchId);
                    var thisHatchClipped = CropSingleHatchBoundary(genResult, boundaryId, hatchStyle, keepInside, ed);
                    clippedCurveIds.AddRange(thisHatchClipped);
                }

                ed.WriteMessage($"\n  共生成 {allGeneratedIds.Count} 条边界曲线，裁剪后得到 {clippedCurveIds.Count} 条曲线，准备用源 Hatch 参数填充...");

                // ★ 第四步：统一环有效性 + clipDepth 逻辑
                //    1. 计算原始环面积 → 确定 clipDepth（裁剪边界对应的原始环深度）
                //    2. Outer 样式: clipDepth >= 1 → 删除 Hatch（裁剪区域在孔洞内，无填充）
                //    3. Normal 样式: 过滤容器曲线（与裁剪边界同形的曲线）
                //    4. Ignore 样式: 取面积最大的曲线作为外环
                List<ObjectId> sortedCurveIds = new List<ObjectId>();
                HatchStyle srcStyle = HatchStyle.Normal;
                if (clippedCurveIds.Count > 0)
                {
                    CadServiceManager._.ExecuteInTransactions(null, ts =>
                    {
                        // 获取源 Hatch 的 HatchStyle
                        srcStyle = HatchStyle.Normal;
                        if (hatchIds.Count > 0 && hatchIds[0].IsValid && !hatchIds[0].IsErased)
                        {
                            var srcHatch = ts.GetObject<Hatch>(hatchIds[0], OpenMode.ForRead);
                            if (srcHatch != null) srcStyle = srcHatch.HatchStyle;
                        }

                        // 计算裁剪边界的面积
                        double clipArea = 0;
                        if (boundaryId.IsValid && !boundaryId.IsErased)
                        {
                            var clipCurve = ts.GetObject<Curve>(boundaryId, OpenMode.ForRead);
                            if (clipCurve is Polyline clipPl)
                                clipArea = clipPl.Area;
                            else if (clipCurve is Circle clipCirc)
                                clipArea = clipCirc.Area;
                            else if (clipCurve is Ellipse clipEll)
                                clipArea = clipEll.Area;
                        }

                        // 计算原始边界实体的面积，按面积降序排序
                        // 面积最大 = 外环(depth 0)，次大 = 内环(depth 1)，以此类推
                        var origAreas = new List<double>();
                        foreach (var id in allGeneratedIds)
                        {
                            if (!id.IsValid || id.IsErased) continue;
                            var ent = ts.GetObject<Entity>(id, OpenMode.ForRead);
                            if (ent is Polyline pl) origAreas.Add(pl.Area);
                            else if (ent is Circle cir) origAreas.Add(cir.Area);
                            else if (ent is Ellipse ell) origAreas.Add(ell.Area);
                        }
                        origAreas.Sort((a, b) => b.CompareTo(a));

                        // 确定 clipDepth：裁剪边界面积匹配哪个原始环
                        // 面积差 < 1% 视为匹配
                        int clipDepth = 0;
                        if (clipArea > 0)
                        {
                            for (int i = 0; i < origAreas.Count; i++)
                            {
                                double ratio = Math.Abs(origAreas[i] - clipArea) / clipArea;
                                if (ratio < 0.01)
                                {
                                    clipDepth = i; // 0=外环, 1=内环, 2+=无效环
                                    break;
                                }
                            }
                        }

                        // ★ Outer 样式：clipDepth >= 1 → 删除 Hatch
                        //    裁剪边界是内环或无效环 → 裁剪区域在孔洞内 → 无填充
                        if (srcStyle == HatchStyle.Outer && clipDepth >= 1)
                        {
                            ed.WriteMessage($"\n  Outer 样式：裁剪边界是内环或无效环(depth={clipDepth})，删除 Hatch");
                            sortedCurveIds = new List<ObjectId>();
                            return;
                        }

                        // 使用包含关系层次排序
                        sortedCurveIds = SortByContainmentHierarchy(
                            clippedCurveIds, srcStyle, ts, clipArea);
                    });
                }

                ed.WriteMessage($"\n  按包含关系排序后取 {sortedCurveIds.Count} 条曲线用于重建 Hatch...");

                // ★ 第四步半：对 OUTER/NORMAL 样式，用偶数环（填充）裁剪奇数环（孔洞），
                //    防止孔洞环局部超出父填充环导致 Hatch 显示异常.
                //    sortedCurveIds 已按 depth 升序排列（depth 0=外环填充, 1=内环孔洞, 2=填充...）.
                if ((srcStyle == HatchStyle.Outer || srcStyle == HatchStyle.Normal)
                    && sortedCurveIds.Count >= 2)
                {
                    var clippedOddIds = new List<ObjectId>();
                    for (int idx = 1; idx < sortedCurveIds.Count; idx += 2)
                    {
                        // idx 为奇数环（孔洞），idx-1 为其父偶数环（填充）
                        var parentId = sortedCurveIds[idx - 1];
                        var holeId = sortedCurveIds[idx];
                        if (parentId.IsNull || holeId.IsNull) continue;

                        var cropResult = CropClosedCurveService.CropClosedCurveMulti(
                            new List<ObjectId> { holeId }, parentId, keepInside: true);
                        if (cropResult.IsSuccess && cropResult.CreatedEntityIds != null
                            && cropResult.CreatedEntityIds.Count > 0)
                        {
                            sortedCurveIds[idx] = cropResult.CreatedEntityIds[0];
                            clippedOddIds.AddRange(cropResult.CreatedEntityIds);
                            Logger._.Info(
                                $"[OddEvenOverlap] 偶数环[{idx - 1}]裁剪奇数环[{idx}]: " +
                                $"{cropResult.CreatedEntityIds.Count} 个结果");
                        }
                    }
                    // 将裁剪产生的新实体加入 clippedCurveIds 以便后续清理
                    clippedCurveIds.AddRange(clippedOddIds);
                    ed.WriteMessage($"\n  奇偶环重叠裁剪完成，修补 {clippedOddIds.Count} 条曲线");
                }

                // ★ 第五步：对每个源 Hatch 用 CloneHatchWithNewBoundaries 创建新 Hatch，清理中间产物
                int newHatchesCreated = 0;
                if (sortedCurveIds.Count > 0)
                {
                    CadServiceManager._.ExecuteInCommandTransaction(ts =>
                    {
                        try
                        {
                            foreach (var srcHatchId in hatchIds)
                            {
                                if (!srcHatchId.IsValid || srcHatchId.IsErased)
                                    continue;

                                var extractResult = CloneHatchCommand.ExtractHatchParams(srcHatchId);
                                if (!extractResult.IsSuccess)
                                {
                                    ed.WriteMessage($"\n  Hatch {srcHatchId}: 提取参数失败 — {extractResult.Message}");
                                    continue;
                                }

                                ObjectId newHatchId = ObjectId.Null;
                                var created = CloneHatchCommand.CloneHatchWithNewBoundaries(
                                    ts, extractResult.Data,
                                    sortedCurveIds.ToArray(), out newHatchId);

                                if (created && !newHatchId.IsNull)
                                {
                                    newHatchesCreated++;
                                    ed.WriteMessage($"\n  Hatch {srcHatchId}: 新填充已创建 ({newHatchId})");
                                }
                                else
                                {
                                    ed.WriteMessage($"\n  Hatch {srcHatchId}: 创建新填充失败");
                                }
                            }

                            // ★ 第六步：清理中间产物（临时注释 allGeneratedIds + clippedCurveIds 用于调试）
                            // foreach (var id in allGeneratedIds)
                            // {
                            //     if (!id.IsValid || id.IsErased) continue;
                            //     try
                            //     {
                            //         var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                            //         if (ent != null && !ent.IsErased) ent.Erase();
                            //     }
                            //     catch { }
                            // }
                            // foreach (var id in clippedCurveIds)
                            // {
                            //     if (!id.IsValid || id.IsErased) continue;
                            //     try
                            //     {
                            //         var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                            //         if (ent != null && !ent.IsErased) ent.Erase();
                            //     }
                            //     catch { }
                            // }
                            // DEBUG: 仅删除源 Hatch，保留临时边界实体用于调试
                            foreach (var id in hatchIds)
                            {
                                if (!id.IsValid || id.IsErased) continue;
                                try
                                {
                                    var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                                    if (ent != null && !ent.IsErased) ent.Erase();
                                }
                                catch { }
                            }

                            return ServiceACAD.OpResult.Success();
                        }
                        catch (System.Exception ex)
                        {
                            Logger._.Error($"CloneHatch 填充失败: {ex.Message}", ex);
                            return ServiceACAD.OpResult.Fail(ex.Message);
                        }
                    });
                }
                else
                {
                    // 无裁剪结果：删除原始 Hatch 和中间边界实体（全在裁剪侧）
                    CadServiceManager._.ExecuteInCommandTransaction(ts =>
                    {
                        try
                        {
                            foreach (var id in hatchIds)
                            {
                                if (!id.IsValid || id.IsErased) continue;
                                try
                                {
                                    var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                                    if (ent != null && !ent.IsErased) ent.Erase();
                                }
                                catch { }
                            }
                            // DEBUG: 保留临时边界实体用于调试
                            // foreach (var id in allGeneratedIds)
                            // {
                            //     if (!id.IsValid || id.IsErased) continue;
                            //     try
                            //     {
                            //         var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                            //         if (ent != null && !ent.IsErased) ent.Erase();
                            //     }
                            //     catch { }
                            // }
                            return ServiceACAD.OpResult.Success();
                        }
                        catch (System.Exception ex)
                        {
                            Logger._.Error($"清理无结果 Hatch 失败: {ex.Message}", ex);
                            return ServiceACAD.OpResult.Fail(ex.Message);
                        }
                    });
                }

                result.IsSuccess = true;
                result.TotalHatchesProcessed = totalHatchesProcessed;
                result.TotalBoundaryEntities = totalBoundaryEntities;
                result.NewHatchesCreated = newHatchesCreated;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"ProcessHatches 失败: {ex.Message}", ex);
                result.IsSuccess = false;
            }
            return result;
        }

        /// <summary>
        ///     裁剪单个 Hatch 生成的边界环集合.
        ///     <para>
        ///         IGNORE 样式：忽略所有内环，只裁剪面积最大的外环（<see cref="CropClosedCurveService.CropClosedCurveMulti"/>
        ///         单曲线裁剪），内环孔洞完全不参与运算.
        ///     </para>
        ///     <para>
        ///         OUTER 样式：填充外环直到第一层内环即止，depth≥2 的岛在
        ///         <see cref="SortByContainmentHierarchy"/> 中必被过滤，故委托 <see cref="CropOuterStyleBoundary"/>
        ///         只处理外环(depth0)+直接内环(depth1)，忽略更深的岛，
        ///         并对外环+单内环使用 CROPTWOCLOSEDCURVE(<see cref="CropClosedCurveService.CropRingWithHole"/>)
        ///         正确处理裁剪边界同时与外环、内环相交的凹字形场景.
        ///     </para>
        ///     <para>
        ///         NORMAL 样式恰好 2 环（外环+单内环）：同样使用 CROPTWOCLOSEDCURVE，
        ///         内环孔洞区域始终不属于结果，无论裁剪方向如何，避免外环、内环各自独立裁剪
        ///         （CROPCLOSEDCURVE）时因共边未对齐产生的错误结果.
        ///     </para>
        ///     <para>
        ///         单环 / NORMAL 3 环以上（多层嵌套）：沿用
        ///         <see cref="CropClosedCurveService.CropClosedCurveMulti"/> 逐环独立裁剪（不变行为）.
        ///     </para>
        /// </summary>
        /// <param name="genResult">GenerateHatchBoundary 生成的边界结果（含环面积、环深度）.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="hatchStyle">源 Hatch 的 HatchStyle.</param>
        /// <param name="keepInside">true=保留内部，false=保留外部.</param>
        /// <param name="ed">编辑器（用于输出日志）.</param>
        /// <returns>裁剪后新生成的曲线 ObjectId 列表.</returns>
        private static List<ObjectId> CropSingleHatchBoundary(
            HatchBoundaryService.GenerateHatchBoundaryResult genResult,
            ObjectId boundaryId, HatchStyle hatchStyle, bool keepInside, Editor ed)
        {
            try
            {
                var ids = genResult.GeneratedEntityIds;
                if (ids == null || ids.Count == 0)
                    return new List<ObjectId>();

                // ── IGNORE 样式：忽略所有内环，只裁剪外环 ──
                if (hatchStyle == HatchStyle.Ignore)
                {
                    int outerIdx = IndexOfMaxArea(genResult.LoopAreas);
                    return ClipSingleRing(ids[outerIdx], boundaryId, keepInside, ed, "IGNORE 仅外环");
                }

                // ── OUTER 样式：只处理外环(depth0)+直接内环(depth1)，忽略更深的岛 ──
                if (hatchStyle == HatchStyle.Outer)
                    return CropOuterStyleBoundary(genResult, boundaryId, keepInside, ed);

                // ── NORMAL 且恰好 2 环（外环+单内环）：CROPTWOCLOSEDCURVE 处理凹字形重叠 ──
                if (ids.Count == 2)
                {
                    int outerIdx = IndexOfMaxArea(genResult.LoopAreas);
                    int holeIdx = outerIdx == 0 ? 1 : 0;
                    var ringIds = CropTwoRings(ids[outerIdx], ids[holeIdx], boundaryId, keepInside, ed, "NORMAL");
                    // CROPTWOCLOSEDCURVE 成功且有结果 → 直接返回；
                    // 返回空结果（非 null 但 Count==0）或曲线转换失败（null）→ 回退到逐环裁剪
                    if (ringIds != null && ringIds.Count > 0)
                        return ringIds;
                    if (ringIds != null && ringIds.Count == 0)
                        ed.WriteMessage($"\n  NORMAL CROPTWOCLOSEDCURVE 返回空结果，回退到 CROPCLOSEDCURVE 逐环裁剪。");
                }

                // ── 默认（单环 / NORMAL 3+ 环 / CROPTWOCLOSEDCURVE 失败回退）：逐环独立裁剪 ──
                return ClipMultipleRings(ids, boundaryId, keepInside, ed);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropSingleHatchBoundary 失败: {ex.Message}", ex);
                return new List<ObjectId>();
            }
        }

        /// <summary>
        ///     裁剪 OUTER 样式 Hatch 的边界环集合.
        ///     <para>
        ///         OUTER 语义：填充最外层，遇到第一层内环即停止填充。
        ///         depth≥2 的岛在 <see cref="SortByContainmentHierarchy"/> 中始终被过滤（保留 depth≤1），
        ///         因此这里只需正确处理 depth0(外环)+depth1(直接内环)，更深的岛直接忽略，
        ///         不参与裁剪运算（省去无谓且可能出错的计算）.
        ///     </para>
        ///     <para>
        ///         外环+单个直接内环：使用 CROPTWOCLOSEDCURVE（<see cref="CropClosedCurveService.CropRingWithHole"/>）
        ///         正确处理裁剪边界同时与外环、内环相交的凹字形场景.
        ///     </para>
        ///     <para>
        ///         外环无内环，或存在多个并列直接内环（siblings）：分别回退到单环裁剪或
        ///         <see cref="CropClosedCurveService.CropClosedCurveMulti"/> 逐环独立裁剪.
        ///     </para>
        /// </summary>
        /// <param name="genResult">GenerateHatchBoundary 生成的边界结果（含环深度）.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="keepInside">true=保留内部，false=保留外部.</param>
        /// <param name="ed">编辑器（用于输出日志）.</param>
        /// <returns>裁剪后新生成的曲线 ObjectId 列表.</returns>
        /// <summary>
        ///     裁剪 OUTER 样式 Hatch 的边界环集合.
        ///     <para>
        ///         不依赖 <see cref="HatchBoundaryService.GenerateHatchBoundaryResult.LoopDepths"/>
        ///         （其底层 <c>IsEntityInsideAnother</c> 仅处理 Polyline，遇到 Circle/Ellipse 会
        ///         静默失败导致所有环 depth=0），改用面积排序确定外环/内环：
        ///         面积最大=外环，面积次大=第一层内环（孔洞），其余更深的岛在后续
        ///         <see cref="SortByContainmentHierarchy"/> 中按 OUTER 规则自然过滤.
        ///     </para>
        ///     <para>
        ///         外环+第一层内环使用 CROPTWOCLOSEDCURVE（<see cref="CropClosedCurveService.CropRingWithHole"/>）
        ///         正确处理裁剪边界同时与外环、内环相交的凹字形场景.
        ///     </para>
        /// </summary>
        private static List<ObjectId> CropOuterStyleBoundary(
            HatchBoundaryService.GenerateHatchBoundaryResult genResult,
            ObjectId boundaryId, bool keepInside, Editor ed)
        {
            var ids = genResult.GeneratedEntityIds;
            var areas = genResult.LoopAreas;

            if (ids == null || ids.Count == 0)
                return new List<ObjectId>();

            if (ids.Count == 1)
                return ClipSingleRing(ids[0], boundaryId, keepInside, ed, "OUTER 单环");

            // 按面积降序排列索引：面积最大=外环，次大=第一层内环.
            var sortedIndices = new List<int>(ids.Count);
            for (int i = 0; i < ids.Count; i++) sortedIndices.Add(i);
            sortedIndices.Sort((a, b) =>
            {
                double areaA = areas != null && a < areas.Length ? areas[a] : 0;
                double areaB = areas != null && b < areas.Length ? areas[b] : 0;
                return areaB.CompareTo(areaA);
            });

            int outerIdx = sortedIndices[0];
            int holeIdx = sortedIndices[1];

            ed.WriteMessage(
                $"\n  OUTER 面积排序: outer[{outerIdx}] A={GetArea(areas, outerIdx):F2}, hole[{holeIdx}] A={GetArea(areas, holeIdx):F2}");

            var ringIds = CropTwoRings(ids[outerIdx], ids[holeIdx], boundaryId, keepInside, ed, "OUTER");
            // CROPTWOCLOSEDCURVE 成功且有结果 → 直接返回
            if (ringIds != null && ringIds.Count > 0)
                return ringIds;

            // CROPTWOCLOSEDCURVE 返回空结果（ringIds 非 null 但 Count==0）或
            // 曲线转换失败（ringIds==null）：回退到逐环独立裁剪，
            // 确保第 3+ 个环（如 depth≥2 的岛）也能被处理，而非被静默忽略
            if (ringIds != null && ringIds.Count == 0)
                ed.WriteMessage($"\n  OUTER CROPTWOCLOSEDCURVE 返回空结果，回退到 CROPCLOSEDCURVE 逐环裁剪。");
            return ClipMultipleRings(ids, boundaryId, keepInside, ed);
        }

        private static double GetArea(double[] areas, int idx)
        {
            if (areas == null || idx < 0 || idx >= areas.Length) return 0;
            return areas[idx];
        }

        /// <summary>
        ///     使用 CROPTWOCLOSEDCURVE（<see cref="CropClosedCurveService.CropRingWithHole"/>）
        ///     裁剪一个外环+一个内环（孔洞），正确处理裁剪边界同时与外环、内环相交的凹字形场景.
        ///     <para>
        ///         只要三条曲线均成功转换为 <c>CurveSelection</c>（即算法能够执行），
        ///         就直接采用 CropRingWithHole 的返回结果 —— 即使结果为空环（0 个封闭环）
        ///         也是权威且正确的答案（例如裁剪边界恰好完全落在孔洞内部时，
        ///         外环∩Clip\内环 本应为空，Hatch 在该区域确实无填充）。
        ///         绝不能因为"无结果"而回退到 <see cref="CropClosedCurveService.CropClosedCurveMulti"/>，
        ///         后者把内环当作独立 Subject 与 Clip 求交，交集会被当成新增填充环
        ///         （本应是孔洞挖空区域），导致填充区域反转、环结构错乱.
        ///         只有当曲线转换本身失败（如不支持的曲线类型）时，才返回 null 交由调用方回退.
        ///     </para>
        /// </summary>
        /// <param name="outerId">外环曲线 ObjectId.</param>
        /// <param name="holeId">内环（孔洞）曲线 ObjectId.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="keepInside">true=保留内部，false=保留外部.</param>
        /// <param name="ed">编辑器（用于输出日志）.</param>
        /// <param name="styleLabel">用于日志标注的样式标签.</param>
        /// <returns>裁剪后新生成的曲线 ObjectId 列表（可能为空列表，代表该区域无填充）；曲线转换失败时返回 null（供调用方回退）.</returns>
        private static List<ObjectId> CropTwoRings(
            ObjectId outerId, ObjectId holeId, ObjectId boundaryId, bool keepInside,
            Editor ed, string styleLabel)
        {
            try
            {
                var outerSel = CropClosedCurveService.CreateCurveSelection(outerId);
                var holeSel = CropClosedCurveService.CreateCurveSelection(holeId);
                var clipSel = CropClosedCurveService.CreateCurveSelection(boundaryId);
                if (outerSel == null || holeSel == null || clipSel == null)
                {
                    ed.WriteMessage($"\n  CROPTWOCLOSEDCURVE({styleLabel}) 曲线转换失败，回退到 CROPCLOSEDCURVE。");
                    return null;
                }

                var ringResult = CropClosedCurveService.CropRingWithHole(outerSel, holeSel, clipSel, keepInside);

                // ★ CropRingWithHole 已成功执行（无论结果是否为空环），结果即为权威答案，
                //   不回退到会产生错误语义的 CropClosedCurveMulti。
                ed.WriteMessage($"\n  CROPTWOCLOSEDCURVE 裁剪完成({styleLabel}): {ringResult.Message}");
                return ringResult.CreatedEntityIds ?? new List<ObjectId>();
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CropTwoRings 失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        ///     使用 CROPCLOSEDCURVE（<see cref="CropClosedCurveService.CropClosedCurveMulti"/>）裁剪单条曲线.
        /// </summary>
        /// <param name="id">待裁剪曲线 ObjectId.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="keepInside">true=保留内部，false=保留外部.</param>
        /// <param name="ed">编辑器（用于输出日志）.</param>
        /// <param name="label">用于日志标注的场景标签.</param>
        /// <returns>裁剪后新生成的曲线 ObjectId 列表.</returns>
        private static List<ObjectId> ClipSingleRing(
            ObjectId id, ObjectId boundaryId, bool keepInside, Editor ed, string label)
        {
            var result = CropClosedCurveService.CropClosedCurveMulti(
                new List<ObjectId> { id }, boundaryId, keepInside);
            ed.WriteMessage(result.IsSuccess
                ? $"\n  CROPCLOSEDCURVE 裁剪完成({label}): {result.Message}"
                : $"\n  CROPCLOSEDCURVE 裁剪({label}): {result.Message}");
            return result.CreatedEntityIds ?? new List<ObjectId>();
        }

        /// <summary>
        ///     使用 CROPCLOSEDCURVE（<see cref="CropClosedCurveService.CropClosedCurveMulti"/>）逐环独立裁剪多条曲线.
        /// </summary>
        /// <param name="ids">待裁剪曲线 ObjectId 列表.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="keepInside">true=保留内部，false=保留外部.</param>
        /// <param name="ed">编辑器（用于输出日志）.</param>
        /// <returns>裁剪后新生成的曲线 ObjectId 列表.</returns>
        private static List<ObjectId> ClipMultipleRings(
            List<ObjectId> ids, ObjectId boundaryId, bool keepInside, Editor ed)
        {
            var result = CropClosedCurveService.CropClosedCurveMulti(ids, boundaryId, keepInside);
            ed.WriteMessage(result.IsSuccess
                ? $"\n  CROPCLOSEDCURVE 裁剪完成: {result.Message}"
                : $"\n  CROPCLOSEDCURVE 裁剪: {result.Message}");
            return result.CreatedEntityIds ?? new List<ObjectId>();
        }

        /// <summary>
        ///     读取指定 Hatch 的 HatchStyle.
        /// </summary>
        /// <param name="hatchId">Hatch 的 ObjectId.</param>
        /// <returns>HatchStyle；读取失败时返回 <see cref="HatchStyle.Normal"/>.</returns>
        private static HatchStyle GetHatchStyle(ObjectId hatchId)
        {
            var style = HatchStyle.Normal;
            try
            {
                if (!hatchId.IsValid || hatchId.IsErased)
                    return style;

                CadServiceManager._.ExecuteInTransactions(null, ts =>
                {
                    var hatch = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                    if (hatch != null) style = hatch.HatchStyle;
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"GetHatchStyle 失败: {ex.Message}", ex);
            }
            return style;
        }

        /// <summary>
        ///     返回面积数组中最大值的索引（用于确定外环）.
        /// </summary>
        /// <param name="areas">环面积数组.</param>
        /// <returns>最大面积对应的索引；数组为空时返回 0.</returns>
        private static int IndexOfMaxArea(double[] areas)
        {
            if (areas == null || areas.Length == 0)
                return 0;
            int maxIdx = 0;
            for (int i = 1; i < areas.Length; i++)
            {
                if (areas[i] > areas[maxIdx])
                    maxIdx = i;
            }
            return maxIdx;
        }

        /// <summary>
        ///     询问裁剪方向：保留边界内部还是外部.
        /// </summary>
        /// <returns>true=裁剪外部（保留内部），false=裁剪内部（保留外部），null=取消.</returns>
        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions(
                    "\n请选择裁剪方向 [裁剪内部-保留外部(I)/裁剪外部-保留内部(O)]: ", "裁剪内部 裁剪外部");
                options.Keywords.Add("裁剪内部", "裁剪内部-保留外部(I)", "裁剪掉边界内部的实体，保留外部");
                options.Keywords.Add("裁剪外部", "裁剪外部-保留内部(O)", "裁剪掉边界外部的实体，保留内部");
                options.Keywords.Default = "裁剪外部";
                options.AllowNone = true;

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                {
                    ed.WriteMessage("\n取消裁剪方向选择。");
                    return null;
                }

                // 裁剪内部 = 保留外部 = keepInside = false
                // 裁剪外部 = 保留内部 = keepInside = true
                if (result.StringResult == "裁剪内部")
                    return false;
                if (result.StringResult == "裁剪外部")
                    return true;

                // 默认 = 裁剪外部（保留内部）
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

        /// <summary>
        ///     使用射线法判断点是否在多边形内部（WCS 2D 投影）.
        ///     水平向右射线，与多边形边交点为奇数则在内部.
        /// </summary>
        /// <param name="point">测试点（WCS）.</param>
        /// <param name="polyline">闭合多段线.</param>
        /// <returns>true=点在多边形内部，false=在外部或边界上.</returns>
        private static bool IsPointInsidePolygon(Point3d point, Polyline polyline)
        {
            try
            {
                if (polyline == null || !polyline.Closed) return false;

                int n = polyline.NumberOfVertices;
                if (n < 3) return false;

                bool inside = false;
                double px = point.X, py = point.Y;

                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    var p1 = polyline.GetPoint3dAt(i);
                    var p2 = polyline.GetPoint3dAt(j);

                    // 水平向右射线法：判断射线与边的交点
                    if ((p1.Y > py) != (p2.Y > py) &&
                        px < (p2.X - p1.X) * (py - p1.Y) / (p2.Y - p1.Y) + p1.X)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"IsPointInsidePolygon 失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        ///     使用包含关系层次排序裁剪后的曲线列表.
        ///     构建包含树：depth = 被包含次数（被多少个其他环包含）.
        ///     按 depth 升序 + 面积降序排列，再按 HatchStyle 过滤.
        /// </summary>
        /// <param name="curveIds">裁剪后的 Polyline ObjectId 列表.</param>
        /// <param name="style">源 Hatch 的 HatchStyle.</param>
        /// <param name="ts">事务服务.</param>
        /// <param name="clipArea">裁剪边界的面积（用于 Normal 样式过滤"容器"曲线）.</param>
        /// <returns>排序后的 ObjectId 列表（depth 0 在前 = 最外环）.</returns>
        private static List<ObjectId> SortByContainmentHierarchy(
            List<ObjectId> curveIds, HatchStyle style, ITransactionService ts,
            double clipArea = 0)
        {
            try
            {
                if (curveIds == null || curveIds.Count <= 1)
                    return curveIds ?? new List<ObjectId>();

                int n = curveIds.Count;
                var areas = new double[n];
                var plineCache = new Polyline[n];
                var depth = new int[n];

                // Step 1: 读取所有 Polyline，计算面积
                for (int i = 0; i < n; i++)
                {
                    if (!curveIds[i].IsValid || curveIds[i].IsErased) continue;
                    var pline = ts.GetObject<Polyline>(curveIds[i], OpenMode.ForRead);
                    if (pline == null) continue;
                    plineCache[i] = pline;
                    areas[i] = pline.Area;
                }

                // Step 2: 构建包含矩阵，计算 depth = 被包含次数
                //    HatchStyle.Ignore 特殊处理：只需取面积最大的曲线（外环），
                //    不需要包含关系检测。Ignore 语义 = 只填充最外环，忽略所有内环.
                if (style == HatchStyle.Ignore)
                {
                    // 按面积降序排序，取第 1 个（最大的 = 外环）
                    var areaSorted = new List<(int Index, double Area)>();
                    for (int i = 0; i < n; i++)
                    {
                        if (plineCache[i] == null) continue;
                        areaSorted.Add((i, areas[i]));
                    }
                    areaSorted.Sort((a, b) => b.Area.CompareTo(a.Area));

                    var ignoreResult = new List<ObjectId>();
                    if (areaSorted.Count > 0)
                        ignoreResult.Add(curveIds[areaSorted[0].Index]);
                    return ignoreResult;
                }

                // Outer / Normal: 使用包含关系层次排序
                //    同形检测：面积近似相等（差 < 1e-8）则视为 siblings，不建立包含关系
                //    使用多顶点投票法：测试多个顶点是否在另一个多边形内部，
                //    多数在内则判定为包含.
                const double areaTol = 1e-8;
                for (int i = 0; i < n; i++)
                {
                    if (plineCache[i] == null) continue;

                    for (int j = 0; j < n; j++)
                    {
                        if (i == j || plineCache[j] == null) continue;

                        // 同形检测：面积近似相等则视为 siblings
                        if (Math.Abs(areas[i] - areas[j]) < areaTol) continue;

                        // 多顶点投票法：测试最多 5 个顶点
                        int insideCount = 0;
                        int testCount = 0;
                        int maxTests = Math.Min(5, plineCache[i].NumberOfVertices);
                        for (int v = 0; v < maxTests; v++)
                        {
                            var pt = plineCache[i].GetPoint3dAt(v);
                            if (IsPointInsidePolygon(pt, plineCache[j]))
                                insideCount++;
                            testCount++;
                        }
                        // 多数顶点在 j 内部 → i 被 j 包含
                        if (testCount > 0 && insideCount > testCount / 2)
                            depth[i]++;
                    }
                }

                // ★ 调试日志：输出每条曲线的 depth 和面积
                for (int i = 0; i < n; i++)
                {
                    if (plineCache[i] == null) continue;
                    Logger._.Info($"[ContainmentSort] 曲线[{i}]: Area={areas[i]:F4}, Depth={depth[i]}, Style={style}");
                }

                // Step 3: 按 HatchStyle 过滤 + 去重
                //    Ignore: 只保留 depth == 0
                //    Outer: 保留 depth <= 1
                //    Normal: 保留所有 depth
                //    去重：面积近似相等的曲线只保留第一条（避免同形曲线相互抵消）
                var filtered = new List<(int Index, int Depth, double Area)>();
                var seenAreas = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    if (plineCache[i] == null) continue;
                    if (style == HatchStyle.Ignore && depth[i] > 0)
                    {
                        Logger._.Info($"[ContainmentSort] 曲线[{i}] 被 Ignore 过滤: depth={depth[i]} > 0");
                        continue;
                    }
                    if (style == HatchStyle.Outer && depth[i] > 1)
                    {
                        Logger._.Info($"[ContainmentSort] 曲线[{i}] 被 Outer 过滤: depth={depth[i]} > 1");
                        continue;
                    }
                    // 去重：检查是否已有近似面积的曲线
                    bool isDuplicate = false;
                    foreach (var seenArea in seenAreas)
                    {
                        if (Math.Abs(areas[i] - seenArea) < areaTol)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (isDuplicate)
                    {
                        Logger._.Info($"[ContainmentSort] 曲线[{i}] 去重跳过: Area={areas[i]:F4} 已存在");
                        continue;
                    }
                    filtered.Add((i, depth[i], areas[i]));
                    seenAreas.Add(areas[i]);
                }

                // Step 3b: Normal 样式 — 过滤与裁剪边界同形的"容器"曲线
                //    当裁剪边界是 Hatch 的内环时，外环裁剪后与裁剪边界同形（"容器"），
                //    不应作为外环。应保留面积不同的"内容"曲线作为外环.
                //    仅当存在与裁剪边界面积不同的曲线时才过滤.
                if (style == HatchStyle.Normal && clipArea > 0 && filtered.Count > 1)
                {
                    bool hasNonClipCurves = false;
                    foreach (var item in filtered)
                    {
                        if (Math.Abs(item.Area - clipArea) >= areaTol)
                        {
                            hasNonClipCurves = true;
                            break;
                        }
                    }

                    if (hasNonClipCurves)
                    {
                        int removedCount = filtered.RemoveAll(
                            item => Math.Abs(item.Area - clipArea) < areaTol);
                        if (removedCount > 0)
                            Logger._.Info($"[ContainmentSort] Normal 过滤容器曲线: 移除 {removedCount} 条 (clipArea={clipArea:F4})");
                    }
                }

                // Step 3c: 移除容器曲线后，重新计算剩余曲线的 depth
                //    容器曲线被移除后，剩余曲线的 depth 需要基于剩余曲线重新计算
                if (filtered.Count > 1)
                {
                    for (int a = 0; a < filtered.Count; a++)
                    {
                        int newDepth = 0;
                        var plineA = plineCache[filtered[a].Index];
                        if (plineA == null) continue;

                        for (int b = 0; b < filtered.Count; b++)
                        {
                            if (a == b) continue;
                            var plineB = plineCache[filtered[b].Index];
                            if (plineB == null) continue;
                            if (Math.Abs(filtered[a].Area - filtered[b].Area) < areaTol) continue;

                            int insideCount = 0;
                            int testCount = 0;
                            int maxTests = Math.Min(5, plineA.NumberOfVertices);
                            for (int v = 0; v < maxTests; v++)
                            {
                                var pt = plineA.GetPoint3dAt(v);
                                if (IsPointInsidePolygon(pt, plineB))
                                    insideCount++;
                                testCount++;
                            }
                            if (testCount > 0 && insideCount > testCount / 2)
                                newDepth++;
                        }
                        filtered[a] = (filtered[a].Index, newDepth, filtered[a].Area);
                    }
                }

                Logger._.Info($"[ContainmentSort] 过滤后剩余 {filtered.Count} 条曲线 (Style={style})");

                if (filtered.Count == 0)
                    return new List<ObjectId>();

                // Step 4: 排序 — depth 升序，同 depth 面积降序
                filtered.Sort((a, b) =>
                {
                    int cmp = a.Depth.CompareTo(b.Depth);
                    if (cmp == 0) cmp = b.Area.CompareTo(a.Area);
                    return cmp;
                });

                // Step 5: 构建结果
                var result = new List<ObjectId>();
                foreach (var item in filtered)
                    result.Add(curveIds[item.Index]);

                return result;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"SortByContainmentHierarchy 失败: {ex.Message}", ex);
                return new List<ObjectId>();
            }
        }
    }
}
