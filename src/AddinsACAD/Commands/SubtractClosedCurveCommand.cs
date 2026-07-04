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
    ///     SUBTRACTCLOSEDCURVE — 选择封闭曲线 A，再选择封闭曲线 B，
    ///     计算 A \ B（曲线 A 减去 A 与 B 的交集）并绘制结果.
    ///     支持 Polyline、Circle、Ellipse、Spline.
    ///     <para>
    ///         精确模式：使用 <see cref="CurveSubtractService"/> 逐边精确求交，
    ///         交点采用解析解（直线-圆/直线-椭圆二次方程），子线段参数化生成.
    ///     </para>
    ///     - 不相交 → 返回 A
    ///     - A 包含 B → 返回 A 减去 B 区域后的剩余部分（带内孔环）
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

                // ── 步骤 2: 选择曲线 B（被 SUBTRACT 曲线）─────────────────
                var curveB = this.SelectClosedCurve(ed, "B", out var idB);
                if (curveB == null) return;

                // ── 步骤 3: 精确差集 A \ B ────────────────────────────────
                //   双向拆分：A 按 B 交点拆分，B 按 A 交点拆分
                //   保留 A 不在 B 内部的子段 + B 在 A 内部的子段（反向）
                var subtractService = new CurveSubtractService();
                var subtractResult = subtractService.Subtract(
                    curveA.ExactSegments, curveA.Boundary,
                    curveB.ExactSegments, curveB.Boundary);

                bool noResult = !subtractResult.IsSuccess || subtractResult.Data.IsEmpty;

                // ── 步骤 4: 绘制结果（精确段→Polyline）────────────────────
                if (!noResult && subtractResult.IsSuccess)
                {
                    CadServiceManager._.ExecuteInTransactions("", ts =>
                    {
                        foreach (var loop in subtractResult.Data.Loops)
                        {
                            if (loop == null || loop.Count == 0) continue;

                            int vertexCount = CurveToExactSegmentConverter.DrawExactSegments(
                                ts, loop, 3);
                            if (vertexCount > 0)
                            {
                                resultPolyCount++;
                                totalVertices += vertexCount;
                            }
                        }
                    });
                }

                isSuccess = resultPolyCount > 0;
                stopwatch.Stop();

                // ── 步骤 5: 输出命令行信息 ──────────────────────────────────
                if (isSuccess)
                {
                    ed.WriteMessage(
                        $"\n差集结果：{resultPolyCount} 个封闭环，共 {totalVertices} 个顶点（精确边界）");
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
                        BoundaryVertices = new List<Point2D>(curveA.Polygon),
                        BoundaryVertexCount = curveA.Polygon.Count,
                        TotalEntityCount = 2,
                        KeptCount = resultPolyCount,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Entities = new List<CropEntitySnapshot>
                        {
                            CreateSnapshot(idA.ToString(), curveA.Type, curveA.Polygon),
                            CreateSnapshot(idB.ToString(), curveB.Type, curveB.Polygon),
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
        ///     选择单条闭合曲线，转换为精确段列表和裁剪边界.
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

                    // 精确段列表
                    var exactSegments = CurveToExactSegmentConverter.ConvertToExactSegments(curve);
                    if (exactSegments == null || exactSegments.Count == 0)
                    {
                        ed.WriteMessage($"\n曲线 {label} 精确段转换失败。");
                        return;
                    }

                    // 精确裁剪边界
                    var boundary = CurveToExactSegmentConverter.ConvertToCropBoundary(curve);
                    if (boundary == null)
                    {
                        ed.WriteMessage($"\n曲线 {label} 边界转换失败。");
                        return;
                    }

                    // 采样多边形（用于 TestRecorder 记录）
                    var polygon = new CurveToPolygonConverter().ConvertCurveToPolygon(curve);
                    if (polygon == null || polygon.Count < 3)
                    {
                        ed.WriteMessage($"\n曲线 {label} 多边形转换失败（顶点 < 3）。");
                        return;
                    }

                    string type = curve.GetType().Name;
                    sel = new CurveSelection
                    {
                        Type = type,
                        Polygon = polygon,
                        ExactSegments = exactSegments,
                        Boundary = boundary
                    };
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
