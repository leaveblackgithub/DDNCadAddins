using System.Collections.Generic;
using System.Linq;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Tests.Fakes
{
    /// <summary>
    ///     图层仓储 Fake 实现，用于纯单元测试
    /// </summary>
    public class FakeLayerRepository : ILayerRepository
    {
        /// <summary>
        ///     内存中的图层列表
        /// </summary>
        public List<LayerInfo> Layers { get; set; } = new List<LayerInfo>();

        /// <summary>
        ///     记录 UpdateLayer 调用
        /// </summary>
        public List<LayerInfo> UpdatedLayers { get; } = new List<LayerInfo>();

        /// <summary>
        ///     模拟 GetAllLayers 失败
        /// </summary>
        public bool ShouldFailGetAll { get; set; }

        /// <summary>
        ///     模拟 GetCurrentLayerName 失败
        /// </summary>
        public bool ShouldFailGetCurrentLayer { get; set; }

        /// <summary>
        ///     模拟指定图层 UpdateLayer 失败
        /// </summary>
        public HashSet<string> UpdateFailLayerNames { get; } = new HashSet<string>();

        /// <summary>
        ///     当前图层名称
        /// </summary>
        public string CurrentLayerName { get; set; } = "0";

        /// <inheritdoc />
        public OpResult<LayerInfo> GetLayer(string name)
        {
            var layer = Layers.FirstOrDefault(item => item.Name == name);
            if (layer == null)
            {
                return OpResult<LayerInfo>.Fail($"图层 {name} 不存在");
            }

            return OpResult<LayerInfo>.Success(CloneLayer(layer));
        }

        /// <inheritdoc />
        public OpResult<IReadOnlyList<LayerInfo>> GetAllLayers()
        {
            if (ShouldFailGetAll)
            {
                return OpResult<IReadOnlyList<LayerInfo>>.Fail("模拟获取失败");
            }

            var layers = Layers.Select(CloneLayer).ToList();
            return OpResult<IReadOnlyList<LayerInfo>>.Success(layers.AsReadOnly());
        }

        /// <inheritdoc />
        public OpResult UpdateLayer(LayerInfo layer)
        {
            if (layer == null || string.IsNullOrEmpty(layer.Name))
            {
                return OpResult.Fail("图层信息无效");
            }

            var existing = Layers.FirstOrDefault(item => item.Name == layer.Name);
            if (existing == null)
            {
                return OpResult.Fail($"图层 {layer.Name} 不存在");
            }

            if (UpdateFailLayerNames.Contains(layer.Name))
            {
                return OpResult.Fail($"模拟更新图层 {layer.Name} 失败");
            }

            existing.IsLocked = layer.IsLocked;
            existing.IsFrozen = layer.IsFrozen;
            existing.ColorIndex = layer.ColorIndex;
            existing.LinetypeName = layer.LinetypeName;

            UpdatedLayers.Add(CloneLayer(layer));
            return OpResult.Success();
        }

        /// <inheritdoc />
        public OpResult<string> GetCurrentLayerName()
        {
            if (ShouldFailGetCurrentLayer)
            {
                return OpResult<string>.Fail("模拟获取当前图层失败");
            }

            return OpResult<string>.Success(CurrentLayerName);
        }

        /// <summary>
        ///     克隆图层信息，避免测试间共享引用
        /// </summary>
        /// <param name="layer">源图层</param>
        /// <returns>克隆后的图层</returns>
        private static LayerInfo CloneLayer(LayerInfo layer)
        {
            return new LayerInfo
            {
                Name = layer.Name,
                IsLocked = layer.IsLocked,
                IsFrozen = layer.IsFrozen,
                ColorIndex = layer.ColorIndex,
                LinetypeName = layer.LinetypeName
            };
        }
    }
}
