using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CloneHatchCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     CLONEHATCH 命令 — 提取源 Hatch 的填充参数（图案 / 比例 / 原点 / 角度），
    ///     然后让用户选取新的边界对象，用相同的参数对新边界进行填充.
    ///     交互流程：选择源 Hatch → 输出填充参数 → 选取新边界 → 用相同参数填充.
    /// </summary>
    public class CloneHatchCommand
    {
        /// <summary>
        ///     描述一个 Hatch 的可复制填充参数.
        /// </summary>
        private struct HatchParams
        {
            public HatchPatternType PatternType;
            public string PatternName;
            public double PatternScale;
            public double PatternAngle;
            public Point2d Origin;
            public HatchStyle Style;
            public Vector3d Normal;
            public double Elevation;
            public string Layer;
            public Autodesk.AutoCAD.Colors.Color Color;
        }

        [CommandMethod("CLONEHATCH")]
        public void Execute()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                // Step 1: 选择源 Hatch
                var peo = new PromptEntityOptions("\n选择源 Hatch（提取填充参数）: ");
                peo.SetRejectMessage("\n请选择一个 Hatch 实体。");
                peo.AddAllowedClass(typeof(Hatch), false);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消选择。");
                    return;
                }

                var sourceId = per.ObjectId;

                // Step 2: 在只读事务中提取并输出参数
                var p = new HatchParams();
                var gotParams = false;
                CadServiceManager._.ExecuteInTransactions("", ts =>
                {
                    var src = ts.GetObject<Hatch>(sourceId, OpenMode.ForRead);
                    if (src == null || src.IsErased)
                    {
                        ed.WriteMessage("\n源 Hatch 无效或已被删除。");
                        return;
                    }

                    p.PatternType  = src.PatternType;
                    p.PatternName  = src.PatternName;
                    p.PatternScale = src.PatternScale;
                    p.PatternAngle = src.PatternAngle;
                    p.Origin       = src.Origin;
                    p.Style        = src.HatchStyle;
                    p.Normal       = src.Normal;
                    p.Elevation    = src.Elevation;
                    p.Layer        = src.Layer;
                    p.Color        = src.Color;
                    gotParams      = true;

                    ed.WriteMessage(
                        $"\n源 Hatch 参数：\n" +
                        $"  PATTERN  = {src.PatternName} ({src.PatternType})\n" +
                        $"  比例     = {src.PatternScale}\n" +
                        $"  原点     = ({src.Origin.X:F4}, {src.Origin.Y:F4})\n" +
                        $"  角度     = {src.PatternAngle:F6} rad ({src.PatternAngle * 180.0 / Math.PI:F2}°)");
                });

                if (!gotParams)
                    return;

                // Step 3: 选取新边界对象（可多选）
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选取新边界对象（闭合曲线）: "
                };
                var ssr = ed.GetSelection(pso);
                if (ssr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选取边界，取消。");
                    return;
                }

                var boundaryIds = ssr.Value.GetObjectIds();
                if (boundaryIds == null || boundaryIds.Length == 0)
                {
                    ed.WriteMessage("\n未选取边界，取消。");
                    return;
                }

                // Step 4: 用相同参数填充新边界
                CadServiceManager._.ExecuteInCommandTransaction(ts =>
                {
                    try
                    {
                        var created = CreateHatchOnBoundaries(ts, p, boundaryIds, ed);
                        if (created)
                        {
                            ed.WriteMessage(
                                $"\n已用源参数填充新边界：PATTERN={p.PatternName}, 比例={p.PatternScale}, " +
                                $"角度={p.PatternAngle * 180.0 / Math.PI:F2}°。");
                            return OpResult.Success();
                        }

                        ed.WriteMessage("\n未能创建填充。");
                        return OpResult.Fail("未能创建填充");
                    }
                    catch (System.Exception ex)
                    {
                        Logger._.Error($"CLONEHATCH 填充失败: {ex.Message}", ex);
                        ed.WriteMessage($"\n填充失败: {ex.Message}");
                        return OpResult.Fail(ex.Message);
                    }
                });
            }
            catch (System.Exception ex)
            {
                Logger._.Error($"CLONEHATCH 命令失败: {ex.Message}", ex);
                ed.WriteMessage($"\nCLONEHATCH 命令失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     使用给定填充参数，在选定的边界对象上创建关联填充.
        /// </summary>
        /// <param name="ts">事务服务.</param>
        /// <param name="p">源 Hatch 提取的填充参数.</param>
        /// <param name="boundaryIds">边界对象的 ObjectId 数组.</param>
        /// <param name="ed">编辑器（用于输出提示）.</param>
        /// <returns>是否成功创建填充.</returns>
        private static bool CreateHatchOnBoundaries(
            ITransactionService ts, HatchParams p, ObjectId[] boundaryIds, Editor ed)
        {
            var hatch = new Hatch
            {
                Normal    = p.Normal,
                Elevation = p.Elevation,
                Layer     = p.Layer,
                Color     = p.Color,
            };

            // 必须先加入数据库，AppendLoop / EvaluateHatch 才能正常工作
            if (ts.AppendEntityToCurrentSpace(hatch).IsNull)
            {
                ed.WriteMessage("\n无法将填充加入数据库。");
                return false;
            }

            // 设置图案（关联前）
            hatch.SetHatchPattern(p.PatternType, p.PatternName);
            hatch.Associative = true;
            hatch.HatchStyle  = p.Style;

            // 为每个边界对象追加一个外部环
            var appended = 0;
            foreach (var id in boundaryIds)
            {
                if (id.IsNull) continue;
                var ent = ts.GetObject<Entity>(id, OpenMode.ForRead);
                if (!(ent is Curve)) continue; // 仅接受曲线作为边界

                try
                {
                    var loopIds = new ObjectIdCollection { id };
                    hatch.AppendLoop(HatchLoopTypes.Default, loopIds);
                    appended++;
                }
                catch (System.Exception ex)
                {
                    Logger._.Warn($"边界 {id} 追加失败: {ex.Message}");
                }
            }

            if (appended == 0)
            {
                ed.WriteMessage("\n所选对象均不是有效的闭合边界。");
                hatch.Erase();
                return false;
            }

            // 应用与源相同的比例、角度、原点（在 SetHatchPattern 之后设置）
            hatch.PatternScale = p.PatternScale;
            hatch.PatternAngle = p.PatternAngle;
            hatch.Origin       = p.Origin;
            // 重新应用图案以使新的比例/角度生效
            hatch.SetHatchPattern(p.PatternType, p.PatternName);

            hatch.EvaluateHatch(true);
            return true;
        }
    }
}
