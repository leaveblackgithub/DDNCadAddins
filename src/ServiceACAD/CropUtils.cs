using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
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
    internal static class CropUtils
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
