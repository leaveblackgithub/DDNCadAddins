using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropClosedCurveCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPCLOSEDCURVE — 先选择裁剪边界曲线 B（Clip，单选），再选择被剪曲线 A₁...Aₙ（Subjects，多选），
    ///     根据裁剪方向选择保留外部（差集）或保留内部（交集）.
    ///     支持 Polyline、Circle、Ellipse、Spline.
    ///     <para>
    ///         精确模式：使用 <see cref="CurveSubtractService"/> 逐边精确求交，
    ///         交点采用解析解（直线-圆/直线-椭圆二次方程），子线段参数化生成.
    ///     </para>
    ///     - 保留外部（差集 A \ B）：不相交→返回A，A包含B→带洞环，B包含A→空，相交→L形
    ///     - 保留内部（交集 A ∩ B）：不相交→空，A包含B→B，B包含A→A，相交→交集多边形
    /// </summary>
    public class CropClosedCurveCommand
    {
        /// <summary>曲线选择结果.</summary>
        public sealed class CurveSelection
        {
            /// <summary>曲线类型名称.</summary>
            public string Type;

            /// <summary>采样多边形顶点（用于 TestRecorder）.</summary>
            public List<CorePoint2D> Polygon;

            /// <summary>精确段列表（用于精确差集计算）.</summary>
            public List<ExactSegment> ExactSegments;

            /// <summary>精确裁剪边界（用于精确求交和包含测试）.</summary>
            public ICropBoundary Boundary;
        }

        /// <summary>
        ///     裁剪计算结果.
        /// </summary>
        public sealed class CropResult
        {
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
            public int PolyCount { get; set; }
            public int TotalVertices { get; set; }
            public string Uid { get; set; }
        }

        /// <summary>
        ///     从 Curve ObjectId 创建 CurveSelection.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="curveId">闭合曲线的 ObjectId.</param>
        /// <returns>曲线选择结果；失败返回 null.</returns>
        public static CurveSelection CreateCurveSelection(ObjectId curveId)
        {
            if (curveId.IsNull || curveId.IsErased) return null;

            CurveSelection sel = null;
            CadServiceManager._.ExecuteInTransactions(null, ts =>
            {
                var curve = ts.GetObject<Curve>(curveId, OpenMode.ForRead);
                if (curve == null || !curve.Closed) return;

                var exactSegments = CurveToExactSegmentConverter.ConvertToExactSegments(curve);
                if (exactSegments == null || exactSegments.Count == 0) return;

                var boundary = CurveToExactSegmentConverter.ConvertToCropBoundary(curve);
                if (boundary == null) return;

                var polygon = new CurveToPolygonConverter().ConvertCurveToPolygon(curve);
                if (polygon == null || polygon.Count < 3) return;

                sel = new CurveSelection
                {
                    Type = curve.GetType().Name,
                    Polygon = polygon,
                    ExactSegments = exactSegments,
                    Boundary = boundary
                };
            });

            return sel;
        }

        /// <summary>
        ///     执行多条闭合曲线 A₁...Aₙ 与一条闭合曲线 B 的裁剪运算（ObjectId 重载）.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        ///     内部自动完成 CreateCurveSelection + 计算 + 绘制.
        /// </summary>
        /// <param name="subjectCurveIds">Subject 曲线的 ObjectId 列表.</param>
        /// <param name="clipCurveId">Clip 曲线 B 的 ObjectId.</param>
        /// <param name="keepInside">true=保留内部（交集 A∩B），false=保留外部（差集 A\B）.</param>
        /// <returns>裁剪计算结果.</returns>
        public static CropResult CropClosedCurveMulti(
            IReadOnlyList<ObjectId> subjectCurveIds, ObjectId clipCurveId,
            bool keepInside)
        {
            // 内部完成 CreateCurveSelection
            var subjectCurves = new List<CurveSelection>();
            foreach (var id in subjectCurveIds)
            {
                var sel = CreateCurveSelection(id);
                if (sel != null)
                    subjectCurves.Add(sel);
            }

            var clipCurve = CreateCurveSelection(clipCurveId);
            return CropClosedCurveMulti(subjectCurves, clipCurve, keepInside);
        }

        /// <summary>
        ///     执行多条闭合曲线 A₁...Aₙ 与一条闭合曲线 B 的裁剪运算.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="subjectCurves">Subject 曲线列表.</param>
        /// <param name="clipCurve">Clip 曲线 B.</param>
        /// <param name="keepInside">true=保留内部（交集 A∩B），false=保留外部（差集 A\B）.</param>
        /// <returns>裁剪计算结果.</returns>
        public static CropResult CropClosedCurveMulti(
            IReadOnlyList<CurveSelection> subjectCurves, CurveSelection clipCurve,
            bool keepInside)
        {
            var result = new CropResult();
            try
            {
                if (subjectCurves == null || subjectCurves.Count == 0)
                {
                    result.Message = "未选择 Subject 曲线。";
                    return result;
                }
                if (clipCurve == null)
                {
                    result.Message = "未选择 Clip 曲线。";
                    return result;
                }

                var subtractService = new CurveSubtractService();

                // 构建 Subject 元组列表
                var subjects = new List<(IReadOnlyList<ExactSegment> Edges, ICropBoundary Boundary)>();
                foreach (var subj in subjectCurves)
                {
                    subjects.Add((subj.ExactSegments, subj.Boundary));
                }

                // 根据方向选择算法
                ExactSubtractResult subtractResult;
                if (keepInside)
                {
                    // 保留内部 = 交集 A ∩ B
                    var serviceResult = subtractService.IntersectMultiSubject(
                        subjects, clipCurve.ExactSegments, clipCurve.Boundary);
                    subtractResult = serviceResult.IsSuccess ? serviceResult.Data : null;
                }
                else
                {
                    // 保留外部 = 差集 A \ B
                    var serviceResult = subtractService.SubtractMultiSubject(
                        subjects, clipCurve.ExactSegments, clipCurve.Boundary);
                    subtractResult = serviceResult.IsSuccess ? serviceResult.Data : null;
                }

                bool noResult = subtractResult == null || subtractResult.IsEmpty;
                int resultPolyCount = 0;
                int totalVertices = 0;

                if (!noResult)
                {
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var loop in subtractResult.Loops)
                        {
                            if (loop == null || loop.Count == 0) continue;
                            int vertexCount = CurveToExactSegmentConverter.DrawExactSegments(ts, loop, 3);
                            if (vertexCount > 0)
                            {
                                resultPolyCount++;
                                totalVertices += vertexCount;
                            }
                        }
                    });
                }

                result.IsSuccess = resultPolyCount > 0;
                result.PolyCount = resultPolyCount;
                result.TotalVertices = totalVertices;
                string directionLabel = keepInside ? "减掉外部-保留内部" : "减掉内部-保留外部";
                result.Message = resultPolyCount > 0
                    ? $"{directionLabel}: {resultPolyCount} 个封闭环，共 {totalVertices} 个顶点"
                    : noResult ? "无结果"
                               : "裁剪绘制失败";
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPCLOSEDCURVE 失败: {ex.Message}", ex);
                result.Message = $"CROPCLOSEDCURVE 失败: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        ///     执行两条闭合曲线的精确裁剪运算（单 Subject 兼容重载）.
        /// </summary>
        public static CropResult CropClosedCurve(CurveSelection curveA, CurveSelection curveB, bool keepInside)
        {
            return CropClosedCurveMulti(new[] { curveA }, curveB, keepInside);
        }

        [CommandMethod("CROPCLOSEDCURVE")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var stopwatch = Stopwatch.StartNew();
                string uid = "";

                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                // ── 步骤 1: 选择裁剪边界曲线 B（Clip，单选）───────────────────
                var idB = SelectClosedCurveEntity(ed, "B（裁剪边界）");
                if (idB.IsNull) return;
                var curveB = CreateCurveSelection(idB);
                if (curveB == null) { ed.WriteMessage("\n边界曲线 B 转换失败。"); return; }

                // ── 步骤 2: 选择被剪曲线 A₁...Aₙ（Subjects，多选）───────────
                var subjectIds = SelectClosedCurveEntities(ed, "被剪曲线");
                if (subjectIds == null || subjectIds.Count == 0) return;

                var subjectCurves = new List<CurveSelection>();
                foreach (var subjId in subjectIds)
                {
                    var subjCurve = CreateCurveSelection(subjId);
                    if (subjCurve != null)
                        subjectCurves.Add(subjCurve);
                }

                if (subjectCurves.Count == 0)
                {
                    ed.WriteMessage("\n没有有效的被剪曲线。");
                    return;
                }

                ed.WriteMessage($"\n已选择 {subjectCurves.Count} 条被剪曲线。");

                // ── 步骤 2.5: 询问裁剪方向 ────────────────────────────────
                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue)
                    return; // 用户取消

                string directionLabel = keepInside.Value ? "减掉外部-保留内部" : "减掉内部-保留外部";

                // ── 步骤 3: 精确裁剪运算（调用核心方法）─────────────────────
                var result = CropClosedCurveMulti(subjectCurves, curveB, keepInside.Value);
                stopwatch.Stop();

                // ── 步骤 4: 输出命令行信息 ──────────────────────────────────
                ed.WriteMessage($"\n{directionLabel}结果：{result.Message}");

                // ── 步骤 5: TestRecorder 记录 ────────────────────────────
                try
                {
                    var snapshots = new List<CropEntitySnapshot>();
                    for (int i = 0; i < subjectCurves.Count; i++)
                    {
                        snapshots.Add(CreateSnapshot(
                            subjectIds[i].ToString(), subjectCurves[i].Type, subjectCurves[i].Polygon));
                    }
                    snapshots.Add(CreateSnapshot(idB.ToString(), curveB.Type, curveB.Polygon));

                    var record = new CropTestRecord
                    {
                        Command = "CROPCLOSEDCURVE",
                        Direction = directionLabel,
                        IsSuccess = result.IsSuccess,
                        UcsOrigin = ucsO,
                        UcsXAxis = ucsX,
                        UcsYAxis = ucsY,
                        BoundaryVertices = new List<Point2D>(curveB.Polygon),
                        BoundaryVertexCount = curveB.Polygon.Count,
                        TotalEntityCount = subjectCurves.Count + 1,
                        KeptCount = result.PolyCount,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Entities = snapshots,
                    };
                    uid = TestRecorder.Record(record);
                    ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                }
                catch (System.Exception recEx)
                {
                    Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                }
            }
            catch (System.Exception ex)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc.Editor.WriteMessage($"\nCROPCLOSEDCURVE 失败: {ex.Message}");
                Logger._.Error($"CROPCLOSEDCURVE 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     执行 CROPALLCLOSEDCURVES 命令：先选边界 B，再自动选择所有闭合曲线作为 Subject.
        /// </summary>
        [CommandMethod("CROPALLCLOSEDCURVES")]
        public void ExecuteAll()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var stopwatch = Stopwatch.StartNew();
                string uid = "";

                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                // ── 步骤 1: 选择裁剪边界曲线 B（Clip，单选）───────────────────
                var idB = SelectClosedCurveEntity(ed, "B（裁剪边界）");
                if (idB.IsNull) return;
                var curveB = CreateCurveSelection(idB);
                if (curveB == null) { ed.WriteMessage("\n边界曲线 B 转换失败。"); return; }

                // ── 步骤 2: 自动选择所有被剪闭合曲线（Subjects）───────────────
                ed.WriteMessage("\n正在自动选择图纸中所有闭合曲线...");
                List<ObjectId> allCurveIds = null;
                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var polylines = serviceTrans.GetChildObjectsFromModelspace<Polyline>();
                    var circles = serviceTrans.GetChildObjectsFromModelspace<Circle>();
                    var ellipses = serviceTrans.GetChildObjectsFromModelspace<Ellipse>();
                    var splines = serviceTrans.GetChildObjectsFromModelspace<Spline>();

                    allCurveIds = new List<ObjectId>();
                    if (polylines != null) allCurveIds.AddRange(polylines);
                    if (circles != null) allCurveIds.AddRange(circles);
                    if (ellipses != null) allCurveIds.AddRange(ellipses);
                    if (splines != null) allCurveIds.AddRange(splines);
                });

                if (allCurveIds == null || allCurveIds.Count == 0)
                {
                    ed.WriteMessage("\n图纸中没有找到任何闭合曲线。");
                    return;
                }

                var subjectCurves = new List<CurveSelection>();
                var subjectIds = new List<ObjectId>();
                foreach (var id in allCurveIds)
                {
                    var subjCurve = CreateCurveSelection(id);
                    if (subjCurve != null)
                    {
                        subjectCurves.Add(subjCurve);
                        subjectIds.Add(id);
                    }
                }

                if (subjectCurves.Count == 0)
                {
                    ed.WriteMessage("\n没有有效的闭合曲线。");
                    return;
                }

                ed.WriteMessage($"\n已自动选择 {subjectCurves.Count} 条被剪曲线。");

                // ── 步骤 2.5: 询问裁剪方向 ────────────────────────────────
                bool? keepInside = this.AskCropDirection(ed);
                if (!keepInside.HasValue)
                    return;

                string directionLabel = keepInside.Value ? "减掉外部-保留内部" : "减掉内部-保留外部";

                // ── 步骤 3: 精确裁剪运算 ───────────────────────────────────
                var result = CropClosedCurveMulti(subjectCurves, curveB, keepInside.Value);
                stopwatch.Stop();

                // ── 步骤 4: 输出命令行信息 ──────────────────────────────────
                ed.WriteMessage($"\n{directionLabel}结果：{result.Message}");

                // ── 步骤 5: TestRecorder 记录 ────────────────────────────
                try
                {
                    var snapshots = new List<CropEntitySnapshot>();
                    for (int i = 0; i < subjectCurves.Count; i++)
                    {
                        snapshots.Add(CreateSnapshot(
                            subjectIds[i].ToString(), subjectCurves[i].Type, subjectCurves[i].Polygon));
                    }
                    snapshots.Add(CreateSnapshot(idB.ToString(), curveB.Type, curveB.Polygon));

                    var record = new CropTestRecord
                    {
                        Command = "CROPALLCLOSEDCURVES",
                        Direction = directionLabel,
                        IsSuccess = result.IsSuccess,
                        UcsOrigin = ucsO,
                        UcsXAxis = ucsX,
                        UcsYAxis = ucsY,
                        BoundaryVertices = new List<Point2D>(curveB.Polygon),
                        BoundaryVertexCount = curveB.Polygon.Count,
                        TotalEntityCount = subjectCurves.Count + 1,
                        KeptCount = result.PolyCount,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Entities = snapshots,
                    };
                    uid = TestRecorder.Record(record);
                    ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                }
                catch (System.Exception recEx)
                {
                    Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                }
            }
            catch (System.Exception ex)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc.Editor.WriteMessage($"\nCROPALLCLOSEDCURVES 失败: {ex.Message}");
                Logger._.Error($"CROPALLCLOSEDCURVES 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     询问裁剪方向：减掉外部-保留内部，还是减掉内部-保留外部.
        /// </summary>
        /// <returns>true=减掉外部（保留内部），false=减掉内部（保留外部），null=取消.</returns>
        private bool? AskCropDirection(Editor ed)
        {
            try
            {
                var options = new PromptKeywordOptions(
                    "\n请选择裁剪方向 [减掉外部-保留内部(O)/减掉内部-保留外部(I)]: ", "减掉外部 减掉内部");
                options.Keywords.Add("减掉外部", "减掉外部-保留内部(O)", "减掉边界外部的实体，保留内部");
                options.Keywords.Add("减掉内部", "减掉内部-保留外部(I)", "减掉边界内部的实体，保留外部");
                options.Keywords.Default = "减掉外部";
                options.AllowNone = true;

                var result = ed.GetKeywords(options);
                if (result.Status != PromptStatus.OK && result.Status != PromptStatus.Keyword)
                {
                    ed.WriteMessage("\n取消裁剪方向选择。");
                    return null;
                }

                // 减掉外部 = 保留内部 = keepInside = true
                // 减掉内部 = 保留外部 = keepInside = false
                if (result.StringResult == "减掉外部")
                    return true;
                if (result.StringResult == "减掉内部")
                    return false;

                // 默认 = 减掉外部（保留内部）
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
        ///     选择单条闭合曲线，返回 ObjectId.
        /// </summary>
        private static ObjectId SelectClosedCurveEntity(Editor ed, string label)
        {
            try
            {
                var opt = new PromptEntityOptions($"\n选择闭合曲线 {label}: ");
                opt.SetRejectMessage($"\n请选择闭合的 Polyline/Circle/Ellipse/Spline 作为曲线 {label}。");
                opt.AddAllowedClass(typeof(Curve), false);
                var res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage($"\n未选择曲线 {label}。");
                    return ObjectId.Null;
                }
                return res.ObjectId;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择曲线 {label} 失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     选择多条闭合曲线，支持框选和点选，返回 ObjectId 列表.
        /// </summary>
        /// <param name="label">选择提示标签.</param>
        private static List<ObjectId> SelectClosedCurveEntities(Editor ed, string label)
        {
            try
            {
                var filter = new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "POLYLINE"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "ELLIPSE"),
                    new TypedValue((int)DxfCode.Start, "SPLINE"),
                    new TypedValue((int)DxfCode.Operator, "OR>"),
                });

                var options = new PromptSelectionOptions
                {
                    MessageForAdding = $"\n选择{label}（可多选，回车确认）: ",
                    AllowDuplicates = false,
                };

                var result = ed.GetSelection(options, filter);
                if (result.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何曲线或选择被取消。");
                    return null;
                }

                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in result.Value)
                    ids.Add(selObj.ObjectId);

                ed.WriteMessage($"\n已选择 {ids.Count} 条曲线。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择曲线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择曲线失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     创建曲线几何快照（用于 TestRecorder）.
        /// </summary>
        private static CropEntitySnapshot CreateSnapshot(
            string objectId, string type, IReadOnlyList<CorePoint2D> polygon)
        {
            var snap = new CropEntitySnapshot
            {
                ObjectId = objectId,
                Type = type,
                Containment = "N/A",
                Result = "Input",
                KeyGeometry = new List<Point2D>(polygon.Count),
                KeyParams = new List<double> { polygon.Count },
            };
            foreach (var pt in polygon)
                snap.KeyGeometry.Add(new Point2D(pt.X, pt.Y));
            return snap;
        }
    }
}
