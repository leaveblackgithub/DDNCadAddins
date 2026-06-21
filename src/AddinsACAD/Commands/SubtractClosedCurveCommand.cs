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

                // ── 步骤 1: 选择曲线 A ─────────────────────────────────
                var resA = ed.GetEntity(new PromptEntityOptions("\n选择闭合曲线 A: "));
                if (resA.Status != PromptStatus.OK) return;
                var idA = resA.ObjectId;

                // ── 步骤 2: 选择曲线 B ─────────────────────────────────
                var resB = ed.GetEntity(new PromptEntityOptions("\n选择闭合曲线 B: "));
                if (resB.Status != PromptStatus.OK) return;
                var idB = resB.ObjectId;

                // ── 步骤 3: 转换曲线 ──────────────────────────────────
                List<CorePoint2D> polyA = null, polyB = null;
                string typeA = "", typeB = "";

                CadServiceManager._.ExecuteInTransactions(null, ts =>
                {
                    var curveA = ts.GetObject<Curve>(idA, OpenMode.ForRead);
                    var curveB = ts.GetObject<Curve>(idB, OpenMode.ForRead);
                    if (curveA == null || curveB == null) return;
                    typeA = curveA.GetType().Name;
                    typeB = curveB.GetType().Name;
                    if (!curveA.Closed || !curveB.Closed) return;
                    polyA = CurveConverter.ConvertToPolygon(curveA);
                    polyB = CurveConverter.ConvertToPolygon(curveB);
                });

                if (polyA == null || polyB == null || polyA.Count < 3 || polyB.Count < 3)
                {
                    ed.WriteMessage("\n曲线转换失败。");
                    return;
                }

                // ── 步骤 4: 计算差集 A \ B ────────────────────────────
                var clipper = new PolygonClipperService();

                // 4a. 先求交集，用于判断包含关系
                var intersection = clipper.ClipPolygon(polyA, polyB, keepInside: true);
                bool hasInter = (intersection != null && intersection.Count > 0);

                IReadOnlyList<IReadOnlyList<Point2D>> resultPolygons;

                if (!hasInter)
                {
                    // 不相交 → 返回 A
                    resultPolygons = new[] { polyA };
                }
                else
                {
                    // 4b. 直接求差集
                    var diff = clipper.ClipPolygon(polyA, polyB, keepInside: false);
                    if (diff != null && diff.Count > 0)
                    {
                        resultPolygons = diff;

                        // 检查是否是挖孔回退（差集返回了完整 A，但 B 在 A 内）
                        bool lookLikeFallback = (diff.Count == 1 && diff[0].Count == polyA.Count);
                        bool bInsideA = IsPolygonInside(polyB, polyA);

                        if (lookLikeFallback && bInsideA)
                        {
                            // B 完全在 A 内部 → 构造挖孔多边形
                            resultPolygons = new[] {
                                CreateHolePolygon(polyA, polyB)
                            };
                        }
                    }
                    else
                    {
                        // 差集为空 → B 包含 A
                        resultPolygons = Array.Empty<IReadOnlyList<Point2D>>();
                    }
                }

                // ── 步骤 5: 绘制结果 ─────────────────────────────────
                if (resultPolygons.Count > 0)
                {
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var poly in resultPolygons)
                        {
                            if (poly == null || poly.Count < 3) continue;

                            var pline = new Polyline();
                            pline.SetDatabaseDefaults();
                            pline.Closed = true;
                            pline.ColorIndex = 3;

                            for (int i = 0; i < poly.Count; i++)
                                pline.AddVertexAt(i,
                                    new Point2d(poly[i].X, poly[i].Y), 0.0, 0.0, 0.0);

                            try
                            {
                                ts.Style.GetOrCreateLayer("Intersection");
                                pline.Layer = "Intersection";
                            }
                            catch { }

                            ts.AppendEntityToCurrentSpace(pline);
                            resultPolyCount++;
                            totalVertices += poly.Count;
                        }
                    });
                }

                isSuccess = resultPolyCount > 0;
                stopwatch.Stop();

                // ── 步骤 6: 输出 ──────────────────────────────────────
                if (isSuccess)
                {
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭多边形，{totalVertices} 顶点");
                }
                else
                {
                    ed.WriteMessage("\n无结果（B 包含 A，A 被完全减去）。");
                }

                // ── 步骤 7: TestRecorder ──────────────────────────────
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
                            CreateSnapshot(idA.ToString(), typeA, polyA),
                            CreateSnapshot(idB.ToString(), typeB, polyB),
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

        /// <summary>
        ///     判断 inner 多边形是否完全在 outer 多边形内部.
        /// </summary>
        private static bool IsPolygonInside(
            IReadOnlyList<CorePoint2D> inner,
            IReadOnlyList<CorePoint2D> outer)
        {
            foreach (var pt in inner)
                if (!IsPointInPolygon(new Point2D(pt.X, pt.Y), outer))
                    return false;
            return true;
        }

        /// <summary>
        ///     射线法判断点是否在闭合多边形内部.
        /// </summary>
        private static bool IsPointInPolygon(Point2D point, IReadOnlyList<CorePoint2D> polygon)
        {
            int count = polygon.Count;
            if (count < 3) return false;
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var pi = new Point2D(polygon[i].X, polygon[i].Y);
                var pj = new Point2D(polygon[j].X, polygon[j].Y);
                var cross = (pi.X - pj.X) * (point.Y - pj.Y) - (pi.Y - pj.Y) * (point.X - pj.X);
                if (Math.Abs(cross) < 1e-12)
                {
                    var dot = (point.X - pj.X) * (pi.X - pj.X) + (point.Y - pj.Y) * (pi.Y - pj.Y);
                    var lenSq = (pi.X - pj.X) * (pi.X - pj.X) + (pi.Y - pj.Y) * (pi.Y - pj.Y);
                    if (lenSq > 0 && dot >= 0 && dot <= lenSq) return true;
                }
                if ((pi.Y > point.Y) != (pj.Y > point.Y))
                {
                    var t = (point.X - pj.X) - (pi.X - pj.X) * (point.Y - pj.Y) / (pi.Y - pj.Y);
                    if (t < 1e-12) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        ///     构造挖孔多边形：A 外环 + B 反向遍历.
        ///     路径：A[0..n-1] → A[0] → B[0] → B[m-1..0] → B[0] → A[0]
        /// </summary>
        private static IReadOnlyList<Point2D> CreateHolePolygon(
            IReadOnlyList<CorePoint2D> outer,
            IReadOnlyList<CorePoint2D> inner)
        {
            var result = new List<Point2D>();

            // A 外环（完整一圈）
            foreach (var pt in outer)
                result.Add(new Point2D(pt.X, pt.Y));
            // 闭合到 A[0]
            result.Add(new Point2D(outer[0].X, outer[0].Y));

            // 桥接到 B[0]
            result.Add(new Point2D(inner[0].X, inner[0].Y));

            // B 反向遍历（形成孔洞）
            for (int k = inner.Count - 1; k >= 0; k--)
                result.Add(new Point2D(inner[k].X, inner[k].Y));

            // 闭合到 B[0]
            result.Add(new Point2D(inner[0].X, inner[0].Y));

            // 桥接回 A[0]
            result.Add(new Point2D(outer[0].X, outer[0].Y));

            return result;
        }

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