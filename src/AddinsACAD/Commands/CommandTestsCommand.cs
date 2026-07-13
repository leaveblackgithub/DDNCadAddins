using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CommandTestsCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     手工测试命令集合 — 顶层入口，分发到子命令:
    ///     CROPTESTS / CLONEHATCH / GENERATEXCLIPBOUNDARY / EXPLODEASSHOWN /
    ///     GENERATEHATCHBOUNDARY / SUBTRACTCLOSEDCURVE.
    ///     子命令可独立执行，每个子命令返回 TestRecords 用于复盘追溯 BUG.
    ///     通过 SendStringToExecute 将子命令排入 AutoCAD 命令队列，避免嵌套交互式提示.
    /// </summary>
    public class CommandTestsCommand
    {
        [CommandMethod("MANUALCMDTESTS")]
        public void Execute()
        {
            try
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;

                var ch = AskSubCommand(ed);
                if (ch == null) return;

                var cmd = MapToCommand(ch);
                if (cmd == null)
                {
                    ed.WriteMessage("\n无效的选择。");
                    return;
                }

                SendCommand(ed, cmd);
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\nCOMMANDTESTS 执行失败: {ex.Message}");
                Logger._.Error($"COMMANDTESTS 执行失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     选择子命令:
        ///     C = CROPTESTS (裁剪测试)
        ///     K = CLONEHATCH (克隆填充)
        ///     G = GENERATEXCLIPBOUNDARY (生成XClip边界)
        ///     E = EXPLODEASSHOWN (按显示状态爆炸图块)
        ///     H = GENERATEHATCHBOUNDARY (提取Hatch边界)
        ///     B = CROPCLOSEDCURVE (封闭曲线裁剪)
        /// </summary>
        private static string AskSubCommand(Editor ed)
        {
            var kw = new PromptKeywordOptions(
                "\n选择测试子命令 C=CROPTESTS K=CLONEHATCH G=GENERATEXCLIPBOUNDARY E=EXPLODEASSHOWN H=GENERATEHATCHBOUNDARY B=CROPCLOSEDCURVE U=CROPTWOCLOSEDCURVE [C/K/G/E/H/B/U]");
            kw.Keywords.Add("C");
            kw.Keywords.Add("K");
            kw.Keywords.Add("G");
            kw.Keywords.Add("E");
            kw.Keywords.Add("H");
            kw.Keywords.Add("B");
            kw.Keywords.Add("U");
            kw.Keywords.Default = "C";
            kw.AllowNone = true;

            var res = ed.GetKeywords(kw);
            if (res.Status != PromptStatus.OK && res.Status != PromptStatus.Keyword)
                return null;

            return string.IsNullOrEmpty(res.StringResult) ? "C" : res.StringResult;
        }

        /// <summary>
        ///     将选项字符映射到具体 AutoCAD 命令.
        /// </summary>
        private static string MapToCommand(string ch)
        {
            switch (ch)
            {
                case "C": return "CROPTESTS";
                case "K": return "CLONEHATCH";
                case "G": return "GENERATEXCLIPBOUNDARY";
                case "E": return "EXPLODEASSHOWN";
                case "H": return "GENERATEHATCHBOUNDARY";
                case "B": return "CROPCLOSEDCURVE";
                case "U": return "CROPTWOCLOSEDCURVE";
                default:  return null;
            }
        }

        /// <summary>
        ///     通过 SendStringToExecute 将命令排入 AutoCAD 队列.
        /// </summary>
        private static void SendCommand(Editor ed, string cmd)
        {
            ed.WriteMessage($"\n执行命令: {cmd}\n");
            try
            {
                Application.DocumentManager.MdiActiveDocument
                    .SendStringToExecute(cmd + "\n", true, false, true);
            }
            catch (System.Exception cmdEx)
            {
                ed.WriteMessage($"\n执行命令 {cmd} 失败: {cmdEx.Message}");
                ed.WriteMessage("\n提示: 请重新 NETLOAD 最新编译的 DLL 后重试。");
                Logger._.Error($"SendStringToExecute({cmd}) 失败: {cmdEx.Message}", cmdEx);
            }
        }
    }
}
