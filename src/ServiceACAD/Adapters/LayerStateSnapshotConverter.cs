using System;
using Autodesk.AutoCAD.DatabaseServices;
using CoreLayerStateEntry = DDNCadAddins.Core.Models.LayerStateEntry;
using CoreLayerStateSnapshot = DDNCadAddins.Core.Models.LayerStateSnapshot;

namespace ServiceACAD.Adapters
{
    /// <summary>
    ///     ServiceACAD 与 Core 图层状态快照之间的转换工具
    /// </summary>
    internal static class LayerStateSnapshotConverter
    {
        /// <summary>
        ///     将 Core 快照转换为 ServiceACAD 快照（ObjectId 键）
        /// </summary>
        /// <param name="coreSnapshot">Core 快照</param>
        /// <param name="transactionService">事务服务</param>
        /// <returns>ServiceACAD 快照</returns>
        public static LayerStateSnapshot ToLegacy(
            CoreLayerStateSnapshot coreSnapshot,
            ITransactionService transactionService)
        {
            var legacySnapshot = new LayerStateSnapshot();
            if (coreSnapshot == null)
            {
                return legacySnapshot;
            }

            var layerTable = transactionService.Style.GetLayerTable(OpenMode.ForRead);
            if (layerTable == null)
            {
                return legacySnapshot;
            }

            foreach (var entry in coreSnapshot.States)
            {
                if (string.IsNullOrEmpty(entry.Key) || !layerTable.Has(entry.Key))
                {
                    continue;
                }

                var layerId = layerTable[entry.Key];
                legacySnapshot.States[layerId] = new LayerStateEntry
                {
                    IsLocked = entry.Value.IsLocked,
                    IsFrozen = entry.Value.IsFrozen
                };
            }

            return legacySnapshot;
        }

        /// <summary>
        ///     将 ServiceACAD 快照转换为 Core 快照（图层名称键）
        /// </summary>
        /// <param name="legacySnapshot">ServiceACAD 快照</param>
        /// <param name="transactionService">事务服务</param>
        /// <returns>Core 快照</returns>
        public static CoreLayerStateSnapshot ToCore(
            LayerStateSnapshot legacySnapshot,
            ITransactionService transactionService)
        {
            var coreSnapshot = new CoreLayerStateSnapshot();
            if (legacySnapshot == null)
            {
                return coreSnapshot;
            }

            foreach (var entry in legacySnapshot.States)
            {
                try
                {
                    if (!entry.Key.IsValid || entry.Key.IsErased)
                    {
                        continue;
                    }

                    var layer = transactionService.GetObject<LayerTableRecord>(entry.Key, OpenMode.ForRead);
                    if (layer == null || layer.IsErased || string.IsNullOrEmpty(layer.Name))
                    {
                        continue;
                    }

                    coreSnapshot.States[layer.Name] = new CoreLayerStateEntry
                    {
                        IsLocked = entry.Value.IsLocked,
                        IsFrozen = entry.Value.IsFrozen
                    };
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Logger._.Warn($"跳过无效图层 (ObjectId={entry.Key}): {ex.ErrorStatus}");
                }
                catch (Exception ex)
                {
                    Logger._.Warn($"转换图层快照条目失败 (ObjectId={entry.Key}): {ex.Message}");
                }
            }

            return coreSnapshot;
        }
    }
}
