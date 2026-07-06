using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using ServiceACAD;
using OpResult = ServiceACAD.OpResult;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropBlockCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPBLOCK 命令 — 裁剪图块参照，支持包围盒分类 + ExplodeAsShown 炸开.
    ///     <para>流程：获取输入 → 包围盒分类 → Inside 保留 / Outside 删除 / Intersects 炸开 → 返回 TestRecord.</para>
    /// </summary>
    public class CropBlockCommand
    {
        /// <summary>
        ///     手动选择图块参照后裁剪.
        /// </summary>
        [CommandMethod("CROPBLOCK")]
        public void ExecuteCropBlock()
        {
            this.ExecuteCore(selectAll: false);
        }

        /// <summary>
        ///     自动选择所有图块参照后裁剪.
        /// </summary>
        [CommandMethod("CROPALLBLOCKS")]
        public void ExecuteCropAllBlocks()
        {
            this.ExecuteCore(selectAll: true);
        }

        /// <summary>
        ///     核心执行逻辑.
        /// </summary>
        /// <param name="selectAll">是否自动选择所有块参照.</param>
        private void ExecuteCore(bool selectAll)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager
                    .MdiActiveDocument;
                var ed = doc.Editor;

                // ── 1. 选择裁剪边界 ──
                var (boundary, boundaryPoints, boundaryId) = this.SelectBoundaryCurve(ed);
                if (boundary == null || boundaryPoints == null || boundaryPoints.Count < 3)
                {
                    return;
                }

                // ── 2. 选择块参照 ──
                List<ObjectId> blockRefIds;
                if (selectAll)
                {
                    blockRefIds = this.SelectAllBlocks(ed);
                }
                else
                {
                    blockRefIds = this.SelectBlocksManually(ed);
                }

                if (blockRefIds == null || blockRefIds.Count == 0)
                {
                    return;
                }

                // 排除边界自身（如果边界是块参照）
                blockRefIds.RemoveAll(id => id == boundaryId);
                if (blockRefIds.Count == 0)
                {
                    ed.WriteMessage("\n排除边界后没有其他图块。");
                    return;
                }

                // ── 3. 询问裁剪方向 ──
                bool? keepInside = AskCropDirection(ed);
                if (!keepInside.HasValue)
                    return; // 用户取消

                string commandName = selectAll ? "CROPALLBLOCKS" : "CROPBLOCK";
                string directionName = keepInside.Value ? "Inside" : "Outside";

                // ── 采集 UCS（用于 TestRecorder） ──
                TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);
                var capturedUcsOrigin = ucsOrigin;
                var capturedUcsX = ucsX;
                var capturedUcsY = ucsY;
                var capturedBoundaryVerts = boundaryPoints;
                var capturedKeepInside = keepInside.Value;

                // ── 4. 执行裁剪 ──
                ServiceACAD.OpResult<ServiceACAD.CropBlockResult> blockCropResult = null;
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var geoService = new CropGeometryService();
                        var cropService = new CropService(geoService);
                        var blockService = new CropBlockService(geoService, cropService);

                        blockCropResult = blockService.CropBlocks(
                            boundary, boundaryPoints.AsReadOnly(), blockRefIds, capturedKeepInside, serviceTrans);

                        if (blockCropResult == null || !blockCropResult.IsSuccess)
                        {
                            var msg = blockCropResult?.Message ?? "块裁剪返回空结果";
                            ed.WriteMessage($"\n裁剪图块失败: {msg}");
                        }
                        else
                        {
                            var br = blockCropResult.Data;
                            ed.WriteMessage(
                                $"\n{commandName} ({directionName}): 删除 {br.DeletedCount} 个, " +
                                $"炸开 {br.ExplodedCount} 个, 保留 {br.KeptCount} 个, 跳过 {br.SkippedCount} 个");
                        }

                        // ── TestRecorder 记录 ──
                        try
                        {
                            var blockData = blockCropResult?.IsSuccess == true ? blockCropResult.Data : null;
                            var record = new CropTestRecord
                            {
                                Command = commandName,
                                Direction = directionName,
                                IsSuccess = blockData != null,
                                UcsOrigin = capturedUcsOrigin,
                                UcsXAxis = capturedUcsX,
                                UcsYAxis = capturedUcsY,
                                BoundaryVertices = capturedBoundaryVerts,
                                BoundaryVertexCount = capturedBoundaryVerts.Count,
                                TotalEntityCount = blockRefIds.Count,
                                DeletedCount = blockData?.DeletedCount ?? 0,
                                SplitCount = blockData?.ExplodedCount ?? 0,
                                KeptCount = blockData?.KeptCount ?? 0,
                                SkippedCount = blockData?.SkippedCount ?? 0,
                            };

                            if (blockData != null)
                            {
                                record.Entities = TestRecorder.CollectSnapshots(
                                    serviceTrans, blockRefIds, boundaryPoints, new CropGeometryService());
                            }

                            var uid = TestRecorder.Record(record);
                            ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                        }
                        catch (System.Exception recEx)
                        {
                            Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                            ed.WriteMessage($"\n[TestRecorder] 记录失败: {recEx.Message}");
                        }

                        return OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"CROPBLOCK 内部失败: {ex.Message}", ex);
                        return OpResult.Fail($"裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPBLOCK 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPBLOCK 命令失败: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  边界选择
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     选择闭合曲线作为裁剪边界.
        /// </summary>
        /// <returns>(ICropBoundary, 多边形顶点列表, 边界ObjectId).</returns>
        private (DDNCadAddins.Core.Interfaces.ICropBoundary, List<DDNCadAddins.Core.Models.Point2D>, ObjectId)
            SelectBoundaryCurve(Editor ed)
        {
            var boundaryId = ObjectId.Null;
            try
            {
                var opt = new PromptEntityOptions("\n选择闭合曲线作为裁剪边界");
                opt.SetRejectMessage("请选择圆、椭圆、闭合多段线或闭合样条线。");
                opt.AddAllowedClass(typeof(Curve), exactMatch: false);

                var res = ed.GetEntity(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n*取消*");
                    return (null, null, ObjectId.Null);
                }

                boundaryId = res.ObjectId;
                var capturedId = boundaryId;
                DDNCadAddins.Core.Interfaces.ICropBoundary boundary = null;
                var points = new List<DDNCadAddins.Core.Models.Point2D>();

                CadServiceManager._.ExecuteInTransactions(null, serviceTrans =>
                {
                    var curve = serviceTrans.GetObject<Curve>(capturedId);
                    if (curve == null)
                    {
                        return;
                    }

                    if (!curve.Closed)
                    {
                        ed.WriteMessage("\n所选的边界曲线未闭合，请选择闭合曲线。");
                        return;
                    }

                    // 使用 CropBoundaryFactory 创建精确边界
                    boundary = CropBoundaryFactory.CreateFromCurve(curve);

                    // 获取近似多边形（用于嵌套块包围盒粗筛）
                    if (boundary != null)
                    {
                        var polygon = boundary.GetApproximatePolygon();
                        if (polygon != null && polygon.Count >= 3)
                        {
                            points.AddRange(polygon);
                        }
                    }
                });

                if (boundary == null || points.Count < 3)
                {
                    ed.WriteMessage("\n边界曲线无效或顶点不足，请选择更大的闭合曲线。");
                    return (null, null, ObjectId.Null);
                }

                return (boundary, points, boundaryId);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择边界失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择边界失败: {ex.Message}");
                return (null, null, ObjectId.Null);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  块参照选择
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     手动选择块参照.
        /// </summary>
        private List<ObjectId> SelectBlocksManually(Editor ed)
        {
            try
            {
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT"),
                });

                var opt = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要裁剪的图块参照",
                };

                var res = ed.GetSelection(opt, filter);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n*取消*");
                    return null;
                }

                return res.Value.GetObjectIds().ToList();
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择块参照失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择块参照失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     自动选择所有块参照.
        /// </summary>
        private List<ObjectId> SelectAllBlocks(Editor ed)
        {
            try
            {
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT"),
                });

                var res = ed.SelectAll(filter);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未找到图块参照");
                    return null;
                }

                return res.Value.GetObjectIds().ToList();
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择所有块参照失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择所有块参照失败: {ex.Message}");
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  裁剪方向选择
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        ///     询问裁剪方向.
        /// </summary>
        /// <returns>true=保留内部，false=保留外部，null=取消.</returns>
        private static bool? AskCropDirection(Editor ed)
        {
            try
            {
                const string kwInside = "Inside";
                const string kwOutside = "Outside";

                var opt = new PromptKeywordOptions(
                    "\n选择裁剪方向 [内部(Inside)/外部(Outside)]", $"{kwInside} {kwOutside}")
                {
                    AllowNone = false,
                };
                opt.Keywords.Add(kwInside);
                opt.Keywords.Add(kwOutside);
                opt.Keywords.Default = kwInside;

                var res = ed.GetKeywords(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n*取消*");
                    return null;
                }

                return res.StringResult == kwInside;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"询问裁剪方向失败: {ex.Message}");
                return null;
            }
        }
    }
}
