using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CommandTestsCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     命令测试菜单 - 集合需要手动测试的命令，提供编号可选菜单.
    /// </summary>
    public class CommandTestsCommand
    {
        /// <summary>
        ///     执行 COMMANDTESTS 命令，显示可选的手动测试命令菜单.
        /// </summary>
        [CommandMethod("COMMANDTESTS")]
        public void Execute()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                ed.WriteMessage("\n======== 手动测试命令菜单 ========\n");

                var commands = new List<(int id, string name, string cmdName, string description)>
                {
                    (1, "CROPLINE", "CROPLINE", "裁剪直线 - 选边界（单选）→ 选直线 → 选方向"),
                    (2, "CROPALLLINES", "CROPALLLINES", "裁剪全部直线 - 选边界（单选）→ 自动选直 → 选方向"),
                    (3, "CROPINSIDE", "CROPINSIDE", "裁剪全部对象（保留内部）- 支持多种对象类型"),
                    (4, "CROPOUTSIDE", "CROPOUTSIDE", "裁剪全部对象（保留外部）- 支持多种对象类型"),
                    (5, "BLOCKCLEANUP", "BLOCKCLEANUP", "块表清理 - 删除未引用的块定义"),
                    (6, "EXPLODEASSHOWN", "EXPLODEASSHOWN", "显示状态爆炸 - 爆炸分解块引用"),
                    (7, "GENERATEXCLIPBOUNDARY", "GENERATEXCLIPBOUNDARY", "生成外部参考裁剪边界"),
                };

                foreach (var cmd in commands)
                {
                    ed.WriteMessage($"\n{cmd.id}. {cmd.name}");
                    ed.WriteMessage($"   {cmd.description}");
                }

                ed.WriteMessage("\n\n请输入命令编号（1-7）或命令名称执行对应操作，Esc 取消: ");

                var keywordOptions = new PromptKeywordOptions(string.Empty);
                keywordOptions.AppendKeywordsToMessage = false;
                keywordOptions.Keywords.Add("1");
                keywordOptions.Keywords.Add("2");
                keywordOptions.Keywords.Add("3");
                keywordOptions.Keywords.Add("4");
                keywordOptions.Keywords.Add("5");
                keywordOptions.Keywords.Add("6");
                keywordOptions.Keywords.Add("7");

                var keywordResult = ed.GetKeywords(keywordOptions);
                if (keywordResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消命令选择。");
                    return;
                }

                string cmdToExecute = null;
                switch (keywordResult.StringResult)
                {
                    case "1":
                        cmdToExecute = "CROPLINE";
                        break;
                    case "2":
                        cmdToExecute = "CROPALLLINES";
                        break;
                    case "3":
                        cmdToExecute = "CROPINSIDE";
                        break;
                    case "4":
                        cmdToExecute = "CROPOUTSIDE";
                        break;
                    case "5":
                        cmdToExecute = "BLOCKCLEANUP";
                        break;
                    case "6":
                        cmdToExecute = "EXPLODEASSHOWN";
                        break;
                    case "7":
                        cmdToExecute = "GENERATEXCLIPBOUNDARY";
                        break;
                }

                if (string.IsNullOrEmpty(cmdToExecute))
                {
                    ed.WriteMessage("\n无效的选择。");
                    return;
                }

                ed.WriteMessage($"\n执行命令: {cmdToExecute}\n");
                ed.Command(cmdToExecute);
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nCOMMANDTESTS 执行失败: {ex.Message}");
            }
        }
    }
}
