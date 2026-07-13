using System;
using System.Collections.Generic;
using System.Linq;
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

            /// <summary>生成的实体 ObjectId 列表（按环顺序排列）.</summary>
            public List<ObjectId> GeneratedEntityIds { get; set; } = new List<ObjectId>();

            /// <summary>每个环的面积（与 GeneratedEntityIds 索引对齐）.</summary>
            public double[] LoopAreas { get; set; } = Array.Empty<double>();

            /// <summary>每个环的包含深度（与 GeneratedEntityIds 索引对齐）.</summary>
            public int[] LoopDepths { get; set; } = Array.Empty<int>();

            /// <summary>
            ///     完整方法调用标志 — 用于 CropHatch 验证 GenerateHatchBoundary 打包方法被调用.
            ///     只有此标志为 true 的结果才能被 ProcessHatches 安全使用.
            /// </summary>
            public bool CalledFromCompleteMethod { get; set; }
        }

        /// <summary>
        ///     逐环边界信息 — 保留环索引、面积和原始深度，支持逐环裁剪.
        /// </summary>
        public sealed class LoopBoundaryInfo
        {
            /// <summary>原始环索引（对应 Hatch.GetLoopAt 的索引）.</summary>
            public int LoopIndex { get; set; }

            /// <summary>该环生成的边界实体 ObjectId 列表.</summary>
            public List<ObjectId> GeneratedEntityIds { get; set; } = new List<ObjectId>();

            /// <summary>该环面积（绝对值）.</summary>
            public double Area { get; set; }

            /// <summary>原始包含深度（计算后填充）.</summary>
            public int OriginalDepth { get; set; }
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
                var loopAreas = new List<double>();
                var loopDepths = new List<int>();

                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var hatch = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                    if (hatch == null) { result.Message = "无法打开 Hatch。"; return; }

                    var plane = new Plane(
                        Point3d.Origin + hatch.Normal * hatch.Elevation,
                        hatch.Normal);
                    loopCount = hatch.NumberOfLoops;

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

                            // ★ 在事务内直接读取面积，避免调用方额外开事务
                            var ent = ts.GetObject<Entity>(objId, OpenMode.ForRead);
                            double area = 0;
                            if (ent is Polyline pl) area = Math.Abs(pl.Area);
                            else if (ent is Circle cir) area = Math.PI * cir.Radius * cir.Radius;
                            else if (ent is Ellipse ell)
                            { double a = ell.MajorAxis.Length; double b = ell.MinorRadius; area = Math.PI * a * b; }
                            loopAreas.Add(area);
                            loopDepths.Add(0); // 先占位，后计算
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

                // ★ 计算深度（在事务外，使用生成的 entity IDs 和面积）
                if (generatedIds.Count > 1)
                {
                    int n = generatedIds.Count;
                    var areasArr = loopAreas.ToArray();
                    var depthsArr = new int[n];

                    var sortedIndices = Enumerable.Range(0, n)
                        .OrderByDescending(i => areasArr[i]).ToArray();

                    for (int si = 0; si < n; si++)
                    {
                        int i = sortedIndices[si];
                        if (areasArr[i] <= 0) continue;
                        for (int sj = 0; sj < si; sj++)
                        {
                            int j = sortedIndices[sj];
                            if (areasArr[j] <= 0) continue;
                            double ratio = Math.Abs(areasArr[i] - areasArr[j]) / areasArr[i];
                            if (ratio < 0.01) continue;
                            if (IsEntityInsideAnother(generatedIds[i], generatedIds[j]))
                                depthsArr[i]++;
                        }
                    }

                    for (int i = 0; i < n; i++)
                        loopDepths[i] = depthsArr[i];
                }

                result.IsSuccess = true;
                result.Message = "生成完成";
                result.LoopCount = loopCount;
                result.EntityCount = entityCount;
                result.TypeLog = typeLog;
                result.Uid = uid;
                result.GeneratedEntityIds = generatedIds;
                result.LoopAreas = loopAreas.ToArray();
                result.LoopDepths = loopDepths.ToArray();
                result.CalledFromCompleteMethod = true;
            }
            catch (Exception ex)
            {
                Logger._.Error($"GENERATEHATCHBOUNDARY 失败: {ex.Message}", ex);
                result.Message = $"GENERATEHATCHBOUNDARY 失败: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        ///     逐环生成 Hatch 边界实体，返回每个环的独立边界信息.
        ///     用于逐环裁剪，保留原始环的 depth 关联.
        /// </summary>
        /// <param name="hatchId">Hatch 实体的 ObjectId.</param>
        /// <returns>逐环边界信息列表；失败或无效时返回空列表.</returns>
        public static OpResult<List<LoopBoundaryInfo>> GenerateHatchBoundaryPerLoop(ObjectId hatchId)
        {
            try
            {
                if (hatchId.IsNull || hatchId.IsErased)
                    return OpResult<List<LoopBoundaryInfo>>.Fail("Hatch 无效或已被删除。");

                var loopInfos = new List<LoopBoundaryInfo>();

                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var hatch = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                    if (hatch == null) return;

                    var plane = new Plane(
                        Point3d.Origin + hatch.Normal * hatch.Elevation,
                        hatch.Normal);
                    int loopCount = hatch.NumberOfLoops;
                    var generator = new CurveToPolygonConverter();

                    for (int li = 0; li < loopCount; li++)
                    {
                        var loop = hatch.GetLoopAt(li);
                        if (loop == null) continue;

                        var info = new LoopBoundaryInfo { LoopIndex = li };
                        int color = (li == 0) ? 2 : 4;

                        var objId = generator.CreateEntityFromLoop(loop, plane, color, hatch.Layer, ts);
                        if (!objId.IsNull)
                        {
                            info.GeneratedEntityIds.Add(objId);

                            var ent = ts.GetObject<Entity>(objId, OpenMode.ForRead);
                            if (ent is Polyline pl)
                                info.Area = Math.Abs(pl.Area);
                            else if (ent is Circle cir)
                                info.Area = Math.PI * cir.Radius * cir.Radius;
                            else if (ent is Ellipse ell)
                            {
                                double a = ell.MajorAxis.Length;
                                double b = ell.MinorRadius;
                                info.Area = Math.PI * a * b;
                            }
                        }

                        loopInfos.Add(info);
                    }
                });

                ComputeOriginalDepths(loopInfos);

                return OpResult<List<LoopBoundaryInfo>>.Success(loopInfos);
            }
            catch (Exception ex)
            {
                Logger._.Error($"GenerateHatchBoundaryPerLoop 失败: {ex.Message}", ex);
                return OpResult<List<LoopBoundaryInfo>>.Fail($"逐环边界生成失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     根据包含关系计算每个环的 originalDepth.
        ///     环 A 在环 B 内部 → depth(A) = depth(B) + 1.
        ///     面积容差 1% 以内的环视为同一环，不计入包含.
        /// </summary>
        /// <param name="loopInfos">待计算深度的环列表.</param>
        private static void ComputeOriginalDepths(List<LoopBoundaryInfo> loopInfos)
        {
            try
            {
                if (loopInfos == null || loopInfos.Count <= 1)
                {
                    if (loopInfos != null && loopInfos.Count == 1)
                        loopInfos[0].OriginalDepth = 0;
                    return;
                }

                int n = loopInfos.Count;
                var depth = new int[n];

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        if (loopInfos[i].GeneratedEntityIds.Count == 0) continue;
                        if (loopInfos[j].GeneratedEntityIds.Count == 0) continue;

                        double areaRatio = loopInfos[i].Area > 0
                            ? Math.Abs(loopInfos[i].Area - loopInfos[j].Area) / loopInfos[i].Area
                            : 0;
                        if (areaRatio < 0.01) continue;

                        if (loopInfos[i].Area < loopInfos[j].Area)
                        {
                            if (IsLoopInsideAnother(loopInfos[i], loopInfos[j]))
                                depth[i]++;
                        }
                    }
                }

                for (int i = 0; i < n; i++)
                    loopInfos[i].OriginalDepth = depth[i];

                Logger._.Info($"[ComputeDepths] {n} loops: " +
                    string.Join(", ", loopInfos.ConvertAll(
                        l => $"L{l.LoopIndex}:Area={l.Area:F2},Depth={l.OriginalDepth}")));
            }
            catch (Exception ex)
            {
                Logger._.Error($"ComputeOriginalDepths 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     判断环 a 是否在环 b 内部（通过采样顶点检测）.
        /// </summary>
        private static bool IsLoopInsideAnother(LoopBoundaryInfo a, LoopBoundaryInfo b)
        {
            try
            {
                if (a.GeneratedEntityIds.Count == 0 || b.GeneratedEntityIds.Count == 0)
                    return false;

                bool isInside = false;
                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var plB = ts.GetObject<Polyline>(b.GeneratedEntityIds[0], OpenMode.ForRead);
                    if (plB == null || !plB.Closed) return;

                    var plA = ts.GetObject<Polyline>(a.GeneratedEntityIds[0], OpenMode.ForRead);
                    if (plA == null || !plA.Closed) return;

                    int testCount = 0;
                    int insideCount = 0;
                    int maxTests = Math.Min(5, plA.NumberOfVertices);
                    for (int v = 0; v < maxTests; v++)
                    {
                        var pt = plA.GetPoint3dAt(v);
                        if (IsPointInsidePolyline(pt, plB))
                            insideCount++;
                        testCount++;
                    }
                    if (testCount > 0 && insideCount > testCount / 2)
                        isInside = true;
                });

                return isInside;
            }
            catch (Exception ex)
            {
                Logger._.Error($"IsLoopInsideAnother 失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        ///     射线法判断点是否在闭合多段线内部.
        /// </summary>
        private static bool IsPointInsidePolyline(Point3d point, Polyline polyline)
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
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
            catch (Exception ex)
            {
                Logger._.Error($"IsPointInsidePolyline 失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        ///     判断实体 a 是否在实体 b 内部（采样顶点射线法）.
        ///     供 <see cref="GenerateHatchBoundary"/> 内部计算环深度使用.
        /// </summary>
        private static bool IsEntityInsideAnother(ObjectId aId, ObjectId bId)
        {
            try
            {
                if (aId.IsNull || bId.IsNull) return false;
                bool inside = false;
                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var entA = ts.GetObject<Entity>(aId, OpenMode.ForRead);
                    var entB = ts.GetObject<Entity>(bId, OpenMode.ForRead);
                    if (!(entA is Polyline plA) || !plA.Closed) return;
                    if (!(entB is Polyline plB) || !plB.Closed) return;
                    int tc = 0, ic = 0;
                    int max = Math.Min(5, plA.NumberOfVertices);
                    for (int v = 0; v < max; v++)
                    { if (IsPointInsidePolyline(plA.GetPoint3dAt(v), plB)) ic++; tc++; }
                    if (tc > 0 && ic > tc / 2) inside = true;
                });
                return inside;
            }
            catch (Exception ex) { Logger._.Error($"IsEntityInsideAnother: {ex.Message}", ex); return false; }
        }
    }
}
