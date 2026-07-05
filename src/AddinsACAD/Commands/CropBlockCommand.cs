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

[assembly: CommandClass(typeof(AddinsACAD.Commands.CropBlockCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CROPBLOCK 命令 — 裁剪图块参照，支持包围盒分类 + 爆炸裁剪（仅最外层）.
    ///     <para>嵌套块采用包围盒粗筛（Inside→保留 / Outside→删除 / Intersects→跳过）.</para>
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

                // ── 4. 分离 Hatch 和非 Hatch 块 ──
                //     （Hatch 块需要特殊处理：爆炸后 hatch 需重建边界）
                var nonHatchBlockIds = new List<ObjectId>();
                var hatchBlockIds = new List<ObjectId>();
                foreach (var id in blockRefIds)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    CadServiceManager._.ExecuteInTransactions(null, ts =>
                    {
                        var blkRef = ts.GetObject<BlockReference>(id, OpenMode.ForRead);
                        if (blkRef == null) return;
                        var blkDef = ts.GetObject<BlockTableRecord>(blkRef.BlockTableRecord);
                        if (blkDef == null) return;

                        // 检查块定义中是否包含 Hatch
                        bool hasHatch = false;
                        foreach (ObjectId childId in blkDef)
                        {
                            if (!childId.IsValid) continue;
                            if (childId.ObjectClass != null &&
                                childId.ObjectClass.Name == "AcDbHatch")
                            {
                                hasHatch = true;
                                break;
                            }
                        }

                        if (hasHatch)
                            hatchBlockIds.Add(id);
                        else
                            nonHatchBlockIds.Add(id);
                    });
                }

                // ── 采集 UCS（用于 TestRecorder） ──
                ServiceACAD.TestRecorder.CaptureUcs(out var ucsOrigin, out var ucsX, out var ucsY);
                var capturedUcsOrigin = ucsOrigin;
                var capturedUcsX = ucsX;
                var capturedUcsY = ucsY;
                var capturedBoundaryVerts = boundaryPoints;
                var capturedKeepInside = keepInside.Value;

                // ── 5. 执行裁剪 ──
                int hatchNewCreated = 0;
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var geoService = new CropGeometryService();
                        var cropService = new CropService(geoService);
                        CropResult cropResult = null;

                        // ── 处理非 Hatch 块 ──
                        if (nonHatchBlockIds.Count > 0)
                        {
                            var input = new CropInput
                            {
                                Boundary = boundary,
                                BoundaryPoints = boundaryPoints.AsReadOnly(),
                                EntityIds = nonHatchBlockIds,
                                TransactionService = serviceTrans,
                            };

                            var result = capturedKeepInside
                                ? cropService.CropInside(input)
                                : cropService.CropOutside(input);

                            if (!result.IsSuccess)
                            {
                                ed.WriteMessage($"\n裁剪非 Hatch 块失败: {result.Message}");
                                return ServiceACAD.OpResult.Fail(result.Message);
                            }

                            cropResult = result.Data;
                            ed.WriteMessage(
                                $"\n{commandName} 非 Hatch 块: 删除 {cropResult.DeletedCount} 个, " +
                                $"拆分 {cropResult.SplitCount} 个, 保留 {cropResult.KeptCount} 个, 跳过 {cropResult.SkippedCount} 个");
                        }

                        // ── 处理 Hatch 块 ──
                        if (hatchBlockIds.Count > 0)
                        {
                            ed.WriteMessage($"\n正在处理 {hatchBlockIds.Count} 个含 Hatch 的图块...");
                            foreach (var hatchBlockId in hatchBlockIds)
                            {
                                var hatchResult = ProcessHatchBlock(
                                    ed, hatchBlockId, boundaryId, capturedKeepInside, serviceTrans);
                                hatchNewCreated += hatchResult.NewHatchesCreated;
                            }
                            ed.WriteMessage($"\nHatch 块处理完成，新增 {hatchNewCreated} 个填充。");
                        }

                        // ── TestRecorder 记录 ──
                        try
                        {
                            var record = new CropTestRecord
                            {
                                Command = commandName,
                                Direction = directionName,
                                IsSuccess = true,
                                UcsOrigin = capturedUcsOrigin,
                                UcsXAxis = capturedUcsX,
                                UcsYAxis = capturedUcsY,
                                BoundaryVertices = capturedBoundaryVerts,
                                BoundaryVertexCount = capturedBoundaryVerts.Count,
                                TotalEntityCount = blockRefIds.Count,
                                DeletedCount = cropResult?.DeletedCount ?? 0,
                                SplitCount = (cropResult?.SplitCount ?? 0) + hatchNewCreated,
                                KeptCount = (cropResult?.KeptCount ?? 0) + hatchNewCreated,
                                SkippedCount = cropResult?.SkippedCount ?? 0,
                            };

                            if (nonHatchBlockIds.Count > 0)
                            {
                                record.Entities = ServiceACAD.TestRecorder.CollectSnapshots(
                                    serviceTrans, nonHatchBlockIds, boundaryPoints, geoService);
                            }

                            var uid = ServiceACAD.TestRecorder.Record(record);
                            ed.WriteMessage($"\n[TestRecorder] UID: {uid}");
                        }
                        catch (System.Exception recEx)
                        {
                            Logger._.Warn($"TestRecorder 记录失败: {recEx.Message}");
                            ed.WriteMessage($"\n[TestRecorder] 记录失败: {recEx.Message}");
                        }

                        ed.WriteMessage(
                            $"\n{commandName} ({directionName}) 完成: 删除 {cropResult?.DeletedCount ?? 0} 个, " +
                            $"{hatchNewCreated} 个 Hatch 重建");

                        return ServiceACAD.OpResult.Success();
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"CROPBLOCK 内部失败: {ex.Message}", ex);
                        return ServiceACAD.OpResult.Fail($"裁剪失败: {ex.Message}");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CROPBLOCK 命令失败: {ex.Message}", ex);
                CadServiceManager.ServiceEd.WriteMessage($"\nCROPBLOCK 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理含 Hatch 的图块：爆炸 → 裁剪 Hatch → 重建.
        /// </summary>
        private static CropHatchCommand.ProcessHatchesResult ProcessHatchBlock(
            Editor ed,
            ObjectId blockRefId,
            ObjectId boundaryId,
            bool keepInside,
            ITransactionService serviceTrans)
        {
            try
            {
                var blockRef = serviceTrans.GetObject<BlockReference>(blockRefId);
                if (blockRef == null || blockRef.IsErased)
                    return new CropHatchCommand.ProcessHatchesResult();

                // 爆炸图块，获取子实体
                var exploder = new BlockExploder(serviceTrans);
                var explodeResult = exploder.Explode(blockRef);
                if (!explodeResult.IsSuccess)
                {
                    ed.WriteMessage($"\n爆炸图块失败: {explodeResult.Message}");
                    return new CropHatchCommand.ProcessHatchesResult();
                }

                // 筛选 Hatch 实体
                var childIds = explodeResult.Data.EntityIds;
                var hatchIds = new List<ObjectId>();
                foreach (var childId in childIds)
                {
                    if (!childId.IsValid || childId.IsErased) continue;
                    if (childId.ObjectClass != null &&
                        childId.ObjectClass.Name == "AcDbHatch")
                    {
                        hatchIds.Add(childId);
                    }
                }

                if (hatchIds.Count == 0)
                    return new CropHatchCommand.ProcessHatchesResult();

                // 委托给 CropHatchCommand.ProcessHatches
                return CropHatchCommand.ProcessHatches(ed, hatchIds, boundaryId, keepInside);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"处理 Hatch 块失败: {ex.Message}", ex);
                return new CropHatchCommand.ProcessHatchesResult();
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
                    boundary = ServiceACAD.CropBoundaryFactory.CreateFromCurve(curve);

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
        //  裁剪方向选择（复用 CropArcCommand 的 AskCropDirection 模式）
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
                    "\n选择裁剪方向 [内部(Inside)/外部(Outside)]", $"{kwInside} / {kwOutside}")
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
