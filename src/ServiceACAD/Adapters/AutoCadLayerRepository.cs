using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace ServiceACAD.Adapters
{
    /// <summary>
    ///     AutoCAD 图层仓储适配器 - 负责 CAD 类型与 Core POCO 的转换
    /// </summary>
    public class AutoCadLayerRepository : ILayerRepository
    {
        private readonly ITransactionService _transactionService;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        public AutoCadLayerRepository(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<LayerInfo> ILayerRepository.GetLayer(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    return DDNCadAddins.Core.Models.OpResult<LayerInfo>.Fail("图层名称为空");
                }

                var layer = _transactionService.Style.GetLayer(name, OpenMode.ForRead);
                if (layer == null || layer.IsErased)
                {
                    return DDNCadAddins.Core.Models.OpResult<LayerInfo>.Fail($"图层 {name} 不存在");
                }

                return DDNCadAddins.Core.Models.OpResult<LayerInfo>.Success(ToLayerInfo(layer));
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取图层异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<LayerInfo>.Fail($"获取图层失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<IReadOnlyList<LayerInfo>> ILayerRepository.GetAllLayers()
        {
            try
            {
                var layerTable = _transactionService.Style.GetLayerTable(OpenMode.ForRead);
                if (layerTable == null)
                {
                    return DDNCadAddins.Core.Models.OpResult<IReadOnlyList<LayerInfo>>.Fail("无法获取图层表");
                }

                var layers = new List<LayerInfo>();
                foreach (ObjectId layerId in layerTable)
                {
                    try
                    {
                        if (!layerId.IsValid || layerId.IsErased)
                        {
                            continue;
                        }

                        var layer = _transactionService.GetObject<LayerTableRecord>(layerId, OpenMode.ForRead);
                        if (layer == null || layer.IsErased)
                        {
                            continue;
                        }

                        layers.Add(ToLayerInfo(layer));
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        Logger._.Warn($"跳过无效图层 (ObjectId={layerId}): {ex.ErrorStatus}");
                    }
                }

                return DDNCadAddins.Core.Models.OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly());
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取所有图层异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<IReadOnlyList<LayerInfo>>.Fail($"获取所有图层失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult ILayerRepository.UpdateLayer(LayerInfo layer)
        {
            try
            {
                if (layer == null || string.IsNullOrEmpty(layer.Name))
                {
                    return DDNCadAddins.Core.Models.OpResult.Fail("图层信息无效");
                }

                var layerRecord = _transactionService.Style.GetLayer(layer.Name, OpenMode.ForWrite);
                if (layerRecord == null || layerRecord.IsErased)
                {
                    return DDNCadAddins.Core.Models.OpResult.Fail($"图层 {layer.Name} 不存在");
                }

                layerRecord.IsLocked = layer.IsLocked;
                layerRecord.IsFrozen = layer.IsFrozen;

                return DDNCadAddins.Core.Models.OpResult.Success();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Logger._.Warn($"跳过无效图层 {layer?.Name}: {ex.ErrorStatus}");
                return DDNCadAddins.Core.Models.OpResult.Fail($"更新图层失败: {ex.ErrorStatus}");
            }
            catch (Exception ex)
            {
                Logger._.Error($"更新图层异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult.Fail($"更新图层失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        DDNCadAddins.Core.Models.OpResult<string> ILayerRepository.GetCurrentLayerName()
        {
            try
            {
                var db = HostApplicationServices.WorkingDatabase;
                var layer = _transactionService.GetObject<LayerTableRecord>(db.Clayer, OpenMode.ForRead);
                if (layer == null || layer.IsErased)
                {
                    return DDNCadAddins.Core.Models.OpResult<string>.Fail("无法获取当前图层");
                }

                return DDNCadAddins.Core.Models.OpResult<string>.Success(layer.Name);
            }
            catch (Exception ex)
            {
                Logger._.Error($"获取当前图层名称异常: {ex.Message}");
                return DDNCadAddins.Core.Models.OpResult<string>.Fail($"获取当前图层名称失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     将 CAD 图层记录转换为 Core POCO
        /// </summary>
        /// <param name="layer">图层记录</param>
        /// <returns>图层信息</returns>
        private LayerInfo ToLayerInfo(LayerTableRecord layer)
        {
            var linetypeName = CadServiceManager.Linetypes.Continuous;
            try
            {
                if (layer.LinetypeObjectId.IsValid && !layer.LinetypeObjectId.IsErased)
                {
                    var linetype = _transactionService.GetObject<LinetypeTableRecord>(
                        layer.LinetypeObjectId,
                        OpenMode.ForRead);
                    if (linetype != null && !string.IsNullOrEmpty(linetype.Name))
                    {
                        linetypeName = linetype.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger._.Warn($"读取图层 {layer.Name} 线型失败: {ex.Message}");
            }

            return new LayerInfo
            {
                Name = layer.Name,
                IsLocked = layer.IsLocked,
                IsFrozen = layer.IsFrozen,
                ColorIndex = layer.Color.ColorIndex,
                LinetypeName = linetypeName
            };
        }
    }
}
