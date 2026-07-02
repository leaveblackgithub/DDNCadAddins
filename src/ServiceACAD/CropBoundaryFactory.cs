using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     裁剪边界工厂 — 根据 AutoCAD Curve 类型创建对应的 ICropBoundary 实现.
    ///     <para>
    ///         - Polyline → PolygonCropBoundary（精确）
    ///         - Circle → CircleCropBoundary（精确解析）
    ///         - Ellipse → EllipseCropBoundary（精确解析，支持旋转）
    ///         - Spline / 其他 → PolygonCropBoundary（采样代理）
    ///     </para>
    /// </summary>
    public static class CropBoundaryFactory
    {
        /// <summary>
        ///     从 AutoCAD Curve 创建裁剪边界.
        /// </summary>
        /// <param name="curve">闭合曲线（WCS）.</param>
        /// <returns>ICropBoundary 实现；曲线无效返回 null.</returns>
        public static ICropBoundary CreateFromCurve(Curve curve)
        {
            if (curve == null || !curve.Closed)
                return null;

            try
            {
                // Polyline → 多边形边界（精确）
                if (curve is Polyline pl)
                {
                    return CreateFromPolyline(pl);
                }

                // Circle → 圆边界（精确解析）
                if (curve is Circle circle)
                {
                    return new CircleCropBoundary(
                        new CorePoint2D(circle.Center.X, circle.Center.Y),
                        circle.Radius);
                }

                // Ellipse → 椭圆边界（精确解析，支持旋转）
                if (curve is Ellipse ellipse)
                {
                    var center = new CorePoint2D(ellipse.Center.X, ellipse.Center.Y);
                    var majorAxis = ellipse.MajorAxis;
                    var majorDir = majorAxis.GetNormal();
                    var majorLen = majorAxis.Length;
                    var minorLen = ellipse.MinorRadius;
                    // 旋转角度 = atan2(majorDir.Y, majorDir.X)
                    var rotation = Math.Atan2(majorDir.Y, majorDir.X);
                    return new EllipseCropBoundary(center, majorLen, minorLen, rotation);
                }

                // Spline / 其他闭合曲线 → 采样多边形代理
                var polygon = SampleCurveToPolygon(curve);
                if (polygon != null && polygon.Count >= 3)
                    return new PolygonCropBoundary(polygon);

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     从 Polyline 创建多边形边界.
        /// </summary>
        private static ICropBoundary CreateFromPolyline(Polyline pl)
        {
            var converter = new CurveToPolygonConverter();
            var polygon = converter.ConvertCurveToPolygon(pl);
            if (polygon == null || polygon.Count < 3)
                return null;
            return new PolygonCropBoundary(polygon);
        }

        /// <summary>
        ///     将曲线采样为多边形顶点（用于 Spline 等无解析解的曲线）.
        /// </summary>
        private static System.Collections.Generic.List<CorePoint2D> SampleCurveToPolygon(Curve curve)
        {
            var converter = new CurveToPolygonConverter();
            return converter.ConvertCurveToPolygon(curve);
        }
    }
}
