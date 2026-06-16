using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     手动测试记录器 — 每次裁剪操作自动生成带 UID 的 JSON 快照。
    ///     记录包含：坐标系统、边界几何、被裁剪实体几何、裁剪结果。
    /// </summary>
    public static class TestRecorder
    {
        private static string _recordsDir;
        private static readonly object Lock = new object();

        /// <summary>总是使用固定的 DDNCadAddins 项目目录，避免热加载路径问题</summary>
        private static string GetRecordsDir()
        {
            if (_recordsDir != null) return _recordsDir;
            _recordsDir = @"D:\leaveblackgithub\DDNCadAddins\TestRecords";
            if (!Directory.Exists(_recordsDir))
                Directory.CreateDirectory(_recordsDir);
            return _recordsDir;
        }

        public static string GenerateUid()
        {
            var now = DateTime.Now;
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 4);
            return $"{now:yyyyMMdd}-{now:HHmmss}-{suffix}";
        }

        public static string Record(CropTestRecord record)
        {
            record.Timestamp = DateTime.Now;
            if (string.IsNullOrEmpty(record.Uid))
                record.Uid = GenerateUid();

            var json = ToJson(record);
            var fileName = $"crop_{record.Uid}.json";
            var filePath = Path.Combine(GetRecordsDir(), fileName);

            lock (Lock) { File.WriteAllText(filePath, json, Encoding.UTF8); }
            Debug.WriteLine($"[TestRecorder] 记录已保存: {filePath}");
            return record.Uid;
        }

        public static CropTestRecord Load(string uid)
        {
            var filePath = Path.Combine(GetRecordsDir(), $"crop_{uid}.json");
            if (!File.Exists(filePath)) return null;
            return FromJson(File.ReadAllText(filePath, Encoding.UTF8));
        }

        public static List<string> ListUids()
        {
            if (!Directory.Exists(GetRecordsDir())) return new List<string>();
            return Directory.GetFiles(GetRecordsDir(), "crop_*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(f => f.StartsWith("crop_") ? f.Substring(5) : f)
                .OrderByDescending(u => u).ToList();
        }

        // ═══════════════ 实体快照采集 ═══════════════

        /// <summary>从 ObjectId 列表采集实体几何快照（需在事务内调用）</summary>
        public static List<CropEntitySnapshot> CollectSnapshots(
            ITransactionService ts, List<ObjectId> ids,
            IReadOnlyList<CorePoint2D> boundary, ICropGeometryService geo)
        {
            var snaps = new List<CropEntitySnapshot>();
            if (ids == null) return snaps;

            foreach (var id in ids)
            {
                try
                {
                    if (!id.IsValid || id.IsErased) continue;
                    var ent = ts.GetObject<Entity>(id);
                    if (ent == null || ent.IsErased) continue;

                    var snap = new CropEntitySnapshot { ObjectId = id.ToString() };
                    var ext = ent.GeometricExtents;

                    // 包围盒分类
                    if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9)
                        snap.Containment = "Degenerate";
                    else
                        snap.Containment = geo.ClassifyBoundingBox(
                            new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y),
                            new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y),
                            boundary).ToString();

                    if (ent is Polyline pl)
                    {
                        snap.Type = "Polyline";
                        var pts = new List<CorePoint2D>();
                        var bulges = new List<double>();
                        int n = pl.NumberOfVertices;
                        for (int i = 0; i < n; i++)
                        {
                            var pt = pl.GetPoint2dAt(i);
                            pts.Add(new CorePoint2D(pt.X, pt.Y));
                            if (i < (pl.Closed ? n : n - 1))
                                bulges.Add(pl.GetBulgeAt(i));
                        }
                        snap.KeyGeometry = pts;
                        snap.KeyParams = bulges;
                    }
                    else if (ent is Circle c)
                    {
                        snap.Type = "Circle";
                        snap.KeyGeometry = new List<CorePoint2D> { new CorePoint2D(c.Center.X, c.Center.Y) };
                        snap.KeyParams = new List<double> { c.Radius };
                    }
                    else if (ent is Line l)
                    {
                        snap.Type = "Line";
                        snap.KeyGeometry = new List<CorePoint2D> {
                            new CorePoint2D(l.StartPoint.X, l.StartPoint.Y),
                            new CorePoint2D(l.EndPoint.X, l.EndPoint.Y)
                        };
                    }
                    else if (ent is Arc a)
                    {
                        snap.Type = "Arc";
                        snap.KeyGeometry = new List<CorePoint2D> { new CorePoint2D(a.Center.X, a.Center.Y) };
                        snap.KeyParams = new List<double> { a.Radius, a.StartAngle, a.EndAngle };
                    }
                    else
                    {
                        snap.Type = ent.GetType().Name;
                    }
                    snaps.Add(snap);
                }
                catch { /* skip individual entity errors */ }
            }
            return snaps;
        }

        /// <summary>获取当前 UCS 原点/轴向（WCS），通过 out 参数返回</summary>
        public static void CaptureUcs(
            out CorePoint2D origin,
            out CorePoint2D xAxis,
            out CorePoint2D yAxis)
        {
            origin = new CorePoint2D(0, 0);
            xAxis = new CorePoint2D(1, 0);
            yAxis = new CorePoint2D(0, 1);
            try
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Core.Application
                    .DocumentManager.MdiActiveDocument?.Editor;
                if (ed == null) return;
                var ucs = ed.CurrentUserCoordinateSystem;
                origin = new CorePoint2D(ucs.CoordinateSystem3d.Origin.X, ucs.CoordinateSystem3d.Origin.Y);
                xAxis = new CorePoint2D(ucs.CoordinateSystem3d.Xaxis.X, ucs.CoordinateSystem3d.Xaxis.Y);
                yAxis = new CorePoint2D(ucs.CoordinateSystem3d.Yaxis.X, ucs.CoordinateSystem3d.Yaxis.Y);
            }
            catch { }
        }

        // ═══════════════ JSON 序列化 ═══════════════

        private static string ToJson(CropTestRecord r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"uid\": \"{E(r.Uid)}\",");
            sb.AppendLine($"  \"timestamp\": \"{r.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\",");
            sb.AppendLine($"  \"command\": \"{E(r.Command)}\",");
            sb.AppendLine($"  \"direction\": \"{E(r.Direction)}\",");

            // ── 坐标系统 ──
            sb.AppendLine("  \"ucs\": {");
            WritePt(sb, "origin", r.UcsOrigin, "    ");
            sb.AppendLine("    ,");
            WritePt(sb, "xAxis", r.UcsXAxis, "    ");
            sb.AppendLine("    ,");
            WritePt(sb, "yAxis", r.UcsYAxis, "    ");
            sb.AppendLine("  },");

            // ── 边界 ──
            sb.AppendLine($"  \"boundaryVertexCount\": {r.BoundaryVertexCount},");
            sb.Append("  \"boundaryVertices\": ");
            WritePtArray(sb, r.BoundaryVertices);
            sb.AppendLine(",");

            // ── 实体 ──
            sb.AppendLine($"  \"totalEntityCount\": {r.TotalEntityCount},");
            sb.AppendLine("  \"entities\": [");

            if (r.Entities != null)
            {
                for (var ei = 0; ei < r.Entities.Count; ei++)
                {
                    var e = r.Entities[ei];
                    var ecomma = ei < r.Entities.Count - 1 ? "," : "";
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"id\": \"{E(e.ObjectId)}\",");
                    sb.AppendLine($"      \"type\": \"{E(e.Type)}\",");
                    sb.AppendLine($"      \"containment\": \"{E(e.Containment)}\",");
                    sb.AppendLine($"      \"result\": \"{E(e.Result)}\",");
                    sb.Append("      \"geometry\": ");
                    WritePtArray(sb, e.KeyGeometry);
                    sb.AppendLine(",");
                    sb.Append("      \"params\": ");
                    WriteDoubleArray(sb, e.KeyParams);
                    sb.AppendLine();
                    sb.Append($"    }}{ecomma}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("  ],");

            // ── 汇总 ──
            sb.AppendLine($"  \"isSuccess\": {r.IsSuccess.ToString().ToLower()},");
            sb.AppendLine($"  \"deletedCount\": {r.DeletedCount},");
            sb.AppendLine($"  \"splitCount\": {r.SplitCount},");
            sb.AppendLine($"  \"keptCount\": {r.KeptCount},");
            sb.AppendLine($"  \"skippedCount\": {r.SkippedCount},");
            sb.AppendLine($"  \"errorMessage\": \"{E(r.ErrorMessage ?? "")}\",");
            sb.AppendLine($"  \"elapsedMs\": {r.ElapsedMs},");
            sb.AppendLine($"  \"excludedBoundaryId\": \"{E(r.ExcludedBoundaryId ?? "")}\"");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void WritePt(StringBuilder sb, string name, Point2D pt, string indent)
        {
            sb.Append(indent).Append('"').Append(name);
            sb.Append("\": {\"x\": ").Append(pt.X.ToString("F6"));
            sb.Append(", \"y\": ").Append(pt.Y.ToString("F6")).Append('}');
        }

        private static void WritePtArray(StringBuilder sb, List<Point2D> pts)
        {
            if (pts == null || pts.Count == 0) { sb.Append("[]"); return; }
            sb.Append('[');
            for (var i = 0; i < pts.Count; i++)
            {
                var c = i < pts.Count - 1 ? "," : "";
                sb.Append("{\"x\": ").Append(pts[i].X.ToString("F6"));
                sb.Append(", \"y\": ").Append(pts[i].Y.ToString("F6")).Append('}').Append(c);
                sb.AppendLine();
            }
            sb.Append("    ]");
        }

        private static void WriteDoubleArray(StringBuilder sb, List<double> vals)
        {
            if (vals == null || vals.Count == 0) { sb.Append("[]"); return; }
            sb.Append("[");
            for (var i = 0; i < vals.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{vals[i]:F6}");
            }
            sb.Append("]");
        }

        // ═══════════════ JSON 反序列化 ═══════════════

        private static CropTestRecord FromJson(string json)
        {
            var r = new CropTestRecord();
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                var t = line.Trim().TrimEnd(',');
                if (t.StartsWith("\"uid\"")) r.Uid = ExtractString(t);
                else if (t.StartsWith("\"timestamp\"")) r.Timestamp = DateTime.Parse(ExtractString(t));
                else if (t.StartsWith("\"command\"")) r.Command = ExtractString(t);
                else if (t.StartsWith("\"direction\"")) r.Direction = ExtractString(t);
                else if (t.StartsWith("\"boundaryVertexCount\"")) r.BoundaryVertexCount = ExtractInt(t);
                else if (t.StartsWith("\"totalEntityCount\"")) r.TotalEntityCount = ExtractInt(t);
                else if (t.StartsWith("\"isSuccess\"")) r.IsSuccess = t.Contains("true");
                else if (t.StartsWith("\"deletedCount\"")) r.DeletedCount = ExtractInt(t);
                else if (t.StartsWith("\"splitCount\"")) r.SplitCount = ExtractInt(t);
                else if (t.StartsWith("\"keptCount\"")) r.KeptCount = ExtractInt(t);
                else if (t.StartsWith("\"skippedCount\"")) r.SkippedCount = ExtractInt(t);
                else if (t.StartsWith("\"errorMessage\"")) r.ErrorMessage = ExtractString(t);
                else if (t.StartsWith("\"elapsedMs\"")) r.ElapsedMs = ExtractLong(t);
                else if (t.StartsWith("\"excludedBoundaryId\"")) r.ExcludedBoundaryId = ExtractString(t);
            }
            return r;
        }

        private static string ExtractString(string line)
        {
            var start = line.IndexOf('"', line.IndexOf(':'));
            if (start < 0) return "";
            var end = line.IndexOf('"', start + 1);
            return end < 0 ? "" : line.Substring(start + 1, end - start - 1);
        }

        private static int ExtractInt(string line)
        {
            var start = line.IndexOf(':') + 1;
            return int.TryParse(line.Substring(start).Trim(), out var v) ? v : 0;
        }

        private static long ExtractLong(string line)
        {
            var start = line.IndexOf(':') + 1;
            return long.TryParse(line.Substring(start).Trim(), out var v) ? v : 0;
        }

        private static string E(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}