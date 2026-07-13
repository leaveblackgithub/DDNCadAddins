using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    public class CropHatchResult
    {
        public int DeletedCount { get; set; }
        public int KeptCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public sealed class ProcessHatchesResult
    {
        public bool IsSuccess { get; set; }
        public int TotalHatchesProcessed { get; set; }
        public int TotalBoundaryEntities { get; set; }
        public int NewHatchesCreated { get; set; }
    }

    /// <summary>
    ///     带原始环关联的裁剪结果.
    /// </summary>
    internal sealed class CroppedLoopInfo
    {
        public ObjectId CurveId;
        public int OriginalLoopIndex;
        public int OriginalDepth;
        public int NewDepth;
        public double Area;
    }

    public class CropHatchService
    {
        private readonly ICropGeometryService _geometry;

        public CropHatchService(ICropGeometryService geometry)
        {
            this._geometry = geometry ?? new CropGeometryService();
        }

        public OpResult<CropHatchResult> CropHatchesInside(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts)
            => this.Crop(bp, ids, ts, true);

        public OpResult<CropHatchResult> CropHatchesOutside(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts)
            => this.Crop(bp, ids, ts, false);

        private OpResult<CropHatchResult> Crop(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids,
            ITransactionService ts, bool keepInside)
        {
            var r = new CropHatchResult();
            try
            {
                foreach (var id in ids)
                {
                    if (!id.IsValid || id.IsErased) { r.SkippedCount++; continue; }
                    var e = ts.GetObject<Hatch>(id);
                    if (e == null || e.IsErased) { r.SkippedCount++; continue; }
                    var cr = CropUtils.ProcessNonCurve(e, bp, keepInside, this._geometry);
                    r.DeletedCount += cr.DeletedCount;
                    r.KeptCount += cr.KeptCount;
                }
                return OpResult<CropHatchResult>.Success(r);
            }
            catch (Exception ex)
            {
                Logger._.Error($"CropHatchService.Crop failed: {ex.Message}", ex);
                return OpResult<CropHatchResult>.Fail($"Crop Hatch failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     批量处理 Hatch 裁剪（GenerateHatchBoundary → CropClosedCurveMulti → SortByContainmentHierarchy → CloneHatch）.
        ///     恢复 fdd8179 完整逻辑：批量裁剪所有环 → 包含关系层级排序 → 统一重建 Hatch.
        ///     此方法可被 CROPINSIDE/CROPOUTSIDE 等命令直接调用.
        /// </summary>
        /// <param name="hatchIds">待裁剪的 Hatch ObjectId 列表.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="boundary">裁剪边界（用于面积计算）.</param>
        /// <param name="keepInside">true=保留内部(CROPOUTSIDE)，false=保留外部(CROPINSIDE).</param>
        /// <returns>处理结果.</returns>
        public ProcessHatchesResult ProcessHatches(
            IReadOnlyList<ObjectId> hatchIds, ObjectId boundaryId,
            ICropBoundary boundary, bool keepInside)
        {
            var result = new ProcessHatchesResult();
            try
            {
                if (hatchIds == null || hatchIds.Count == 0)
                { result.IsSuccess = true; return result; }
                if (boundaryId.IsNull || boundaryId.IsErased)
                { result.IsSuccess = false; return result; }

                int totalHatchesProcessed = 0;
                int totalBoundaryEntities = 0;
                var allGeneratedIds = new List<ObjectId>();

                // ★ 第一步：调用 GenerateHatchBoundary 生成所有 Hatch 的边界实体
                foreach (var hatchId in hatchIds)
                {
                    if (!hatchId.IsValid || hatchId.IsErased)
                        continue;

                    var genResult = HatchBoundaryService.GenerateHatchBoundary(hatchId);
                    if (genResult.IsSuccess)
                    {
                        totalBoundaryEntities += genResult.EntityCount;
                        totalHatchesProcessed++;
                        allGeneratedIds.AddRange(genResult.GeneratedEntityIds);
                        Logger._.Info($"Hatch {hatchId}: 生成 {genResult.EntityCount} 个边界实体 [{genResult.TypeLog}]");
                    }
                    else
                    {
                        Logger._.Warn($"Hatch {hatchId}: 边界生成失败 — {genResult.Message}");
                    }
                }

                Logger._.Info($"共生成 {allGeneratedIds.Count} 条边界曲线，准备用裁剪边界进行裁剪...");

                // ★ 第二步：批量调用 CropClosedCurveMulti 执行裁剪
                //    使用 CropResult.CreatedEntityIds 获取外环→内环顺序的结果曲线
                List<ObjectId> clippedCurveIds = new List<ObjectId>();
                if (allGeneratedIds.Count > 0)
                {
                    var cropResult = CropClosedCurveService.CropClosedCurveMulti(
                        allGeneratedIds, boundaryId, keepInside);

                    if (cropResult.IsSuccess)
                    {
                        Logger._.Info($"CROPCLOSEDCURVE 裁剪完成: {cropResult.Message}");
                        if (cropResult.CreatedEntityIds != null)
                            clippedCurveIds = cropResult.CreatedEntityIds;
                    }
                    else
                    {
                        Logger._.Warn($"CROPCLOSEDCURVE 裁剪: {cropResult.Message}");
                    }
                }
                else
                {
                    Logger._.Info("没有有效的边界曲线可供裁剪。");
                }

                Logger._.Info($"裁剪后新生成 {clippedCurveIds.Count} 条曲线，准备用源 Hatch 参数填充...");

                // ★ 第三步：获取源 HatchStyle + 计算 clipArea → 调用 SortByContainmentHierarchy
                List<ObjectId> sortedCurveIds = new List<ObjectId>();
                if (clippedCurveIds.Count > 0)
                {
                    CadServiceManager._.ExecuteInTransactions(null, ts =>
                    {
                        // 获取源 Hatch 的 HatchStyle（取第一个有效 Hatch）
                        HatchStyle srcStyle = HatchStyle.Normal;
                        foreach (var hid in hatchIds)
                        {
                            if (hid.IsValid && !hid.IsErased)
                            {
                                var srcHatch = ts.GetObject<Hatch>(hid, OpenMode.ForRead);
                                if (srcHatch != null) { srcStyle = srcHatch.HatchStyle; break; }
                            }
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

                        // ★ Outer 样式：clipDepth >= 1 → 删除 Hatch（裁剪区域在孔洞内）
                        if (srcStyle == HatchStyle.Outer && clipArea > 0)
                        {
                            int clipDepth = DetermineClipDepthForAllGenerated(allGeneratedIds, clipArea, ts);
                            if (clipDepth >= 1)
                            {
                                Logger._.Info($"Outer 样式：裁剪边界是内环或无效环(depth={clipDepth})，删除 Hatch");
                                sortedCurveIds = new List<ObjectId>();
                                return;
                            }
                        }

                        // 使用包含关系层次排序（含 Ignore 早期返回逻辑）
                        sortedCurveIds = SortByContainmentHierarchy(
                            clippedCurveIds, srcStyle, ts, clipArea);
                    });
                }

                Logger._.Info($"按包含关系排序后取 {sortedCurveIds.Count} 条曲线用于重建 Hatch...");

                // ★ 第四步：对每个源 Hatch 用 CloneHatchWithNewBoundaries 创建新 Hatch，清理中间产物
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

                                var extractResult = HatchCloneService.ExtractHatchParams(srcHatchId);
                                if (!extractResult.IsSuccess)
                                {
                                    Logger._.Warn($"Hatch {srcHatchId}: 提取参数失败 — {extractResult.Message}");
                                    continue;
                                }

                                ObjectId newHatchId = ObjectId.Null;
                                var created = HatchCloneService.CloneHatchWithNewBoundaries(
                                    ts, extractResult.Data,
                                    sortedCurveIds.ToArray(), out newHatchId);

                                if (created && !newHatchId.IsNull)
                                {
                                    newHatchesCreated++;
                                    Logger._.Info($"Hatch {srcHatchId}: 新填充已创建 ({newHatchId})");
                                }
                                else
                                {
                                    Logger._.Warn($"Hatch {srcHatchId}: 创建新填充失败");
                                }
                            }

                            // ★ 第五步：清理中间产物
                            CleanupEntities(ts, allGeneratedIds);
                            CleanupEntities(ts, clippedCurveIds);
                            foreach (var id in hatchIds)
                            {
                                if (!id.IsValid || id.IsErased) continue;
                                try { var e = ts.GetObject<Entity>(id, OpenMode.ForWrite); if (e != null && !e.IsErased) e.Erase(); } catch { }
                            }

                            return ServiceACAD.OpResult.Success();
                        }
                        catch (Exception ex)
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
                                try { var e = ts.GetObject<Entity>(id, OpenMode.ForWrite); if (e != null && !e.IsErased) e.Erase(); } catch { }
                            }
                            foreach (var id in allGeneratedIds)
                            {
                                if (!id.IsValid || id.IsErased) continue;
                                try { var e = ts.GetObject<Entity>(id, OpenMode.ForWrite); if (e != null && !e.IsErased) e.Erase(); } catch { }
                            }
                            return ServiceACAD.OpResult.Success();
                        }
                        catch (Exception ex)
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
            catch (Exception ex)
            {
                Logger._.Error($"ProcessHatches failed: {ex.Message}", ex);
                result.IsSuccess = false;
            }
            return result;
        }

        // ── 过滤 ──

        internal static List<CroppedLoopInfo> FilterByStyle(
            List<CroppedLoopInfo> loops, HatchStyle style, bool keepInside, int clipDepth)
        {
            try
            {
                if (keepInside) return FilterKeepInside(loops, style, clipDepth);
                else return FilterKeepOutside(loops, style, clipDepth);
            }
            catch (Exception ex) { Logger._.Error($"FilterByStyle: {ex.Message}", ex); return new List<CroppedLoopInfo>(); }
        }

        private static List<CroppedLoopInfo> FilterKeepInside(
            List<CroppedLoopInfo> loops, HatchStyle style, int clipDepth)
        {
            switch (style)
            {
                case HatchStyle.Normal: return loops.Where(l => l.NewDepth >= 0).ToList();
                case HatchStyle.Outer: return loops.Where(l => l.NewDepth >= 0 && l.NewDepth <= 1).ToList();
                case HatchStyle.Ignore: return loops.Where(l => l.NewDepth == 0).ToList();
                default: return loops.Where(l => l.NewDepth >= 0).ToList();
            }
        }

        private static List<CroppedLoopInfo> FilterKeepOutside(
            List<CroppedLoopInfo> loops, HatchStyle style, int clipDepth)
        {
            switch (style)
            {
                case HatchStyle.Normal: return loops.Where(l => l.NewDepth >= 0).ToList();
                case HatchStyle.Outer: return loops.Where(l => l.NewDepth >= 0 && l.NewDepth <= 1).ToList();
                case HatchStyle.Ignore: return loops.Where(l => l.NewDepth == 0).ToList();
                default: return loops.Where(l => l.NewDepth >= 0).ToList();
            }
        }

        // ── Style ──

        /// <summary>
        ///     确定重建 Hatch 时应使用的 HatchStyle.
        ///     保留内部: 保持源 Style.
        ///     保留外部: IGNORE + 多环结果 → OUTER（环形区域=外环+内环需要孔洞语义）.
        ///     OUTER 始终保持 OUTER（即使只剩单环，语义不变）.
        /// </summary>
        internal static HatchStyle DetermineTargetStyle(
            HatchStyle srcStyle, bool keepInside, int clipDepth, int filteredCount)
        {
            try
            {
                if (keepInside) return srcStyle;

                // 保留外部: IGNORE 只有外环A，A\B可能产生多个环（外环+孔洞B）
                // 多环结果需要用 OUTER 来正确表达"外环内部挖孔洞"的语义
                if (srcStyle == HatchStyle.Ignore && filteredCount > 1) return HatchStyle.Outer;

                // OUTER 始终保持 OUTER，不转换为 IGNORE
                return srcStyle;
            }
            catch (Exception ex) { Logger._.Error($"DetermineTargetStyle: {ex.Message}", ex); return srcStyle; }
        }

        // ── 排序去重 ──

        internal static List<CroppedLoopInfo> SortAndDeduplicate(List<CroppedLoopInfo> loops)
        {
            try
            {
                if (loops == null || loops.Count <= 1) return loops ?? new List<CroppedLoopInfo>();
                const double tol = 1e-8;
                var deduped = new List<CroppedLoopInfo>();
                foreach (var l in loops)
                {
                    bool dup = false;
                    foreach (var e in deduped)
                    { if (Math.Abs(l.Area - e.Area) < tol) { dup = true; break; } }
                    if (!dup) deduped.Add(l);
                }
                deduped.Sort((a, b) =>
                {
                    int c = a.NewDepth.CompareTo(b.NewDepth);
                    if (c == 0) c = b.Area.CompareTo(a.Area);
                    return c;
                });
                return deduped;
            }
            catch (Exception ex) { Logger._.Error($"SortAndDeduplicate: {ex.Message}", ex); return loops ?? new List<CroppedLoopInfo>(); }
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     根据 allGeneratedIds 计算 clipDepth（裁剪边界面积匹配的原始环深度）.
        ///     面积按降序排序，匹配位置索引即为 clipDepth（0=外环, 1=内环）.
        /// </summary>
        private static int DetermineClipDepthForAllGenerated(
            List<ObjectId> allGeneratedIds, double clipArea, ITransactionService ts)
        {
            try
            {
                if (allGeneratedIds == null || allGeneratedIds.Count == 0 || clipArea <= 0)
                    return 0;

                // 计算原始边界实体的面积，按面积降序排序
                var origAreas = new List<double>();
                foreach (var id in allGeneratedIds)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    var ent = ts.GetObject<Entity>(id, OpenMode.ForRead);
                    if (ent is Polyline pl) origAreas.Add(pl.Area);
                    else if (ent is Circle cir) origAreas.Add(cir.Area);
                    else if (ent is Ellipse ell) origAreas.Add(ell.Area);
                }
                origAreas.Sort((a, b) => b.CompareTo(a)); // 降序

                // 面积差 < 1% 视为匹配
                for (int i = 0; i < origAreas.Count; i++)
                {
                    double ratio = Math.Abs(origAreas[i] - clipArea) / clipArea;
                    if (ratio < 0.01)
                        return i; // 0=外环, 1=内环, 2+=无效环
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger._.Error($"DetermineClipDepthForAllGenerated: {ex.Message}", ex);
                return 0;
            }
        }

        private static void CleanupEntities(ITransactionService ts, List<ObjectId> ids)
        {
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased) continue;
                try { var e = ts.GetObject<Entity>(id, OpenMode.ForWrite); if (e != null && !e.IsErased) e.Erase(); } catch { }
            }
        }

        internal static double ComputeBoundaryArea(ICropBoundary boundary)
        {
            try
            {
                if (boundary is CircleCropBoundary c) return Math.PI * c.Radius * c.Radius;
                if (boundary is EllipseCropBoundary e) return Math.PI * e.MajorRadius * e.MinorRadius;
                return ComputePolygonArea(boundary.GetApproximatePolygon());
            }
            catch (Exception ex) { Logger._.Error($"ComputeBoundaryArea: {ex.Message}", ex); return 0; }
        }

        internal static double ComputePolygonArea(IReadOnlyList<CorePoint2D> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0;
            double area = 0; int n = polygon.Count;
            for (int i = 0; i < n; i++)
            { int j = (i + 1) % n; area += polygon[i].X * polygon[j].Y; area -= polygon[j].X * polygon[i].Y; }
            return Math.Abs(area) / 2.0;
        }

        internal static bool IsPointInsidePolygon(Point3d point, Polyline polyline)
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
                    if ((p1.Y > py) != (p2.Y > py) &&
                        px < (p2.X - p1.X) * (py - p1.Y) / (p2.Y - p1.Y) + p1.X)
                        inside = !inside;
                }
                return inside;
            }
            catch (Exception ex) { Logger._.Error($"IsPointInsidePolygon: {ex.Message}", ex); return false; }
        }

        /// <summary>
        ///     使用包含关系层次排序裁剪后的曲线列表（恢复 fdd8179 完整逻辑）.
        /// </summary>
        internal static List<ObjectId> SortByContainmentHierarchy(
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

                // Step 2: Ignore 样式 — 只需取面积最大的曲线（外环），不需要包含关系检测
                //    Ignore 语义 = 只填充最外环，忽略所有内环
                if (style == HatchStyle.Ignore)
                {
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

                const double areaTol = 1e-8;

                // Step 3: Outer / Normal — 构建包含关系层次
                //    同形检测：面积近似相等（差 < 1e-8）则视为 siblings，不建立包含关系
                //    使用多顶点投票法
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

                // ★ 调试日志
                for (int i = 0; i < n; i++)
                {
                    if (plineCache[i] == null) continue;
                    Logger._.Info($"[ContainmentSort] 曲线[{i}]: Area={areas[i]:F4}, Depth={depth[i]}, Style={style}");
                }

                // Step 4: 按 HatchStyle 过滤 + 去重
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

                // Step 5: Normal 样式 — 过滤与裁剪边界同形的"容器"曲线
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

                // Step 6: 移除容器曲线后，重新计算剩余曲线的 depth
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

                // Step 7: 排序 — depth 升序，同 depth 面积降序
                filtered.Sort((a, b) =>
                {
                    int cmp = a.Depth.CompareTo(b.Depth);
                    if (cmp == 0) cmp = b.Area.CompareTo(a.Area);
                    return cmp;
                });

                // Step 8: 构建结果
                var result = new List<ObjectId>();
                foreach (var item in filtered)
                    result.Add(curveIds[item.Index]);

                return result;
            }
            catch (Exception ex) { Logger._.Error($"SortByContainmentHierarchy: {ex.Message}", ex); return new List<ObjectId>(); }
        }
    }
}
