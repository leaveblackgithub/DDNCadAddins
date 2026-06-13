using System;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     图层状态管理业务服务 - 纯业务逻辑，无 CAD 依赖
    /// </summary>
    public class LayerManagementService : ILayerManagementService
    {
        private readonly ILayerRepository _layerRepository;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="layerRepository">图层仓储</param>
        public LayerManagementService(ILayerRepository layerRepository)
        {
            _layerRepository = layerRepository;
        }

        /// <inheritdoc />
        public OpResult<LayerStateSnapshot> CaptureAllLayerStates()
        {
            try
            {
                var allLayersResult = _layerRepository.GetAllLayers();
                if (!allLayersResult.IsSuccess)
                {
                    return OpResult<LayerStateSnapshot>.Fail(allLayersResult.Message);
                }

                var snapshot = new LayerStateSnapshot();
                foreach (var layer in allLayersResult.Data)
                {
                    if (string.IsNullOrEmpty(layer?.Name))
                    {
                        continue;
                    }

                    snapshot.States[layer.Name] = new LayerStateEntry
                    {
                        IsLocked = layer.IsLocked,
                        IsFrozen = layer.IsFrozen
                    };
                }

                return OpResult<LayerStateSnapshot>.Success(snapshot);
            }
            catch (Exception ex)
            {
                return OpResult<LayerStateSnapshot>.Fail($"记录图层状态失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public OpResult<bool> UnlockAndThawAllLayers()
        {
            try
            {
                var allLayersResult = _layerRepository.GetAllLayers();
                if (!allLayersResult.IsSuccess)
                {
                    return OpResult<bool>.Fail(allLayersResult.Message);
                }

                foreach (var layer in allLayersResult.Data)
                {
                    if (string.IsNullOrEmpty(layer?.Name))
                    {
                        continue;
                    }

                    var updatedLayer = new LayerInfo
                    {
                        Name = layer.Name,
                        IsLocked = false,
                        IsFrozen = false,
                        ColorIndex = layer.ColorIndex,
                        LinetypeName = layer.LinetypeName
                    };

                    var updateResult = _layerRepository.UpdateLayer(updatedLayer);
                    if (!updateResult.IsSuccess)
                    {
                        // 与旧实现一致：跳过无效图层，继续处理其余图层
                        continue;
                    }
                }

                return OpResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return OpResult<bool>.Fail($"解锁解冻图层失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public OpResult<bool> RestoreLayerStates(LayerStateSnapshot snapshot)
        {
            try
            {
                if (snapshot == null || snapshot.States.Count == 0)
                {
                    return OpResult<bool>.Success(true);
                }

                var currentLayerResult = _layerRepository.GetCurrentLayerName();
                if (!currentLayerResult.IsSuccess)
                {
                    return OpResult<bool>.Fail(currentLayerResult.Message);
                }

                var currentLayerName = currentLayerResult.Data;

                foreach (var layerState in snapshot.States)
                {
                    var layerName = layerState.Key;
                    if (string.IsNullOrEmpty(layerName))
                    {
                        continue;
                    }

                    var getLayerResult = _layerRepository.GetLayer(layerName);
                    if (!getLayerResult.IsSuccess)
                    {
                        continue;
                    }

                    var layer = getLayerResult.Data;
                    layer.IsLocked = layerState.Value.IsLocked;

                    if (layerState.Value.IsFrozen)
                    {
                        if (layerName == "0" || layerName == currentLayerName)
                        {
                            layer.IsFrozen = false;
                        }
                        else
                        {
                            layer.IsFrozen = true;
                        }
                    }
                    else
                    {
                        layer.IsFrozen = false;
                    }

                    var updateResult = _layerRepository.UpdateLayer(layer);
                    if (!updateResult.IsSuccess)
                    {
                        // 与旧实现一致：跳过无效图层，继续处理其余图层
                        continue;
                    }
                }

                return OpResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return OpResult<bool>.Fail($"恢复图层状态失败: {ex.Message}");
            }
        }
    }
}
