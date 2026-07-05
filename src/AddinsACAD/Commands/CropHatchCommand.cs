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

                // ★ 第一步：调用 GENERATEHATCHBOUNDARY 生成所有 Hatch 的边界实体
                foreach (var hatchId in hatchIds)
                {
                    if (!hatchId.IsValid || hatchId.IsErased)
                        continue;

                    var genResult = GenerateHatchBoundaryCommand.GenerateHatchBoundary(hatchId);
                    if (genResult.IsSuccess)
                    {
                        totalBoundaryEntities += genResult.EntityCount;
                        totalHatchesProcessed++;
                        allGeneratedIds.AddRange(genResult.GeneratedEntityIds);
                        ed.WriteMessage($"\n  Hatch {hatchId}: 生成 {genResult.EntityCount} 个边界实体 [{genResult.TypeLog}]");
                    }
                    else
                    {
                        ed.WriteMessage($"\n  Hatch {hatchId}: 边界生成失败 — {genResult.Message}");
                    }
                }

                ed.WriteMessage($"\n  共生成 {allGeneratedIds.Count} 条边界曲线，准备用裁剪边界进行裁剪...");

                // ★ 第二步：调用 CROPCLOSEDCURVE 执行裁剪
                //    使用 CropResult.CreatedEntityIds 获取外环→内环顺序的结果曲线，
                //    而非依赖不可靠的 before/after diff（BlockTableRecord 迭代顺序不确定）.
                List<ObjectId> clippedCurveIds = new List<ObjectId>();
                if (allGeneratedIds.Count > 0)
                {
                    var cropResult = CropClosedCurveCommand.CropClosedCurveMulti(
                        allGeneratedIds, boundaryId, keepInside);

                    if (cropResult.IsSuccess)
                    {
                        ed.WriteMessage($"\n  CROPCLOSEDCURVE 裁剪完成: {cropResult.Message}");
                        if (cropResult.CreatedEntityIds != null)
                            clippedCurveIds = cropResult.CreatedEntityIds;
                    }
                    else
                    {
                        ed.WriteMessage($"\n  CROPCLOSEDCURVE 裁剪: {cropResult.Message}");
                    }
                }
                else
                {
                    ed.WriteMessage("\n  没有有效的边界曲线可供裁剪。");
                }

                ed.WriteMessage($"\n  裁剪后新生成 {clippedCurveIds.Count} 条曲线，准备用源 Hatch 参数填充...");

                // ★ 第四步：用包含关系层次排序替代面积排序
                //    构建包含树 → 按 depth 升序 + 面积降序排列
                //    HatchStyle.Ignore: 只保留 depth == 0（最外环）
                //    HatchStyle.Outer: 保留 depth <= 1（最外环 + 所有孔洞）
                //    HatchStyle.Normal: 保留所有 depth（交替填充）
                List<ObjectId> sortedCurveIds = new List<ObjectId>();
                if (clippedCurveIds.Count > 0)
                {
                    CadServiceManager._.ExecuteInTransactions(null, ts =>
                    {
                        // 获取源 Hatch 的 HatchStyle
                        HatchStyle srcStyle = HatchStyle.Normal;
                        if (hatchIds.Count > 0 && hatchIds[0].IsValid && !hatchIds[0].IsErased)
                        {
                            var srcHatch = ts.GetObject<Hatch>(hatchIds[0], OpenMode.ForRead);
                            if (srcHatch != null) srcStyle = srcHatch.HatchStyle;
                        }

                        // 使用包含关系层次排序（替代面积排序）
                        sortedCurveIds = SortByContainmentHierarchy(
                            clippedCurveIds, srcStyle, ts);
                    });
                }

                ed.WriteMessage($"\n  按包含关系排序后取 {sortedCurveIds.Count} 条曲线用于重建 Hatch...");

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

                            // ★ 第六步：清理中间产物
                            foreach (var id in allGeneratedIds)
                            {
                                if (!id.IsValid || id.IsErased) continue;
                                try
                                {
                                    var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                                    if (ent != null && !ent.IsErased) ent.Erase();
                                }
                                catch { }
                            }
                            foreach (var id in clippedCurveIds)
                            {
                                if (!id.IsValid || id.IsErased) continue;
                                // 跳过已选入 sortedCurveIds 的曲线（它们作为 Hatch 边界关联对象保留）
                                if (sortedCurveIds.Contains(id)) continue;
                                try
                                {
                                    var ent = ts.GetObject<Entity>(id, OpenMode.ForWrite);
                                    if (ent != null && !ent.IsErased) ent.Erase();
                                }
                                catch { }
                            }
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
                            foreach (var id in allGeneratedIds)
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
        /// <returns>排序后的 ObjectId 列表（depth 0 在前 = 最外环）.</returns>
        private static List<ObjectId> SortByContainmentHierarchy(
            List<ObjectId> curveIds, HatchStyle style, ITransactionService ts)
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
                //    同形检测：面积近似相等（差 < 1e-8）则视为 siblings，不建立包含关系
                const double areaTol = 1e-8;
                for (int i = 0; i < n; i++)
                {
                    if (plineCache[i] == null) continue;
                    var testPt = plineCache[i].GetPoint3dAt(0);

                    for (int j = 0; j < n; j++)
                    {
                        if (i == j || plineCache[j] == null) continue;

                        // 同形检测：面积近似相等则视为 siblings
                        if (Math.Abs(areas[i] - areas[j]) < areaTol) continue;

                        if (IsPointInsidePolygon(testPt, plineCache[j]))
                            depth[i]++;
                    }
                }

                // Step 3: 按 HatchStyle 过滤
                //    Ignore: 只保留 depth == 0
                //    Outer: 保留 depth <= 1
                //    Normal: 保留所有 depth
                var filtered = new List<(int Index, int Depth, double Area)>();
                for (int i = 0; i < n; i++)
                {
                    if (plineCache[i] == null) continue;
                    if (style == HatchStyle.Ignore && depth[i] > 0) continue;
                    if (style == HatchStyle.Outer && depth[i] > 1) continue;
                    filtered.Add((i, depth[i], areas[i]));
                }

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
