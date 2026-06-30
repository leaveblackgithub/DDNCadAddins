using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace ServiceACAD
{
    /// <summary>
    ///     曲线→多边形转换器：将各种 AutoCAD Curve 类型转换为多边形顶点列表.
    ///     内部委托给 <see cref="CurveToPolygonConverter"/>，自动选择精确/拟合策略.
    ///     - Polyline：逐顶点+凸度提取（精确，使用 ExactCurveGenerator）
    ///     - Circle：用2个半圆+bulges=1.0表示（精确）
    ///     - Ellipse：提取关键顶点+凸度（精确）
    ///     - Spline：按控制点倍数采样（拟合，使用 FittedCurveGenerator）
    /// </summary>
    public static class CurveConverter
    {
        private static readonly CurveToPolygonConverter Generator = new CurveToPolygonConverter();

        /// <summary>
        ///     将闭合 Curve 转换为多边形顶点列表 (WCS).
        ///     返回 null 表示转换失败或曲线不闭合.
        /// </summary>
        public static List<CorePoint2D> ConvertToPolygon(Curve curve)
        {
            return Generator.ConvertCurveToPolygon(curve);
        }
    }
}