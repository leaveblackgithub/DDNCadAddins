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
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Models;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropClosedCurveCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPCLOSEDCURVE — 先选择裁剪边界曲线 B（Clip，单选），再选择被剪曲线 A₁...Aₙ（Subjects，多选），
    ///     根据裁剪方向选择保留外部（差集）或保留内部（交集）.
    ///     核心逻辑委托给 <see cref="CropClosedCurveService"/> 实现.
    /// </summary>
    public class CropClosedCurveCommand
    {
        /// <summary>
        ///     CurveSelection 别名，委托到 <see cref="ServiceACAD.CropClosedCurveService.CurveSelection"/>.
        /// </summary>
        public sealed class CurveSelection : ServiceACAD.CropClosedCurveService.CurveSelection { }

        /// <summary>
        ///     CropResult 别名，委托到 <see cref="ServiceACAD.CropClosedCurveService.CropResult"/>.
        /// </summary>
        public sealed class CropResult : ServiceACAD.CropClosedCurveService.CropResult { }

        /// <summary>
        ///     从 Curve ObjectId 创建 CurveSelection.
        ///     委托到 <see cref="CropClosedCurveService.CreateCurveSelection"/>.
        /// </summary>
        public static ServiceACAD.CropClosedCurveService.CurveSelection CreateCurveSelection(ObjectId curveId)
        {
            return ServiceACAD.CropClosedCurveService.CreateCurveSelection(curveId);
        }

        /// <summary>
        ///     执行多条闭合曲线 A₁...Aₙ 与一条闭合曲线 B 的裁剪运算（ObjectId 重载）.
        ///     委托到 <see cref="CropClosedCurveService.CropClosedCurveMulti"/>.
        /// </summary>
        public static ServiceACAD.CropClosedCurveService.CropResult CropClosedCurveMulti(
            IReadOnlyList<ObjectId> subjectCurveIds, ObjectId clipCurveId, bool keepInside)
        {
            return ServiceACAD.CropClosedCurveService.CropClosedCurveMulti(
                subjectCurveIds, clipCurveId, keepInside);
        }

        /// <summary>
        ///     执行多条闭合曲线 A₁...Aₙ 与一条闭合曲线 B 的裁剪运算.
        ///     委托到 <see cref="CropClosedCurveService.CropClosedCurveMulti"/>.
        /// </summary>
        public static ServiceACAD.CropClosedCurveService.CropResult CropClosedCurveMulti(
            IReadOnlyList<ServiceACAD.CropClosedCurveService.CurveSelection> subjectCurves,
            ServiceACAD.CropClosedCurveService.CurveSelection clipCurve,
            bool keepInside)
        {
            return ServiceACAD.CropClosedCurveService.CropClosedCurveMulti(
                subjectCurves, clipCurve, keepInside);
        }

        /// <summary>
        ///     执行两条闭合曲线的精确裁剪运算（单 Subject 兼容重载）.
        /// </summary>
        public static ServiceACAD.CropClosedCurveService.CropResult CropClosedCurve(
            ServiceACAD.CropClosedCurveService.CurveSelection curveA,
            ServiceACAD.CropClosedCurveService.CurveSelection curveB,
            bool keepInside)
        {
            return ServiceACAD.CropClosedCurveService.CropClosedCurve(curveA, curveB, keepInside);
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
                var curveB = ServiceACAD.CropClosedCurveService.CreateCurveSelection(idB);
                if (curveB == null) { ed.WriteMessage("\n边界曲线 B 转换失败。"); return; }

                // ── 步骤 2: 选择被剪曲线 A₁...Aₙ（Subjects，多选）───────────
                var subjectIds = SelectClosedCurveEntities(ed, "被剪曲线");
                if (subjectIds == null || subjectIds.Count == 0) return;

                var subjectCurves = new List<ServiceACAD.CropClosedCurveService.CurveSelection>();
                foreach (var subjId in subjectIds)
                {
                    var subjCurve = ServiceACAD.CropClosedCurveService.CreateCurveSelection(subjId);
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

                // ── 步骤 3: 精确裁剪运算（委托到 CropClosedCurveService）──────
                var result = ServiceACAD.CropClosedCurveService.CropClosedCurveMulti(
                    subjectCurves, curveB, keepInside.Value);
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
                var curveB = ServiceACAD.CropClosedCurveService.CreateCurveSelection(idB);
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

                var subjectCurves = new List<ServiceACAD.CropClosedCurveService.CurveSelection>();
                var subjectIds = new List<ObjectId>();
                foreach (var id in allCurveIds)
                {
                    var subjCurve = ServiceACAD.CropClosedCurveService.CreateCurveSelection(id);
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
                var result = ServiceACAD.CropClosedCurveService.CropClosedCurveMulti(
                    subjectCurves, curveB, keepInside.Value);
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
