using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     Hatch 裁剪结果（按边界框分类的保留/删除统计）.
    /// </summary>
    public class CropHatchResult
    {
        /// <summary>删除数量.</summary>
        public int DeletedCount { get; set; }

        /// <summary>保留数量.</summary>
        public int KeptCount { get; set; }

        /// <summary>跳过数量.</summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    ///     ProcessHatches 处理结果.
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
    ///     Hatch 裁剪服务 — 框分类裁剪和批量裁剪处理.
    /// </summary>
    public class CropHatchService
    {
        private readonly ICropGeometryService _geometry;

        /// <summary>
        ///     构造函数.
        /// </summary>
        /// <param name="geometry">裁剪几何服务.</param>
        public CropHatchService(ICropGeometryService geometry)
        {
            this._geometry = geometry ?? new CropGeometryService();
        }

        /// <summary>
        ///     裁剪 Hatch 保留边界内部.
        /// </summary>
        public OpResult<CropHatchResult> CropHatchesInside(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts)
            => this.Crop(bp, ids, ts, true);

        /// <summary>
        ///     裁剪 Hatch 保留边界外部.
        /// </summary>
        public OpResult<CropHatchResult> CropHatchesOutside(
            IReadOnlyList<CorePoint2D> bp, List<ObjectId> ids, ITransactionService ts)
            => this.Crop(bp, ids, ts, false);

        /// <summary>
        ///     按边界框分类裁剪 Hatch.
        /// </summary>
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
                Logger._.Error($"CropHatchService.Crop 失败: {ex.Message}", ex);
                return OpResult<CropHatchResult>.Fail($"裁剪 Hatch 失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     批量处理 Hatch 裁剪（GenerateHatchBoundary → CropClosedCurveMulti → CloneHatch）.
        ///     此方法可被 CROPINSIDE/CROPOUTSIDE/CROPHATCH 等命令直接调用.
        ///     <para>
        ///         与旧版 <c>ProcessHatches(Editor, ...)</c> 的区别：
        ///         - 去除了 Editor 依赖，改用 Logger._.Info() 记录日志
        ///         - 新增 <paramref name="boundary"/> 参数，支持 ICropBoundary 抽象边界
        ///         - 命令层调用方自行输出 <see cref="ProcessHatchesResult"/> 的汇总信息
        ///     </para>
        /// </summary>
        /// <param name="hatchIds">待裁剪的 Hatch ObjectId 列表.</param>
        /// <param name="boundaryId">裁剪边界曲线 ObjectId.</param>
        /// <param name="boundary">抽象裁剪边界（用于面积计算等几何操作）.</param>
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
                        Logger._.Info($"Hatch {hatchId}: 边界生成失败 — {genResult.Message}");
                    }
                }

                Logger._.Info($"共生成 {allGeneratedIds.Count} 条边界曲线，准备用裁剪边界进行裁剪...");

                // ★ 第二步：调用 CropClosedCurveService 执行裁剪
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
                        Logger._.Info($"CROPCLOSEDCURVE 裁剪: {cropResult.Message}");
                    }
                }
                else
                {
                    Logger._.Info("没有有效的边界曲线可供裁剪。");
                }

                Logger._.Info($"裁剪后新生成 {clippedCurveIds.Count} 条曲线，准备用源 Hatch 参数填充...");

                // ★ 第四步：统一环有效性 + clipDepth 逻辑
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

                        // 计算裁剪边界的面积（从 ICropBoundary 获取，不再依赖 AutoCAD Curve）
                        double clipArea = ComputeBoundaryArea(boundary);

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
                        origAreas.Sort((a, b) => b.CompareTo(a));

                        // 确定 clipDepth：裁剪边界面积匹配哪个原始环
                        int clipDepth = 0;
                        if (clipArea > 0)
                        {
                            for (int i = 0; i < origAreas.Count; i++)
                            {
                                double ratio = Math.Abs(origAreas[i] - clipArea) / clipArea;
                                if (ratio < 0.01)
                                {
                                    clipDepth = i;
                                    break;
                                }
                            }
                        }

                        // ★ Outer 样式：clipDepth >= 1 → 删除 Hatch
                        if (srcStyle == HatchStyle.Outer && clipDepth >= 1)
                        {
                            Logger._.Info($"Outer 样式：裁剪边界是内环或无效环(depth={clipDepth})，删除 Hatch");
                            sortedCurveIds = new List<ObjectId>();
                            return;
                        }

                        // 使用包含关系层次排序
                        sortedCurveIds = SortByContainmentHierarchy(
                            clippedCurveIds, srcStyle, ts, clipArea);
                    });
                }

                Logger._.Info($"按包含关系排序后取 {sortedCurveIds.Count} 条曲线用于重建 Hatch...");

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

                                var extractResult = HatchCloneService.ExtractHatchParams(srcHatchId);
                                if (!extractResult.IsSuccess)
                                {
                                    Logger._.Info($"Hatch {srcHatchId}: 提取参数失败 — {extractResult.Message}");
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
                                    Logger._.Info($"Hatch {srcHatchId}: 创建新填充失败");
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
                        catch (Exception ex)
                        {
                            Logger._.Error($"CloneHatch 填充失败: {ex.Message}", ex);
                            return ServiceACAD.OpResult.Fail(ex.Message);
                        }
                    });
                }
                else
                {
                    // 无裁剪结果：删除原始 Hatch 和中间边界实体
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
                Logger._.Error($"ProcessHatches 失败: {ex.Message}", ex);
                result.IsSuccess = false;
            }
            return result;
        }

        /// <summary>
        ///     计算裁剪边界面积（从 ICropBoundary 获取，无 AutoCAD 依赖）.
        ///     Circle/椭圆使用解析公式，其他使用多边形面积公式.
        /// </summary>
        private static double ComputeBoundaryArea(ICropBoundary boundary)
        {
            try
            {
                if (boundary is CircleCropBoundary circle)
                    return Math.PI * circle.Radius * circle.Radius;
                if (boundary is EllipseCropBoundary ellipse)
                    return Math.PI * ellipse.MajorRadius * ellipse.MinorRadius;
                // PolygonCropBoundary / SplineCropBoundary / 其他
                var polygon = boundary.GetApproximatePolygon();
                return ComputePolygonArea(polygon);
            }
            catch (Exception ex)
            {
                Logger._.Error($"ComputeBoundaryArea 失败: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        ///     计算多边形面积（Shoelace formula）.
        /// </summary>
        private static double ComputePolygonArea(IReadOnlyList<CorePoint2D> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0;
            double area = 0;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += polygon[i].X * polygon[j].Y;
                area -= polygon[j].X * polygon[i].Y;
            }
            return Math.Abs(area) / 2.0;
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
            catch (Exception ex)
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
                if (style == HatchStyle.Ignore)
                {
                    // Ignore: 只需取面积最大的曲线（外环）
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
                    // 去重
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

                // Step 3c: 移除容器曲线后，重新计算 depth
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
            catch (Exception ex)
            {
                Logger._.Error($"SortByContainmentHierarchy 失败: {ex.Message}", ex);
                return new List<ObjectId>();
            }
        }
    }
}
