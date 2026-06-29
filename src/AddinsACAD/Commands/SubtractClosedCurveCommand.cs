using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    ///     - 不相交 → 返回 A
    ///     - A 包含 B → 返回 A 减去 B 区域后的剩余部分
    ///     - B 包含 A → 无结果（A 完全被减掉）
    ///     - 相交 → 返回 A 除掉交集部分的封闭多边形
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

                // ── 步骤 3: 计算差集 A \ B（带来源标记）───────────────────────
                //   ClipPolygonWithSources(subject=A, clip=B, keepInside=false)
                //   返回 subject(A) 在 clip(B) 外部的部分 = A \ B
                var clipper = new PolygonClipperService();
                var differenceWithSources = clipper.ClipPolygonWithSources(polyA, polyB, keepInside: false);

                bool noResult = (differenceWithSources == null || differenceWithSources.Count == 0);

                // ── 步骤 4: 混合绘制结果多边形 ──────────────────────────────
                //   - 来自 Subject（曲线 A）的段 → 用 CurveFit
                //   - 来自 Clip（折线 B）的段 → 保持折线
                bool isCurveA = curveA.Type != "Polyline";
                bool isCurveB = curveB.Type != "Polyline";

                if (!noResult)
                {
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var clippedPoly in differenceWithSources)
                        {
                            if (clippedPoly == null || clippedPoly.IsEmpty) continue;

                            int vertexCount = DrawMixedPolygon(ts, clippedPoly, isCurveA, isCurveB, 3);
                            resultPolyCount++;
                            totalVertices += vertexCount;
                        }
                    });
                }

                isSuccess = resultPolyCount > 0;
                stopwatch.Stop();

                // ── 步骤 5: 输出命令行信息 ──────────────────────────────────
                if (isSuccess)
                {
                    string outputType = (isCurveA || isCurveB) ? "混合" : "折线";
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭多边形，{outputType} {totalVertices}");
                }
                else if (noResult)
                {
                    ed.WriteMessage("\n无结果（B 包含 A，A 被完全减去）。");
                }
                else
                {
                    ed.WriteMessage("\n差集绘制失败（几何计算成功，但绘制时发生异常）。");
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
        ///     绘制曲线拟合的闭合多边形（在当前空间）.
        ///     用于全曲线类型（如曲线完全包含的场景）.
        ///     返回顶点数.
        /// </summary>
        private static int DrawPolygonCurveFit(
            ITransactionService ts, IReadOnlyList<CorePoint2D> loop, int colorIndex)
        {
            var poly2d = new Polyline2d
            {
                PolyType = Poly2dType.SimplePoly,
                Closed = true,
                ColorIndex = colorIndex
            };
            if (ts.AppendEntityToCurrentSpace(poly2d).IsNull)
                return 0;
            foreach (var pt in loop)
            {
                var vertex = new Vertex2d(
                    new Point3d(pt.X, pt.Y, 0.0), 0.0, 0.0, 0.0, 0.0);
                poly2d.AppendVertex(vertex);
                ts.AddNewlyCreatedDBObject(vertex, true);
            }
            poly2d.CurveFit();
            try { ts.Style.GetOrCreateLayer("Intersection"); poly2d.Layer = "Intersection"; }
            catch { }
            return Math.Max(1, loop.Count / 4);
        }

        /// <summary>
        ///     混合绘制：先对曲线段分别 CurveFit，再 JOIN 所有段并 CLOSE.
        ///     返回总顶点数.
        /// </summary>
        private static int DrawMixedPolygon(
            ITransactionService ts, ClippedPolygonWithSources clippedPoly,
            bool isCurveSubject, bool isCurveClip, int colorIndex)
        {
            // 全是折线段 → 直接绘制闭合 Polyline
            bool hasCurveSegment = false;
            foreach (var seg in clippedPoly.Segments)
            {
                if ((seg.Source == SegmentSource.Clip && isCurveClip) ||
                    (seg.Source == SegmentSource.Subject && isCurveSubject))
                { hasCurveSegment = true; break; }
            }
            if (!hasCurveSegment)
                return DrawPolygonPlain(ts, clippedPoly.Vertices, colorIndex);

            // 逐段收集顶点+凸度，跳过段间重复端点
            var segmentIds = new List<ObjectId>();
            int segIndex = 0;
            foreach (var seg in clippedPoly.Segments)
            {
                if (seg.Vertices.Count < 2) continue;
                bool isCurve = (seg.Source == SegmentSource.Clip && isCurveClip)
                    || (seg.Source == SegmentSource.Subject && isCurveSubject);

                ObjectId segId;
                if (isCurve)
                    segId = CreateCurveFitSegment(ts, seg.Vertices, colorIndex, segIndex);
                else
                    segId = CreateStraightSegment(ts, seg.Vertices, colorIndex, segIndex);

                if (!segId.IsNull)
                    segmentIds.Add(segId);
                segIndex++;
            }

            if (segmentIds.Count == 0) return 0;
            if (segmentIds.Count == 1)
            {
                ClosePolylineById(ts, segmentIds[0]);
                return clippedPoly.Vertices.Count;
            }

            // ── 第二步：JOIN 所有段到第一个 ────────────────────────────
            var firstPline = ts.GetObject<Polyline>(segmentIds[0], OpenMode.ForWrite);
            if (firstPline == null) return 0;

            // 诊断：记录第一个 Polyline 的首尾端点
            Logger._.Debug($"DrawMixedPolygon JOIN 开始 — 共 {segmentIds.Count} 段");
            if (firstPline.NumberOfVertices > 0)
            {
                var firstStart = firstPline.GetPoint3dAt(0);
                var firstEnd = firstPline.GetPoint3dAt(firstPline.NumberOfVertices - 1);
                Logger._.Debug($"  Seg[0] firstPline Start={firstStart}, End={firstEnd}, VtxCount={firstPline.NumberOfVertices}");
            }

            for (int i = 1; i < segmentIds.Count; i++)
            {
                int vtxBefore = firstPline.NumberOfVertices;

                // 诊断：JOIN 前检查连接端点
                if (firstPline.NumberOfVertices > 0)
                {
                    var segEntity = ts.GetObject<Entity>(segmentIds[i], OpenMode.ForRead);
                    if (segEntity is Polyline nextPlineRO && nextPlineRO.NumberOfVertices > 0)
                    {
                        var firstEnd = firstPline.GetPoint3dAt(firstPline.NumberOfVertices - 1);
                        var nextStart = nextPlineRO.GetPoint3dAt(0);
                        var nextEnd = nextPlineRO.GetPoint3dAt(nextPlineRO.NumberOfVertices - 1);
                        double distStart = firstEnd.DistanceTo(nextStart);
                        double distEnd = firstEnd.DistanceTo(nextEnd);
                        Logger._.Debug($"  JOIN[{i}] firstPline.End={firstEnd} ↔ seg.Start={nextStart} dist={distStart:F6}");
                        Logger._.Debug($"  JOIN[{i}] firstPline.End={firstEnd} ↔ seg.End={nextEnd} dist={distEnd:F6}");
                    }
                }

                // 重新以写模式获取段实体并 JOIN
                var segEntityWrite = ts.GetObject<Entity>(segmentIds[i], OpenMode.ForWrite);
                if (segEntityWrite != null)
                {
                    firstPline.JoinEntity(segEntityWrite);

                    int vtxAfter = firstPline.NumberOfVertices;
                    bool joined = (vtxAfter > vtxBefore);
                    Logger._.Debug($"  JOIN[{i}] 结果: VtxBefore={vtxBefore}, VtxAfter={vtxAfter}, Joined={joined}");

                    // JOIN 成功 → 删除被合并的段实体，否则残留
                    if (joined)
                        segEntityWrite.Erase();
                }
                else
                {
                    Logger._.Debug($"  JOIN[{i}] 结果: segEntityWrite 为 null，跳过");
                }
            }

            // ── 第三步：CLOSE ──────────────────────────────────────────
            if (firstPline.NumberOfVertices > 0)
            {
                var closeStart = firstPline.GetPoint3dAt(0);
                var closeEnd = firstPline.GetPoint3dAt(firstPline.NumberOfVertices - 1);
                double closeDist = closeStart.DistanceTo(closeEnd);
                Logger._.Debug($"  CLOSE前: Start={closeStart}, End={closeEnd}, Gap={closeDist:F6}, VtxCount={firstPline.NumberOfVertices}");
            }
            firstPline.Closed = true;

            if (firstPline.NumberOfVertices > 0)
            {
                Logger._.Debug($"  CLOSE后: Closed={firstPline.Closed}, VtxCount={firstPline.NumberOfVertices}");
            }

            return clippedPoly.Vertices.Count;
        }

        /// <summary>
        ///     创建曲线拟合段：Polyline2d → CurveFit → 转为 Polyline.
        /// </summary>
        private static ObjectId CreateCurveFitSegment(
            ITransactionService ts, List<CorePoint2D> vertices, int colorIndex, int segIndex = -1)
        {
            // CurveFit() 要求至少 3 个顶点，否则抛 eNotApplicable。
            // 顶点不足时退化为直线段，避免异常导致整个事务 Abort。
            if (vertices == null || vertices.Count < 1) return ObjectId.Null;
            if (vertices.Count < 3)
            {
                Logger._.Debug($"  CreateCurveFitSegment[{segIndex}] 顶点数={vertices.Count}<3，退化为直线段");
                return CreateStraightSegment(ts, vertices, colorIndex, segIndex);
            }

            var poly2d = new Polyline2d
            {
                PolyType = Poly2dType.SimplePoly,
                Closed = false,
                ColorIndex = colorIndex
            };
            var poly2dId = ts.AppendEntityToCurrentSpace(poly2d);
            if (poly2dId.IsNull) return ObjectId.Null;

            // 诊断：记录输入顶点
            Logger._.Debug($"  CreateCurveFitSegment[{segIndex}] 输入顶点数={vertices.Count}, First=({vertices[0].X:F6},{vertices[0].Y:F6}), Last=({vertices[vertices.Count - 1].X:F6},{vertices[vertices.Count - 1].Y:F6})");

            foreach (var pt in vertices)
            {
                var vertex = new Vertex2d(
                    new Point3d(pt.X, pt.Y, 0.0), 0.0, 0.0, 0.0, 0.0);
                poly2d.AppendVertex(vertex);
                ts.AddNewlyCreatedDBObject(vertex, true);
            }
            poly2d.CurveFit();

            // 诊断：读取 CurveFit 后的 Polyline2d 顶点（转换前）
            var fitVerts = new List<(Point3d Pos, double Bulge)>();
            foreach (ObjectId vid in poly2d)
            {
                var v2d = ts.GetObject<Vertex2d>(vid);
                if (v2d != null)
                    fitVerts.Add((v2d.Position, v2d.Bulge));
            }
            if (fitVerts.Count > 0)
            {
                Logger._.Debug($"  CreateCurveFitSegment[{segIndex}] CurveFit后顶点数={fitVerts.Count}, First=({fitVerts[0].Pos.X:F6},{fitVerts[0].Pos.Y:F6}) bulge={fitVerts[0].Bulge:F6}, Last=({fitVerts[fitVerts.Count - 1].Pos.X:F6},{fitVerts[fitVerts.Count - 1].Pos.Y:F6}) bulge={fitVerts[fitVerts.Count - 1].Bulge:F6}");
                // 诊断端点偏移
                double firstDelta = Math.Abs(fitVerts[0].Pos.X - vertices[0].X) + Math.Abs(fitVerts[0].Pos.Y - vertices[0].Y);
                double lastDelta = Math.Abs(fitVerts[fitVerts.Count - 1].Pos.X - vertices[vertices.Count - 1].X)
                    + Math.Abs(fitVerts[fitVerts.Count - 1].Pos.Y - vertices[vertices.Count - 1].Y);
                Logger._.Debug($"  CreateCurveFitSegment[{segIndex}] 端点偏移: FirstDelta={firstDelta:F6}, LastDelta={lastDelta:F6}");
            }

            // 读取 CurveFit 后的顶点和凸度，转为 Polyline
            var resultPline = new Polyline();
            resultPline.SetDatabaseDefaults();
            resultPline.ColorIndex = colorIndex;
            int idx = 0;
            foreach (var fv in fitVerts)
            {
                resultPline.AddVertexAt(idx,
                    new Point2d(fv.Pos.X, fv.Pos.Y),
                    fv.Bulge, 0.0, 0.0);
                idx++;
            }

            // 诊断：转换后 Polyline 端点
            if (resultPline.NumberOfVertices > 0)
            {
                var plStart = resultPline.GetPoint2dAt(0);
                var plEnd = resultPline.GetPoint2dAt(resultPline.NumberOfVertices - 1);
                Logger._.Debug($"  CreateCurveFitSegment[{segIndex}] 转换后Polyline: VtxCount={resultPline.NumberOfVertices}, Start=({plStart.X:F6},{plStart.Y:F6}), End=({plEnd.X:F6},{plEnd.Y:F6})");
            }

            // 删除中间 Polyline2d，保留 Polyline
            var p2dToErase = ts.GetObject<Polyline2d>(poly2dId, OpenMode.ForWrite);
            if (p2dToErase != null) p2dToErase.Erase();
            var plineId = ts.AppendEntityToCurrentSpace(resultPline);
            try { ts.Style.GetOrCreateLayer("Intersection"); resultPline.Layer = "Intersection"; }
            catch { }
            return plineId;
        }

        /// <summary>
        ///     创建直线段 Polyline（不闭合）.
        /// </summary>
        private static ObjectId CreateStraightSegment(
            ITransactionService ts, List<CorePoint2D> vertices, int colorIndex, int segIndex = -1)
        {
            var pline = new Polyline();
            pline.SetDatabaseDefaults();
            pline.ColorIndex = colorIndex;
            for (int i = 0; i < vertices.Count; i++)
                pline.AddVertexAt(i,
                    new Point2d(vertices[i].X, vertices[i].Y), 0.0, 0.0, 0.0);

            // 诊断：记录直线段端点
            Logger._.Debug($"  CreateStraightSegment[{segIndex}] VtxCount={pline.NumberOfVertices}, Start=({vertices[0].X:F6},{vertices[0].Y:F6}), End=({vertices[vertices.Count - 1].X:F6},{vertices[vertices.Count - 1].Y:F6})");

            try { ts.Style.GetOrCreateLayer("Intersection"); pline.Layer = "Intersection"; }
            catch { }
            return ts.AppendEntityToCurrentSpace(pline);
        }

        /// <summary>
        ///     通过 ObjectId 将 Polyline 闭合.
        /// </summary>
        private static void ClosePolylineById(ITransactionService ts, ObjectId plineId)
        {
            var pline = ts.GetObject<Polyline>(plineId, OpenMode.ForWrite);
            if (pline != null)
                pline.Closed = true;
        }

        // 已移除未使用的 ReadCurveFitVertices 和 ReadStraightVertices 方法
        /// <summary>
        ///     绘制普通折线多边形（在当前空间）.
        ///     用于两个都是折线类型的情况，保持折线特性.
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