using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;
using DDNCadAddins.Core.Models;

[assembly: CommandClass(typeof(AddinsACAD.Commands.GenerateHatchBoundaryCommand))]

namespace AddinsACAD.Commands
{
    public class GenerateHatchBoundaryCommand
    {
        [CommandMethod("GENERATEHATCHBOUNDARY")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                var hatchId = this.SelectSingleHatch(ed);
                if (hatchId.IsNull) return;

                int loopCount = 0;
                int entityCount = 0;
                string uid = "";
                string typeLog = "";
                TestRecorder.CaptureUcs(out var ucsO, out var ucsX, out var ucsY);

                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var hatch = ts.GetObject<Hatch>(hatchId, OpenMode.ForRead);
                    if (hatch == null) { ed.WriteMessage("\n无法打开 Hatch。"); return; }

                    var plane = new Plane(
                        Point3d.Origin + hatch.Normal * hatch.Elevation,
                        hatch.Normal);
                    loopCount = hatch.NumberOfLoops;

                    // 曲线型环逐环提取边界（HatchBoundaryExtractor 只处理 Curves，不处理 Polyline 环）
                    var extractor = new HatchBoundaryExtractor();

                    for (int li = 0; li < loopCount; li++)
                    {
                        var loop = hatch.GetLoopAt(li);
                        if (loop == null) continue;

                        bool isOuter = (li == 0);
                        int color = isOuter ? 2 : 4;

                        // ── Polyline 环（内环通常是此种）────────────
                        if (loop.IsPolyline)
                        {
                            var bulgeVerts = loop.Polyline;
                            if (bulgeVerts != null && bulgeVerts.Count > 0)
                            {
                                var poly = new Polyline();
                                foreach (BulgeVertex bv in bulgeVerts)
                                {
                                    var pt3d = plane.EvaluatePoint(bv.Vertex);
                                    poly.AddVertexAt(poly.NumberOfVertices,
                                        new Point2d(pt3d.X, pt3d.Y),
                                        bv.Bulge, 0.0, 0.0);
                                }
                                poly.Closed = true;
                                poly.ColorIndex = color;
                                poly.Layer = hatch.Layer;
                                ts.AppendEntityToCurrentSpace(poly);
                                entityCount++;
                                typeLog += $"PolyV({poly.NumberOfVertices})|";
                                continue;
                            }
                        }

                        // ── 曲线环 ─────────────────────────────────
                        // 尝试检测完整圆
                        if (loop.Curves != null && loop.Curves.Count == 1
                            && loop.Curves[0] is CircularArc2d arc)
                        {
                            double span = Math.Abs(arc.EndAngle - arc.StartAngle);
                            if (span >= Math.PI * 2 - 1e-6)
                            {
                                var center = plane.EvaluatePoint(arc.Center);
                                var circle = new Circle(center, plane.Normal, arc.Radius)
                                {
                                    ColorIndex = color,
                                    Layer = hatch.Layer
                                };
                                ts.AppendEntityToCurrentSpace(circle);
                                entityCount++;
                                typeLog += "Circle|";
                                continue;
                            }
                        }

                        // 尝试检测完整椭圆
                        if (loop.Curves != null && loop.Curves.Count == 1
                            && loop.Curves[0] is EllipticalArc2d ell)
                        {
                            double span = Math.Abs(ell.EndAngle - ell.StartAngle);
                            if (span >= Math.PI * 2 - 1e-6)
                            {
                                var center = plane.EvaluatePoint(ell.Center);
                                // 主轴方向向量（在 hatch 平面内）转换到 WCS
                                var majorDir = plane.EvaluatePoint(ell.Center + ell.MajorAxis * ell.MajorRadius) - center;
                                double ratio = ell.MinorRadius / ell.MajorRadius;
                                var ellipse = new Ellipse(center, plane.Normal, majorDir, ratio, 0.0, Math.PI * 2)
                                {
                                    ColorIndex = color,
                                    Layer = hatch.Layer
                                };
                                ts.AppendEntityToCurrentSpace(ellipse);
                                entityCount++;
                                typeLog += "Ellipse|";
                                continue;
                            }
                        }

                        // NurbCurve2d 环：采样后创建 Polyline2d 并应用 CurveFit 平滑
                        if (loop.Curves != null && loop.Curves.Count == 1
                            && loop.Curves[0] is NurbCurve2d nurb)
                        {
                            int added = CreateCurveFitPolylineFromNurb(ts, nurb, plane, color, hatch.Layer);
                            if (added > 0)
                            {
                                entityCount++;
                                typeLog += $"CurveFit({added}v)|";
                                continue;
                            }
                        }

                        // 其他曲线型环（line/arc/椭圆弧组合）→ 逐环提取并以直线段拟合
                        var loopPts = extractor.ExtractLoopBoundary(loop);
                        if (loopPts != null && loopPts.Count >= 3)
                        {
                            var poly = new Polyline();
                            foreach (var cp in loopPts)
                            {
                                var pt3d = plane.EvaluatePoint(new Point2d(cp.X, cp.Y));
                                poly.AddVertexAt(poly.NumberOfVertices,
                                    new Point2d(pt3d.X, pt3d.Y), 0.0, 0.0, 0.0);
                            }
                            poly.Closed = true;
                            poly.ColorIndex = color;
                            poly.Layer = hatch.Layer;
                            ts.AppendEntityToCurrentSpace(poly);
                            entityCount++;
                            typeLog += $"Poly({loopPts.Count}v)|";
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
                    ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                });

                ed.WriteMessage($"\n生成完成：{loopCount} 个环，{entityCount} 个实体 [{typeLog}]");
            }
            catch (System.Exception ex)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc.Editor.WriteMessage($"\nGENERATEHATCHBOUNDARY 失败: {ex.Message}");
                Logger._.Error($"GENERATEHATCHBOUNDARY 失败: {ex.Message}", ex);
            }
        }

        // ── NurbCurve2d → 平滑闭合 Polyline2d（应用 CurveFit）────────────
        //   采样为较稀疏的控制顶点，再由 CurveFit 在每对顶点间生成平滑圆弧，
        //   等效于 AutoCAD 中对样条边界拟合 Polyline 后执行 PEDIT → Fit (CURVEFIT)。
        private static int CreateCurveFitPolylineFromNurb(
            ITransactionService ts, NurbCurve2d nurb, Plane plane, int color, string layer)
        {
            try
            {
                var sampler = new DDNCadAddins.Core.Services.CurveSampler();
                var startPt = new Point2D(nurb.StartPoint.X, nurb.StartPoint.Y);
                var endPt   = new Point2D(nurb.EndPoint.X, nurb.EndPoint.Y);
                // CurveFit 会在顶点之间补充圆弧，因此控制顶点取相对稀疏的 32 点即可
                var sampled = sampler.SampleGenericCurve(startPt, endPt, 32,
                    t =>
                    {
                        double param = MapNurbParam(nurb, t);
                        var pt = nurb.EvaluatePoint(param);
                        return new Point2D(pt.X, pt.Y);
                    });
                if (sampled == null || sampled.Count < 3) return 0;

                var poly2d = new Polyline2d
                {
                    PolyType = Poly2dType.SimplePoly,
                    Closed = true,
                    ColorIndex = color,
                    Layer = layer
                };

                // Polyline2d.AppendVertex 要求多段线先成为数据库驻留对象
                if (ts.AppendEntityToCurrentSpace(poly2d).IsNull) return 0;

                foreach (var cp in sampled)
                {
                    var pt3d = plane.EvaluatePoint(new Point2d(cp.X, cp.Y));
                    using (var vertex = new Vertex2d(new Point3d(pt3d.X, pt3d.Y, 0.0), 0.0, 0.0, 0.0, 0.0))
                    {
                        poly2d.AppendVertex(vertex);
                        ts.AddNewlyCreatedDBObject(vertex, true);
                    }
                }

                // 应用曲线拟合：在每对顶点之间生成平滑圆弧对
                poly2d.CurveFit();

                return sampled.Count;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CreateCurveFitPolylineFromNurb 失败: {ex.Message}", ex);
                return 0;
            }
        }

        private static double MapNurbParam(NurbCurve2d nurb, double t)
        {
            try
            {
                int d = nurb.Order - 1;
                if (d < 0) return t;
                var k = nurb.Knots;
                if (k == null || k.Count < d * 2 + 2) return t;
                double s = k[d], e = k[k.Count - d - 1], r = e - s;
                return r <= 0 ? s : s + r * t;
            }
            catch { return t; }
        }

        private ObjectId SelectSingleHatch(Editor ed)
        {
            try
            {
                var opt = new PromptEntityOptions("\n选择要提取边界的 Hatch: ");
                opt.SetRejectMessage("\n请选择 Hatch。");
                opt.AddAllowedClass(typeof(Hatch), false);
                var res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                { ed.WriteMessage("\n未选择 Hatch。"); return ObjectId.Null; }
                return res.ObjectId;
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择 Hatch 失败: {ex.Message}", ex);
                return ObjectId.Null;
            }
        }
    }
}
