using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Core.Models;

namespace ServiceACAD
{
    /// <summary>
    ///     XClip 边界生成服务 — 批量处理图块的 XClip 边界生成.
    ///     将业务逻辑从 <see cref="GenerateXclipBoundaryCommand"/> 中抽取，实现输入/输出与逻辑分离.
    /// </summary>
    public static class XClipBoundaryService
    {
        /// <summary>
        ///     批量生成 XClip 边界的结果.
        /// </summary>
        public sealed class BatchResult
        {
            /// <summary>成功生成边界的图块数量.</summary>
            public int SuccessCount { get; set; }

            /// <summary>失败信息列表.</summary>
            public List<string> FailedMessages { get; set; } = new List<string>();

            /// <summary>是否全部成功.</summary>
            public bool IsAllSuccess => FailedMessages.Count == 0;
        }

        /// <summary>
        ///     批量生成 XClip 边界 — 为指定图块参照列表生成 XClip 边界多段线.
        /// </summary>
        /// <param name="serviceTrans">事务服务</param>
        /// <param name="blockRefIds">图块参照 ObjectId 列表</param>
        /// <returns>批量处理结果</returns>
        public static OpResult<BatchResult> GenerateBatch(
            ITransactionService serviceTrans, IReadOnlyList<ObjectId> blockRefIds)
        {
            try
            {
                if (serviceTrans == null)
                    return OpResult<BatchResult>.Fail("事务服务为空");
                if (blockRefIds == null || blockRefIds.Count == 0)
                    return OpResult<BatchResult>.Fail("图块参照列表为空");

                var result = new BatchResult();

                foreach (var blockRefId in blockRefIds)
                {
                    if (!blockRefId.IsValid || blockRefId.IsErased)
                    {
                        result.FailedMessages.Add($"无效的图块 ID: {blockRefId}");
                        continue;
                    }

                    var blockService = serviceTrans.Block.GetBlockService(blockRefId);
                    if (blockService == null)
                    {
                        result.FailedMessages.Add($"无法获取图块服务: {blockRefId}");
                        continue;
                    }

                    if (!blockService.IsXclipped())
                    {
                        result.FailedMessages.Add($"图块不存在 XClip 边界 (名称: {blockService.Name})");
                        continue;
                    }

                    var genResult = blockService.GenerateXclipBoundary();
                    if (genResult.IsSuccess)
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailedMessages.Add(
                            $"生成 XClip 边界失败: {genResult.Message} (名称: {blockService.Name})");
                    }
                }

                return OpResult<BatchResult>.Success(result);
            }
            catch (Exception ex)
            {
                Logger._.Error($"批量生成 XClip 边界失败: {ex.Message}", ex);
                return OpResult<BatchResult>.Fail($"批量生成 XClip 边界失败: {ex.Message}");
            }
        }
    }
}
