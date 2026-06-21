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

[assembly: CommandClass(typeof(AddinsACAD.Commands.SubtractClosedCurveCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     SUBTRACTCLOSEDCURVE — 选择封闭曲线 A，再选择封闭曲线 B，
    ///     计算 A \ B（曲线 A 减去 A 与 B 的交集）并绘制结果多边形.
    ///     支持 Polyline、Circle、Ellipse、Spline.
    ///     - 不相交 → 返回 A（A 与 B 无交集，A 不变）
    ///     - A 包含 B → 返回 A 减去 B 区域后的剩余部分
    ///     - B 包含 A → 无结果（A 全部在 B 内部，A 被完全减去）
    ///     - A 与 B 相交 → 返回 A 除掉交集部分的封闭多边形
    /// </summary>
    public class SubtractClosedCurveCommand
    {
        [CommandMethod("SUBTRACTCLOSEDCURVE")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var stopwatch = Stopwatch.StartNew();
                string uid = "";
                var isSuccess = false;
                int resultPolyCount = 0;
                int totalVertices = 0;

                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                // ── 步骤 1: 选择曲线 A（SUBTRACT 曲线）───────────────────
                var curveA = this.SelectClosedCurve(ed, "A", out var idA);
                if (curveA == null) return;
                var polyA = curveA.Polygon;

                // ── 步骤 2: 选择曲线 B（被 SUBTRACT 曲线）─────────────────
                var curveB = this.SelectClosedCurve(ed, "B", out var idB);
                if (curveB == null) return;
                var polyB = curveB.Polygon;

                // ── 步骤 3: 计算差集 A \ B ──────────────────────────────
                //   ClipPolygon(A, B, keepInside=false) = A 中在 B 外部的部分
                var clipper = new PolygonClipperService();
                var difference = clipper.ClipPolygon(polyA, polyB, keepInside: false);

                bool noResult = (difference == null || difference.Count == 0);

                // ── 步骤 4: 绘制结果多边形 ──────────────────────────────
                if (!noResult)
                {
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var poly in difference)
                        {
                            if (poly == null || poly.Count < 3) continue;

                            int vertexCount = DrawPolygonPlain(ts, poly, 3);
                            resultPolyCount++;
                            totalVertices += vertexCount;
                        }
                    });
                }

                isSuccess = resultPolyCount > 0;
                stopwatch.Stop();

                // ── 步骤 5: 输出命令行信息 ──────────────────────────────
                if (isSuccess)
                {
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭多边形，{totalVertices} 顶点");
                }
                else
                {
                    ed.WriteMessage("\n无结果（B 包含 A，A 被完全减去）。");
                }

                // ── 步骤 6: TestRecorder 记录 ────────────────────────────
                try
                {
                    var record = new CropTestRecord
                    {
                        Command = "SUBTRACTCLOSEDCURVE",
                        Direction = "Difference",
                        IsSuccess = isSuccess,
                        UcsOrigin = ucsO,
                        UcsXAxis = ucsX,
                        UcsYAxis = ucsY,
                        BoundaryVertices = new List<Point2D>(polyA),
                        BoundaryVertexCount = polyA.Count,
                        TotalEntityCount = 2,
                        KeptCount = resultPolyCount,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Entities = new List<CropEntitySnapshot>
                        {
                            CreateSnapshot(idA.ToString(), curveA.Type, polyA),
                            CreateSnapshot(idB.ToString(), curveB.Type, polyB),
                        },
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

        // ──────────────────────────────────────────────────────────────

        /// <summary>曲线选择结果.</summary>
        private sealed class CurveSelection
        {
            public string Type;          // "Polyline"/"Circle"/"Ellipse"/"Spline"
            public List<CorePoint2D> Polygon;
        }

        /// <summary>
        ///     选择单条闭合曲线并转换为多边形.
        /// </summary>
        private CurveSelection SelectClosedCurve(Editor ed, string label, out ObjectId id)
        {
            id = ObjectId.Null;
            try
            {
                var opt = new PromptEntityOptions($"\n选择闭合曲线 {label}: ");
                opt.SetRejectMessage($"\n请选择闭合的 Polyline/Circle/Ellipse/Spline 作为曲线 {label}。");
                opt.AddAllowedClass(typeof(Curve), false);
                var res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage($"\n未选择曲线 {label}。");
                    return null;
                }

                id = res.ObjectId;
                var capturedId = id;
                CurveSelection sel = null;

                CadServiceManager._.ExecuteInTransactions(null, ts =>
                {
                    var curve = ts.GetObject<Curve>(capturedId, OpenMode.ForRead);
                    if (curve == null || !curve.Closed)
                    {
                        ed.WriteMessage($"\n曲线 {label} 未闭合。");
                        return;
                    }

                    var polygon = CurveConverter.ConvertToPolygon(curve);
                    if (polygon == null || polygon.Count < 3)
                    {
                        ed.WriteMessage($"\n曲线 {label} 转换失败（顶点 < 3）。");
                        return;
                    }

                    string type = curve.GetType().Name;
                    sel = new CurveSelection { Type = type, Polygon = polygon };
                });

                if (sel == null) id = ObjectId.Null;
                return sel;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择曲线 {label} 失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        ///     绘制普通折线多边形（在当前空间）.
        ///     返回顶点数.
        /// </summary>
        private static int DrawPolygonPlain(
            ITransactionService ts, IReadOnlyList<CorePoint2D> loop, int colorIndex)
        {
            var pline = new Polyline();
            pline.SetDatabaseDefaults();

            foreach (var pt in loop)
                pline.AddVertexAt(pline.NumberOfVertices,
                    new Point2d(pt.X, pt.Y), 0.0, 0.0, 0.0);

            pline.Closed = true;
            pline.ColorIndex = colorIndex;

            // 确保 Intersection 图层存在，避免 eKeyNotFound
            try
            {
                ts.Style.GetOrCreateLayer("Intersection");
                pline.Layer = "Intersection";
            }
            catch
            {
                // 图层创建失败时继续使用当前图层，不阻断绘制
            }

            ts.AppendEntityToCurrentSpace(pline);

            return loop.Count;
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