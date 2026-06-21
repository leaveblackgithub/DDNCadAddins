using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

[assembly: CommandClass(typeof(AddinsACAD.Commands.SubtractClosedCurveCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     SUBTRACTCLOSEDCURVE — 选择封闭曲线 A，再选择封闭曲线 B，
    ///     A \ B（曲线 A 减去 A 与 B 的交集）.
    ///     在同一事务中用 Region 布尔运算做差集.
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

                // ── 步骤 3: Region 布尔差集 + 采样为折线 ────────────
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var curveA = tr.GetObject(idA, OpenMode.ForRead) as Curve;
                        var curveB = tr.GetObject(idB, OpenMode.ForRead) as Curve;
                        if (curveA == null || curveB == null || !curveA.Closed || !curveB.Closed)
                        {
                            ed.WriteMessage("\n曲线无效或未闭合。");
                            return;
                        }

                        // 克隆到当前空间（Region.CreateFromCurves 需要 DB 驻留）
                        var btr = (BlockTableRecord)tr.GetObject(
                            doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                        var cloneA = curveA.Clone() as Curve;
                        var cloneB = curveB.Clone() as Curve;
                        btr.AppendEntity(cloneA);
                        tr.AddNewlyCreatedDBObject(cloneA, true);
                        btr.AppendEntity(cloneB);
                        tr.AddNewlyCreatedDBObject(cloneB, true);

                        // 创建 Region — CreateFromCurves 将曲线消费进 Region
                        var colA = new DBObjectCollection();
                        colA.Add(cloneA);
                        var regionsA = Region.CreateFromCurves(colA);

                        var colB = new DBObjectCollection();
                        colB.Add(cloneB);
                        var regionsB = Region.CreateFromCurves(colB);

                        if (regionsA.Count == 0 || regionsB.Count == 0)
                        {
                            ed.WriteMessage("\nRegion 创建失败。");
                            return;
                        }

                        // regionsA[0] 是刚创建的 Region，已在 DB 中
                        var regA = regionsA[0] as Region;
                        var regB = regionsB[0] as Region;

                        // 布尔差集
                        regA.BooleanOperation(BooleanOperationType.BoolSubtract, regB);

                        // 炸开得结果曲线
                        var resultCurves = new DBObjectCollection();
                        regA.Explode(resultCurves);

                        // 收集结果
                        var ptsList = new List<List<CorePoint2D>>();
                        foreach (DBObject obj in resultCurves)
                        {
                            if (obj is Curve curve && curve.Closed)
                            {
                                var pts = CurveConverter.ConvertToPolygon(curve);
                                if (pts != null && pts.Count >= 3)
                                    ptsList.Add(new List<CorePoint2D>(pts));
                            }
                        }

                        // 擦除临时 Region 和克隆（Region 会留在 DB 中需要清理）
                        regA.Erase();
                        regB.Erase();

                        // 回滚事务（丢弃所有临时对象）
                        tr.Abort();

                        if (ptsList.Count == 0)
                        {
                            ed.WriteMessage("\n无结果（B 包含 A，A 被完全减去）。");
                            return;
                        }

                        // 新事务：仅绘制结果
                        using (var drawTr = doc.Database.TransactionManager.StartTransaction())
                        {
                            var drawBtr = (BlockTableRecord)drawTr.GetObject(
                                doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                            // 确保图层
                            var lt = (LayerTable)drawTr.GetObject(
                                doc.Database.LayerTableId, OpenMode.ForRead);
                            if (!lt.Has("Intersection"))
                            {
                                lt.UpgradeOpen();
                                var ltr = new LayerTableRecord
                                {
                                    Name = "Intersection",
                                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3)
                                };
                                lt.Add(ltr);
                                drawTr.AddNewlyCreatedDBObject(ltr, true);
                            }

                            foreach (var pts in ptsList)
                            {
                                var pline = new Polyline();
                                pline.SetDatabaseDefaults();
                                pline.Closed = true;
                                pline.ColorIndex = 3;
                                pline.Layer = "Intersection";

                                for (int i = 0; i < pts.Count; i++)
                                    pline.AddVertexAt(i,
                                        new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);

                                drawBtr.AppendEntity(pline);
                                drawTr.AddNewlyCreatedDBObject(pline, true);
                                resultPolyCount++;
                                totalVertices += pts.Count;
                            }

                            drawTr.Commit();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nRegion 差集运算失败: {ex.Message}");
                        Logger._.Error($"Region 差集失败: {ex.Message}", ex);
                        try { tr.Abort(); } catch { }
                        return;
                    }
                }

                stopwatch.Stop();

                // ── 步骤 4: 输出命令行信息 ──────────────────────────
                if (resultPolyCount > 0)
                {
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭多边形，{totalVertices} 顶点");
                }
                else
                {
                    ed.WriteMessage("\n无结果。");
                }

                // ── 步骤 5: TestRecorder 记录 ────────────────────────
                try
                {
                    var polyA = SampleCurvePoints(idA);
                    var polyB = SampleCurvePoints(idB);
                    var record = new CropTestRecord
                    {
                        Command = "SUBTRACTCLOSEDCURVE",
                        Direction = "DifferenceRegion",
                        IsSuccess = resultPolyCount > 0,
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
                            CreateSnapshot(idA.ToString(), "CurveA", polyA),
                            CreateSnapshot(idB.ToString(), "CurveB", polyB),
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

        private static List<CorePoint2D> SampleCurvePoints(ObjectId curveId)
        {
            try
            {
                var pts = new List<CorePoint2D>();
                var db = HostApplicationServices.WorkingDatabase;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var curve = tr.GetObject(curveId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        var poly = CurveConverter.ConvertToPolygon(curve);
                        if (poly != null) pts.AddRange(poly);
                    }
                    tr.Commit();
                }
                return pts;
            }
            catch { return new List<CorePoint2D>(); }
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