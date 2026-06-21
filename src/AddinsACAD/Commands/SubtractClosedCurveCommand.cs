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
    ///     在侧数据库中用 Region 布尔运算做精确差集，结果采样为折线绘制.
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
                var db = doc.Database;
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

                // ── 步骤 3: 用侧数据库做 Region 布尔差集 ─────────────
                var resultPoints = new List<List<CorePoint2D>>();

                using (var sideDb = new Database(false, true))
                {
                    // 克隆曲线到侧数据库
                    ObjectId cloneAId, cloneBId;
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var curveA = tr.GetObject(idA, OpenMode.ForRead) as Curve;
                        var curveB = tr.GetObject(idB, OpenMode.ForRead) as Curve;
                        if (curveA == null || curveB == null || !curveA.Closed || !curveB.Closed)
                        {
                            ed.WriteMessage("\n曲线无效或未闭合。");
                            return;
                        }

                        var typeA = curveA.GetType().Name;
                        var typeB = curveB.GetType().Name;

                        using (var sideTr = sideDb.TransactionManager.StartTransaction())
                        {
                            var btr = (BlockTableRecord)sideTr.GetObject(
                                sideDb.CurrentSpaceId, OpenMode.ForWrite);

                            var cloneA = curveA.Clone() as Curve;
                            var cloneB = curveB.Clone() as Curve;
                            if (cloneA == null || cloneB == null)
                            {
                                ed.WriteMessage("\n克隆曲线失败。");
                                return;
                            }
                            cloneAId = btr.AppendEntity(cloneA);
                            sideTr.AddNewlyCreatedDBObject(cloneA, true);
                            cloneBId = btr.AppendEntity(cloneB);
                            sideTr.AddNewlyCreatedDBObject(cloneB, true);
                            sideTr.Commit();
                        }
                        tr.Commit();
                    }

                    // 做 Region 布尔运算
                    using (var sideTr = sideDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            var cloneA = sideTr.GetObject(cloneAId, OpenMode.ForRead) as Curve;
                            var cloneB = sideTr.GetObject(cloneBId, OpenMode.ForRead) as Curve;
                            if (cloneA == null || cloneB == null)
                            {
                                ed.WriteMessage("\n侧数据库读取失败。");
                                return;
                            }

                            // 创建 Region
                            var colA = new DBObjectCollection();
                            colA.Add(cloneA);
                            var regionsA = Region.CreateFromCurves(colA);
                            var colB = new DBObjectCollection();
                            colB.Add(cloneB);
                            var regionsB = Region.CreateFromCurves(colB);

                            if (regionsA == null || regionsA.Count == 0 ||
                                regionsB == null || regionsB.Count == 0)
                            {
                                ed.WriteMessage("\nRegion 创建失败。");
                                return;
                            }

                            var regA = sideTr.GetObject(regionsA[0].ObjectId, OpenMode.ForWrite) as Region;
                            var regB = sideTr.GetObject(regionsB[0].ObjectId, OpenMode.ForWrite) as Region;
                            if (regA == null || regB == null)
                            {
                                ed.WriteMessage("\n无法打开 Region。");
                                return;
                            }

                            // 布尔差集 regA = regA - regB
                            try
                            {
                                regA.BooleanOperation(
                                    BooleanOperationType.BoolSubtract, regB);
                            }
                            catch { }

                            // 炸开得到临时曲线对象
                            var resultCurves = new DBObjectCollection();
                            regA.Explode(resultCurves);

                            if (resultCurves.Count == 0)
                            {
                                ed.WriteMessage("\n无结果（A 被完全减去）。");
                                return;
                            }

                            // 采样曲线顶点
                            foreach (DBObject obj in resultCurves)
                            {
                                if (obj is Curve curve && (curve.Closed || curve is Circle))
                                {
                                    var pts = CurveConverter.ConvertToPolygon(curve);
                                    if (pts != null && pts.Count >= 3)
                                        resultPoints.Add(new List<CorePoint2D>(pts));
                                }
                            }

                            // 清理侧数据库临时对象
                            sideTr.Abort();
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\nRegion 差集运算失败: {ex.Message}");
                            Logger._.Error($"Region 差集失败: {ex.Message}", ex);
                            return;
                        }
                    }
                }

                // ── 步骤 4: 在主数据库中绘制结果 ────────────────────
                if (resultPoints.Count == 0)
                {
                    ed.WriteMessage("\n无结果（B 包含 A 或布尔运算失败）。");
                    stopwatch.Stop();
                    return;
                }

                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    foreach (var pts in resultPoints)
                    {
                        if (pts.Count < 3) continue;

                        var pline = new Polyline();
                        pline.SetDatabaseDefaults();
                        foreach (var pt in pts)
                            pline.AddVertexAt(pline.NumberOfVertices,
                                new Point2d(pt.X, pt.Y), 0.0, 0.0, 0.0);
                        pline.Closed = true;
                        pline.ColorIndex = 3;
                        try
                        {
                            ts.Style.GetOrCreateLayer("Intersection");
                            pline.Layer = "Intersection";
                        }
                        catch { }
                        ts.AppendEntityToCurrentSpace(pline);
                        resultPolyCount++;
                        totalVertices += pts.Count;
                    }
                });

                stopwatch.Stop();

                // ── 步骤 5: 输出命令行信息 ──────────────────────────
                if (resultPolyCount > 0)
                {
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭多边形，{totalVertices} 顶点");
                }
                else
                {
                    ed.WriteMessage("\n无结果。");
                }

                // ── 步骤 6: TestRecorder 记录 ────────────────────────
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