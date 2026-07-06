using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Services;
using DDNCadAddins.Core.Models;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     裁剪工具 — 供 placeholder 服务复用.
    ///     <para>非曲线实体：边界框 + 保留/删除.</para>
    ///     <para>曲线实体：采样 + 中点分类 + 拆分（使用 FittedCurveGenerator 统一采样）.</para>
    /// </summary>
    public static class CropUtils
    {
        private static readonly FittedCurveGenerator FittedGen = new FittedCurveGenerator();

        /// <summary>非曲线实体的裁剪结果.</summary>
        internal struct NonCurveResult
        {
            internal int DeletedCount;
            internal int KeptCount;
        }

        /// <summary>曲线实体的裁剪结果.</summary>
        internal struct CurveResult
        {
            internal int DeletedCount;
            internal int SplitCount;
            internal int KeptCount;
        }

        /// <summary>处理单个非曲线实体（Hatch / BlockRef / Text / MText / Dimension / DBPoint）.</summary>
        internal static NonCurveResult ProcessNonCurve(
            Entity entity,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ICropGeometryService geometry)
        {
            var r = new NonCurveResult();
            var ext = entity.GeometricExtents;
            if (ext.MinPoint.DistanceTo(ext.MaxPoint) < 1e-9)
            {
                r.KeptCount = 1;
                return r;
            }

            var minPt = new CorePoint2D(ext.MinPoint.X, ext.MinPoint.Y);
            var maxPt = new CorePoint2D(ext.MaxPoint.X, ext.MaxPoint.Y);
            var containment = geometry.ClassifyBoundingBox(minPt, maxPt, boundaryPoints);

            bool shouldDelete = keepInside
                ? containment == ContainmentResult.Outside
                : containment == ContainmentResult.Inside || containment == ContainmentResult.OnBoundary;

            // 相交=保留（不拆分非曲线实体）
            if (shouldDelete)
            {
                r.DeletedCount = 1;
            }
            else
            {
                r.KeptCount = 1;
            }

            return r;
        }

        /// <summary>处理单个曲线实体（Spline / Ellipse / 3DPolyline / MLine / Leader）— 采样 + 中点分类.</summary>
        internal static CurveResult ProcessCurve(
            Curve curve,
            IReadOnlyList<CorePoint2D> boundaryPoints,
            bool keepInside,
            ITransactionService serviceTrans,
            ICropGeometryService geometry)
        {
            var r = new CurveResult();
            var segments = SampleCurveIntoLineSegments(curve, 50);
            if (segments.Count == 0)
            {
                EraseEntity(curve);
                r.DeletedCount = 1;
                return r;
            }

            var toKeep = new List<Line>();
            foreach (var seg in segments)
            {
                var midPt = new CorePoint2D(
                    (seg.StartPoint.X + seg.EndPoint.X) / 2.0,
                    (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0);
                var inside = geometry.IsPointInPolygon(midPt, boundaryPoints);
                if ((keepInside && inside) || (!keepInside && !inside))
                    toKeep.Add(seg);
                else
                    seg.Dispose();
            }

            if (toKeep.Count > 0)
            {
                EraseEntity(curve);
                foreach (var seg in toKeep)
                    serviceTrans.AppendEntityToCurrentSpace(seg);
                r.DeletedCount = 1;
                r.SplitCount = 1;
            }
            else
            {
                foreach (var seg in toKeep) seg.Dispose();
                EraseEntity(curve);
                r.DeletedCount = 1;
            }

            return r;
        }

        /// <summary>
        ///     将曲线采样为直线段列表 — 委托给 <see cref="FittedCurveGenerator"/>.
        /// </summary>
        private static List<Line> SampleCurveIntoLineSegments(Curve curve, int count)
        {
            var result = new List<Line>();
            try
            {
                var startParam = curve.StartParam;
                var endParam = curve.EndParam;
                var startPt = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
                var endPt = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);

                // 使用 FittedCurveGenerator 获取采样点
                var sampledPts = FittedGen.GenerateGenericCurve(
                    startPt, endPt,
                    t =>
                    {
                        var param = startParam + (endParam - startParam) * t;
                        if (param > endParam) param = endParam;
                        var pt = curve.GetPointAtParameter(param);
                        return new Point2D(pt.X, pt.Y);
                    },
                    count);

                // 将采样点转换为 Line 实体
                for (var i = 0; i < sampledPts.Count - 1; i++)
                {
                    var p1 = sampledPts[i];
                    var p2 = sampledPts[i + 1];
                    var seg = new Line(
                        new Point3d(p1.X, p1.Y, 0.0),
                        new Point3d(p2.X, p2.Y, 0.0))
                    {
                        Layer = curve.Layer,
                        Color = curve.Color,
                        Linetype = curve.Linetype,
                    };
                    result.Add(seg);
                }
            }
            catch (Exception ex)
            {
                Logger._.Warn($"采样曲线失败: {ex.Message}");
                foreach (var s in result) s.Dispose();
                return new List<Line>();
            }

            return result;
        }

        /// <summary>
        ///     将闭合曲线采样为多边形顶点列表（用于裁剪边界）.
        ///     在事务内打开曲线，采样 64 个点，去重后返回.
        /// </summary>
        /// <param name="serviceTrans">事务服务</param>
        /// <param name="curveId">闭合曲线的 ObjectId</param>
        /// <param name="sampleCount">采样点数（默认 64）</param>
        /// <returns>采样后的多边形顶点列表；失败或无效时返回 null.</returns>
        public static List<CorePoint2D> SampleClosedCurveBoundary(
            ITransactionService serviceTrans, ObjectId curveId, int sampleCount = 64)
        {
            try
            {
                var curve = serviceTrans.GetObject<Curve>(curveId);
                if (curve == null || !curve.Closed)
                    return null;

                var startParam = curve.StartParam;
                var endParam = curve.EndParam;
                var points = new List<CorePoint2D>(sampleCount);

                for (var i = 0; i < sampleCount; i++)
                {
                    var param = startParam + (endParam - startParam) * i / sampleCount;
                    var pt = curve.GetPointAtParameter(Math.Min(param, endParam));
                    points.Add(new CorePoint2D(pt.X, pt.Y));
                }

                // 去重
                var deduped = new List<CorePoint2D>(sampleCount);
                foreach (var p in points)
                {
                    if (deduped.Count == 0) { deduped.Add(p); continue; }
                    var last = deduped[deduped.Count - 1];
                    if (Math.Abs(last.X - p.X) > 1e-6 || Math.Abs(last.Y - p.Y) > 1e-6)
                        deduped.Add(p);
                }

                return deduped.Count >= 3 ? deduped : null;
            }
            catch (Exception ex)
            {
                Logger._.Warn($"采样闭合曲线边界失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     询问裁剪方向：减掉外部-保留内部，还是减掉内部-保留外部.
        /// </summary>
        /// <param name="ed">编辑器</param>
        /// <returns>true=减掉外部（保留内部），false=减掉内部（保留外部），null=取消.</returns>
        public static bool? AskCropDirection(Editor ed)
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

                if (result.StringResult == "减掉外部")
                    return true;
                if (result.StringResult == "减掉内部")
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Logger._.Error($"询问裁剪方向失败: {ex.Message}", ex);
                ed.WriteMessage($"\n询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }

        private static void EraseEntity(Entity entity)
        {
            try
            {
                if (!entity.IsWriteEnabled) entity.UpgradeOpen();
                entity.Erase();
            }
            catch (Exception ex)
            {
                Logger._.Warn($"擦除实体失败: {ex.Message}");
            }
        }
    }
}
