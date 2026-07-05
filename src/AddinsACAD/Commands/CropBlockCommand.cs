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

                // ── 3. 询问裁剪方向 ──
                bool keepInside = this.AskCropDirection(ed);
                string directionName = keepInside ? "Inside" : "Outside";

                // ── 4. 执行裁剪 ──
                CadServiceManager._.ExecuteInCommandTransaction(serviceTrans =>
                {
                    try
                    {
                        var geoService = new CropGeometryService();
                        var cropService = new CropService(geoService);
                        var input = new CropInput
                        {
                            Boundary = boundary,
                            BoundaryPoints = boundaryPoints.AsReadOnly(),
                            EntityIds = blockRefIds,
                            TransactionService = serviceTrans,
                        };

                        var result = keepInside
                            ? cropService.CropInside(input)
                            : cropService.CropOutside(input);

                        if (!result.IsSuccess)
                        {
                            ed.WriteMessage($"\n裁剪失败: {result.Message}");
                            return ServiceACAD.OpResult.Fail(result.Message);
                        }

                        var data = result.Data;
                        ed.WriteMessage(
                            $"\nCROPBLOCK({directionName}): 删除 {data.DeletedCount} 个, " +
                            $"拆分 {data.SplitCount} 个, 保留 {data.KeptCount} 个, 跳过 {data.SkippedCount} 个");

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

                // 创建 ICropBoundary
                var boundaryResult = CropBoundaryFactory.CreateBoundary(boundaryId);
                if (!boundaryResult.IsSuccess)
                {
                    ed.WriteMessage($"\n{boundaryResult.Message}");
                    return (null, null, ObjectId.Null);
                }

                var boundary = boundaryResult.Data;
                var polygon = boundary.GetApproximatePolygon();

                if (polygon.Count < 3)
                {
                    ed.WriteMessage("\n边界多边形顶点不足");
                    return (null, null, ObjectId.Null);
                }

                return (boundary, new List<DDNCadAddins.Core.Models.Point2D>(polygon), boundaryId);
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"选择边界失败: {ex.Message}", ex);
                ed.WriteMessage($"\n选择边界失败: {ex.Message}");
                return (null, null, ObjectId.Null);
            }
        }

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

        /// <summary>
        ///     询问裁剪方向.
        /// </summary>
        /// <returns>true=保留内部，false=保留外部.</returns>
        private bool AskCropDirection(Editor ed)
        {
            try
            {
                const string kwInside = "Inside";
                const string kwOutside = "Outside";

                var opt = new PromptKeywordOptions(
                    "\n选择裁剪方向", $"{kwInside} / {kwOutside}")
                {
                    AllowNone = false,
                };
                opt.Keywords.Add(kwInside);
                opt.Keywords.Add(kwOutside);
                opt.Keywords.Default = kwInside;

                var res = ed.GetKeywords(opt);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n*取消*，默认保留内部");
                    return true;
                }

                return res.StringResult == kwInside;
            }
            catch (System.Exception ex)
            {
                Logger._.Warn($"询问裁剪方向失败: {ex.Message}");
                return true;
            }
        }
    }
}
