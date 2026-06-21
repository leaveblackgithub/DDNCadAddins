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
    ///     计算 A \ B（曲线 A 减去 A 与 B 的交集）并绘制结果多边形.
    ///     使用 AutoCAD Region 布尔运算实现精确差集.
    ///     - 不相交 → 返回 A
    ///     - A 包含 B → 返回 A 减去 B 区域后的剩余部分（环形）
    ///     - B 包含 A → 无结果（A 完全被减掉）
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
                var db = doc.Database;
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

                // ── 步骤 3: 用 Region 布尔运算做差集 ───────────────────
                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    // 读原始曲线
                    var curveA = ts.GetObject<Curve>(idA, OpenMode.ForRead);
                    var curveB = ts.GetObject<Curve>(idB, OpenMode.ForRead);
                    if (curveA == null || curveB == null)
                    {
                        ed.WriteMessage("\n无法读取曲线。");
                        return;
                    }

                    // 克隆曲线到当前空间（Region.CreateFromCurves 需要 DB 驻留）
                    var cloneA = curveA.Clone() as Curve;
                    var cloneB = curveB.Clone() as Curve;
                    if (cloneA == null || cloneB == null)
                    {
                        ed.WriteMessage("\n克隆曲线失败。");
                        return;
                    }
                    ts.AppendEntityToCurrentSpace(cloneA);
                    ts.AppendEntityToCurrentSpace(cloneB);

                    // 创建 Region
                    var dbColA = new DBObjectCollection();
                    dbColA.Add(cloneA);
                    var regionsA = Region.CreateFromCurves(dbColA);

                    var dbColB = new DBObjectCollection();
                    dbColB.Add(cloneB);
                    var regionsB = Region.CreateFromCurves(dbColB);

                    if (regionsA == null || regionsA.Count == 0 ||
                        regionsB == null || regionsB.Count == 0)
                    {
                        ed.WriteMessage("\n无法从曲线创建 Region。");
                        return;
                    }

                    var regionA = regionsA[0] as Region;
                    var regionB = regionsB[0] as Region;
                    if (regionA == null || regionB == null)
                    {
                        ed.WriteMessage("\nRegion 创建失败。");
                        return;
                    }

                    // 重新打开 Region 为写模式
                    var regA = ts.GetObject<Region>(regionA.ObjectId, OpenMode.ForWrite);
                    var regB = ts.GetObject<Region>(regionB.ObjectId, OpenMode.ForWrite);
                    if (regA == null || regB == null)
                    {
                        ed.WriteMessage("\n无法打开 Region。");
                        return;
                    }

                    // 执行布尔差集
                    try
                    {
                        regA.BooleanOperation(
                            BooleanOperationType.BoolSubtract, regB);
                    }
                    catch
                    {
                        // 不相交：regA 保持不变
                    }

                    // 炸开结果 Region → 临时曲线对象
                    var resultCurves = new DBObjectCollection();
                    regA.Explode(resultCurves);

                    // 收集所有闭合曲线，加到当前空间
                    foreach (DBObject obj in resultCurves)
                    {
                        if (obj is Curve c && c.Closed)
                        {
                            var id = ts.AppendEntityToCurrentSpace(c);
                            if (id.IsNull)
                            {
                                c.Dispose();
                                continue;
                            }
                            try
                            {
                                ts.Style.GetOrCreateLayer("Intersection");
                                c.Layer = "Intersection";
                            }
                            catch { }
                            c.ColorIndex = 3;

                            resultPolyCount++;
                            if (c is Polyline pl)
                                totalVertices += pl.NumberOfVertices;
                            else if (c is Circle)
                                totalVertices += 20;
                            else if (c is Ellipse)
                                totalVertices += 30;
                            else
                                totalVertices += 20;
                        }
                        else
                        {
                            obj.Dispose();
                        }
                    }

                    // 清理临时对象（忽略异常）
                    try { ts.GetObject<Entity>(cloneA.ObjectId, OpenMode.ForWrite)?.Erase(); } catch { }
                    try { ts.GetObject<Entity>(cloneB.ObjectId, OpenMode.ForWrite)?.Erase(); } catch { }
                    try { ts.GetObject<Entity>(regionA.ObjectId, OpenMode.ForWrite)?.Erase(); } catch { }
                    try { ts.GetObject<Entity>(regionB.ObjectId, OpenMode.ForWrite)?.Erase(); } catch { }
                });

                isSuccess = resultPolyCount > 0;
                stopwatch.Stop();

                // ── 步骤 4: 输出命令行信息 ──────────────────────────
                if (isSuccess)
                {
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭多边形，{totalVertices} 顶点");
                }
                else
                {
                    ed.WriteMessage("\n无结果（B 包含 A 或布尔运算失败）。");
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

        /// <summary>
        ///     对曲线采样，生成 Polygon 点列表（用于 TestRecorder）.
        /// </summary>
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
                        if (poly != null)
                            pts.AddRange(poly);
                    }
                    tr.Commit();
                }
                return pts;
            }
            catch
            {
                return new List<CorePoint2D>();
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