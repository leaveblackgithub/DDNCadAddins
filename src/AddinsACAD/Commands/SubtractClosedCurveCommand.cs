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

[assembly: CommandClass(typeof(AddinsACAD.Commands.SubtractClosedCurveCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     SUBTRACTCLOSEDCURVE — 选择一个或多个被减曲线 A₁...Aₙ（Subjects），再选择一个减去曲线 B（Clip），
    ///     计算 (A₁ ∪ A₂ ∪ ...) \ B 并绘制结果.
    ///     支持 Polyline、Circle、Ellipse、Spline.
    ///     <para>
    ///         精确模式：使用 <see cref="CurveSubtractService"/> 逐边精确求交，
    ///         交点采用解析解（直线-圆/直线-椭圆二次方程），子线段参数化生成.
    ///         每个 Subject 独立执行 Aᵢ \ B 差集运算.
    ///     </para>
    ///     - 不相交 → 返回 A
    ///     - A 包含 B → 返回 A 减去 B 区域后的剩余部分（带内孔环）
    ///     - B 包含 A → 无结果（A 完全被减掉）
    ///     - 相交 → 返回 A 除掉交集部分的封闭多边形
    /// </summary>
    public class SubtractClosedCurveCommand
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
        ///     差集计算结果.
        /// </summary>
        public sealed class SubtractResult
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
        ///     执行多条闭合曲线 A₁...Aₙ 减去一条闭合曲线 B 的精确差集并绘制结果.
        ///     核心方法，不包含 UI 交互，可被其他命令或服务调用.
        /// </summary>
        /// <param name="subjectCurves">Subject 曲线列表.</param>
        /// <param name="clipCurve">Clip 曲线 B.</param>
        /// <returns>差集计算结果.</returns>
        public static SubtractResult SubtractClosedCurveMulti(
            IReadOnlyList<CurveSelection> subjectCurves, CurveSelection clipCurve)
        {
            var result = new SubtractResult();
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

                var subtractResult = subtractService.SubtractMultiSubject(
                    subjects, clipCurve.ExactSegments, clipCurve.Boundary);

                bool noResult = !subtractResult.IsSuccess || subtractResult.Data.IsEmpty;
                int resultPolyCount = 0;
                int totalVertices = 0;

                if (!noResult && subtractResult.IsSuccess)
                {
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var loop in subtractResult.Data.Loops)
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
                result.Message = resultPolyCount > 0
                    ? $"{resultPolyCount} 个封闭环，共 {totalVertices} 个顶点"
                    : noResult ? "无结果（B 包含所有 Subject，全部被减去）"
                               : "差集绘制失败";
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"SUBTRACTCLOSEDCURVE 失败: {ex.Message}", ex);
                result.Message = $"SUBTRACTCLOSEDCURVE 失败: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        ///     执行两条闭合曲线的精确差集 A \ B 并绘制结果（单 Subject 兼容重载）.
        /// </summary>
        public static SubtractResult SubtractClosedCurve(CurveSelection curveA, CurveSelection curveB)
        {
            return SubtractClosedCurveMulti(new[] { curveA }, curveB);
        }

        [CommandMethod("SUBTRACTCLOSEDCURVE")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var stopwatch = Stopwatch.StartNew();
                string uid = "";

                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                // ── 步骤 1: 选择被减曲线 A₁...Aₙ（Subjects，多选）───────────
                var subjectIds = SelectClosedCurveEntities(ed, "被减曲线");
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
                    ed.WriteMessage("\n没有有效的 Subject 曲线。");
                    return;
                }

                ed.WriteMessage($"\n已选择 {subjectCurves.Count} 条被减曲线。");

                // ── 步骤 2: 选择减去曲线 B（Clip，单选）──────────────────────
                var idB = SelectClosedCurveEntity(ed, "B（减去曲线）");
                if (idB.IsNull) return;
                var curveB = CreateCurveSelection(idB);
                if (curveB == null) { ed.WriteMessage("\n曲线 B 转换失败。"); return; }

                // ── 步骤 3: 精确差集 (A₁ ∪ ...) \ B（调用核心方法）────────────
                var result = SubtractClosedCurveMulti(subjectCurves, curveB);
                stopwatch.Stop();

                // ── 步骤 4: 输出命令行信息 ──────────────────────────────────
                ed.WriteMessage($"\n差集结果：{result.Message}");

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
                        Command = "SUBTRACTCLOSEDCURVE",
                        Direction = "Difference",
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
                doc.Editor.WriteMessage($"\nSUBTRACTCLOSEDCURVE 失败: {ex.Message}");
                Logger._.Error($"SUBTRACTCLOSEDCURVE 失败: {ex.Message}", ex);
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
                    ed.WriteMessage("\n未选择任何 Clip 曲线或选择被取消。");
                    return null;
                }

                var ids = new List<ObjectId>();
                foreach (SelectedObject selObj in result.Value)
                    ids.Add(selObj.ObjectId);

                ed.WriteMessage($"\n已选择 {ids.Count} 条 Clip 曲线。");
                return ids;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择 Clip 曲线失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择 Clip 曲线失败: {ex.Message}");
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
