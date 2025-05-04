using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务样式部分，提供图层和线型管理功能
    /// </summary>
    public class TransactionServiceForStyle : ITransactionServiceForStyle
    {
        private readonly TransactionService _transactionService;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        public TransactionServiceForStyle(TransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        ///     获取图层表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>图层表</returns>
        public LayerTable GetLayerTable(OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                var db = HostApplicationServices.WorkingDatabase;
                return _transactionService.GetObject<LayerTable>(db.LayerTableId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取图层表异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     创建新线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的线型对象，如果创建失败则返回null</returns>
        public LinetypeTableRecord GetOrCreateLineType(string lineTypeName)
        {
            try
            {
                if (string.IsNullOrEmpty(lineTypeName))
                {
                    Logger._.Warn($"线型名为空，将返回{CadServiceManager.LineTypeContinuous}线型");
                    return GetLineType(CadServiceManager.LineTypeContinuous);
                }

                // 检查线型是否已存在
                var ltRec = GetLineType(lineTypeName);
                if (ltRec != null)
                {
                    Logger._.Warn($"线型 {lineTypeName} 已存在，将返回现有线型");
                    return ltRec;
                }

                // 创建新线型
                ltRec = CreateLineType(lineTypeName);

                return ltRec;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取或创建线型失败：{ex.Message}");
                return null;
            }
        }

        public LinetypeTableRecord CreateLineType(string lineTypeName)
        {
            try
            {
                if (string.IsNullOrEmpty(lineTypeName))
                {
                    Logger._.Warn("线型名为空");
                    return null;
                }

                if (GetLineType(lineTypeName) != null)
                {
                    Logger._.Warn($"线型{lineTypeName}已存在");
                    return null;
                }

                var ltRec = new LinetypeTableRecord();
                ltRec.Name = lineTypeName;
                ltRec.AsciiDescription = $"Custom linetype - {lineTypeName}";
                ltRec.PatternLength = 0.5;

                // 设置线型图案（示例：点划线） 
                var dashPattern = new List<double> { 0.25, -0.125, 0.0, -0.125 };
                foreach (var dash in dashPattern)
                {
                    ltRec.SetDashLengthAt(ltRec.NumDashes, dash);
                    ltRec.NumDashes++;
                }

                // 添加线型到线型表
                var ltId = GetLineTypeTable(OpenMode.ForWrite).Add(ltRec);
                _transactionService.AddNewlyCreatedDBObject(ltRec, true);

                Logger._.Info($"创建线型成功：{lineTypeName}");
                return ltRec;
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建线型失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>图层对象，如果不存在则返回null</returns>
        public LayerTableRecord GetLayer(string layerName, OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                if (string.IsNullOrEmpty(layerName))
                {
                    Logger._.Error("获取图层失败：图层名称为空");
                    return null;
                }

                // 获取图层表
                var layerTable = GetLayerTable();
                if (layerTable == null)
                {
                    Logger._.Error("获取图层失败：获取图层表失败");
                    return null;
                }

                // 检查图层是否存在
                if (!layerTable.Has(layerName))
                {
                    Logger._.Warn($"图层 {layerName} 不存在");
                    return null;
                }

                // 获取图层
                var layerId = layerTable[layerName];
                return _transactionService.GetObject<LayerTableRecord>(layerId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取图层异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取线型表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>线型表</returns>
        public LinetypeTable GetLineTypeTable(OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                var db = HostApplicationServices.WorkingDatabase;
                return _transactionService.GetObject<LinetypeTable>(db.LinetypeTableId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取线型表异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取有效的图层名称
        /// </summary>
        /// <param name="layerName">原始图层名称</param>
        /// <returns>有效的图层名称</returns>
        public string GetValidLayerName(string layerName) => GetOrCreateLayer(layerName).Name;

        public ObjectId GetValidLineTypeId(string lineTypeName) => GetOrCreateLineType(lineTypeName).Id;

        /// <summary>
        ///     获取有效的线型名称
        /// </summary>
        /// <param name="linetypeName">原始线型名称</param>
        /// <returns>有效的线型名称</returns>
        public string GetValidLineTypeName(string linetypeName) => GetOrCreateLineType(linetypeName).Name;

        /// <summary>
        ///     获取有效的颜色索引
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色索引</returns>
        public short
            GetValidColorIndex(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite) =>
            colorIndex < 0 || colorIndex > 255 ? defaultColorIndex : colorIndex;

        /// <summary>
        ///     获取有效的颜色
        /// </summary>
        /// <param name="colorIndex">原始颜色索引</param>
        /// <param name="defaultColorIndex">默认颜色索引</param>
        /// <returns>有效的颜色</returns>
        public Color GetValidColor(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite)
        {
            colorIndex = GetValidColorIndex(colorIndex, defaultColorIndex);
            return Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
        }

        /// <summary>
        ///     获取当前图层名称
        /// </summary>
        /// <returns>当前图层名称</returns>
        public string GetCurrentLayerName()
        {
            try
            {
                var db = HostApplicationServices.WorkingDatabase;
                var layerName = "0"; // 默认图层

                // 获取当前图层
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var currentLayer = tr.GetObject(db.Clayer, OpenMode.ForRead) as LayerTableRecord;
                    if (currentLayer != null)
                    {
                        layerName = currentLayer.Name;
                    }

                    tr.Commit();
                }

                return layerName;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取当前图层名称异常: {ex.Message}");
                return "0"; // 发生异常时返回默认图层
            }
        }

        /// <summary>
        ///     创建新图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        /// <param name="lineTypeName"></param>
        /// <returns>创建的图层对象，如果创建失败则返回null</returns>
        public LayerTableRecord GetOrCreateLayer(string layerName = "",
            short colorIndex = CadServiceManager.ColorIndexWhite,
            string lineTypeName = CadServiceManager.LineTypeContinuous)
        {
            try
            {
                if (string.IsNullOrEmpty(layerName))
                {
                    Logger._.Warn($"图层名为空，将返回{CadServiceManager.Layer0}图层");
                    return GetLayer(CadServiceManager.Layer0);
                }
                // 生成一个有效的图层名

                var layer = GetLayer(layerName);
                // 检查图层是否已存在
                if (layer != null)
                {
                    Logger._.Warn($"图层 {layerName} 已存在，将返回现有图层");
                    return layer;
                }

                // 创建新图层
                layer = CreateLayer(layerName, colorIndex, lineTypeName);
                return layer;
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取或创建图层失败：{ex.Message}");
                return null;
            }
        }

        public LayerTableRecord CreateLayer(string layerName = "", short colorIndex = CadServiceManager.ColorIndexWhite,
            string lineTypeName = CadServiceManager.LineTypeContinuous)
        {
            try
            {
                if (string.IsNullOrEmpty(layerName))
                {
                    Logger._.Warn("图层名为空");
                    return null;
                }

                if (GetLayer(layerName) != null)
                {
                    Logger._.Warn($"图层{layerName}已存在");
                }

                // 创建新图层
                var layer = new LayerTableRecord();
                layer.Name = layerName;
                layer.Color = GetValidColor(colorIndex);
                layer.LinetypeObjectId = GetValidLineTypeId(lineTypeName);
                layer.IsPlottable = true;

                // 添加图层到图层表
                var layerId = GetLayerTable(OpenMode.ForWrite).Add(layer);
                _transactionService.AddNewlyCreatedDBObject(layer, true);

                Logger._.Info($"创建图层成功：{layerName}");
                return layer;
            }
            catch (Exception ex)
            {
                Logger._.Error($"创建图层失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     获取线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>线型对象，如果不存在则返回null</returns>
        public LinetypeTableRecord GetLineType(string lineTypeName, OpenMode openMode = OpenMode.ForRead)
        {
            try
            {
                if (string.IsNullOrEmpty(lineTypeName))
                {
                    Logger._.Error("获取线型失败：线型名称为空");
                    return null;
                }

                // 获取线型表
                var ltTable = GetLineTypeTable(openMode);
                if (ltTable == null)
                {
                    Logger._.Error("获取线型失败：获取线型表失败");
                    return null;
                }

                // 检查线型是否存在
                if (!ltTable.Has(lineTypeName))
                {
                    Logger._.Warn($"线型 {lineTypeName} 不存在");
                    return null;
                }

                // 获取线型
                var ltId = ltTable[lineTypeName];
                return _transactionService.GetObject<LinetypeTableRecord>(ltId, openMode);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取线型异常: {ex.Message}");
                return null;
            }
        }
    }
}
