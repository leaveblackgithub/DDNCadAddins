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
    ///     <para>曲线实体：采样 + 中点分类 + 拆分.</para>
    /// </summary>
    internal static class CropUtils
    {
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

        private static List<Line> SampleCurveIntoLineSegments(Curve curve, int count)
        {
            var result = new List<Line>();
            try
            {
                var startParam = curve.StartParam;
                var endParam = curve.EndParam;
                var step = (endParam - startParam) / count;
                var prev = curve.GetPointAtParameter(startParam);
                for (var i = 1; i <= count; i++)
                {
                    var param = startParam + step * i;
                    if (param > endParam) param = endParam;
                    var curr = curve.GetPointAtParameter(param);
                    var seg = new Line(prev, curr)
                    {
                        Layer = curve.Layer,
                        Color = curve.Color,
                        Linetype = curve.Linetype,
                    };
                    result.Add(seg);
                    prev = curr;
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
