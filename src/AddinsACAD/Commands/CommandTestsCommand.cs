using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CommandTestsCommand))]

namespace AddinsACAD.Commands
{
    public class CommandTestsCommand
    {
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
                    (1, "CROPLINE", "CROPLINE", "裁剪直线"),
                    (2, "CROPALLLINES", "CROPALLLINES", "裁剪全部直线"),
                    (3, "CROPPOLYLINE", "CROPPOLYLINE", "裁剪多段线"),
                    (4, "CROPALLPOLYLINES", "CROPALLPOLYLINES", "裁剪全部多段线"),
                    (5, "CROPARC", "CROPARC", "裁剪圆弧"),
                    (6, "CROPALLARCS", "CROPALLARCS", "裁剪全部圆弧"),
                    (7, "CROPINSIDE", "CROPINSIDE", "裁剪全部对象（保留内部）"),
                    (8, "CROPOUTSIDE", "CROPOUTSIDE", "裁剪全部对象（保留外部）"),
                    (9, "BLOCKCLEANUP", "BLOCKCLEANUP", "块表清理"),
                    (10, "EXPLODEASSHOWN", "EXPLODEASSHOWN", "显示状态爆炸"),
                    (11, "GENERATEXCLIPBOUNDARY", "GENERATEXCLIPBOUNDARY", "生成外部参考裁剪边界"),
                };

                foreach (var cmd in commands)
                {
                    ed.WriteMessage($"\n{cmd.id}. {cmd.name}");
                    ed.WriteMessage($"   {cmd.description}");
                }

                ed.WriteMessage("\n\n请输入命令编号（1-11）: ");

                var keywordOptions = new PromptKeywordOptions(string.Empty);
                keywordOptions.AppendKeywordsToMessage = false;
                keywordOptions.Keywords.Add("1");
                keywordOptions.Keywords.Add("2");
                keywordOptions.Keywords.Add("3");
                keywordOptions.Keywords.Add("4");
                keywordOptions.Keywords.Add("5");
                keywordOptions.Keywords.Add("6");
                keywordOptions.Keywords.Add("7");
                keywordOptions.Keywords.Add("8");
                keywordOptions.Keywords.Add("9");
                keywordOptions.Keywords.Add("10");
                keywordOptions.Keywords.Add("11");

                var keywordResult = ed.GetKeywords(keywordOptions);
                if (keywordResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消命令选择。");
                    return;
                }

                string cmdToExecute = null;
                switch (keywordResult.StringResult)
                {
                    case "1": cmdToExecute = "CROPLINE"; break;
                    case "2": cmdToExecute = "CROPALLLINES"; break;
                    case "3": cmdToExecute = "CROPPOLYLINE"; break;
                    case "4": cmdToExecute = "CROPALLPOLYLINES"; break;
                    case "5": cmdToExecute = "CROPARC"; break;
                    case "6": cmdToExecute = "CROPALLARCS"; break;
                    case "7": cmdToExecute = "CROPINSIDE"; break;
                    case "8": cmdToExecute = "CROPOUTSIDE"; break;
                    case "9": cmdToExecute = "BLOCKCLEANUP"; break;
                    case "10": cmdToExecute = "EXPLODEASSHOWN"; break;
                    case "11": cmdToExecute = "GENERATEXCLIPBOUNDARY"; break;
                }

                if (string.IsNullOrEmpty(cmdToExecute))
                {
                    ed.WriteMessage("\n无效的选择。");
                    return;
                }

                ed.WriteMessage($"\n执行命令: {cmdToExecute}\n");
                try
                {
                    ed.Command(cmdToExecute);
                }
                catch (System.Exception cmdEx)
                {
                    ed.WriteMessage($"\n执行命令 {cmdToExecute} 失败: {cmdEx.Message}");
                    ed.WriteMessage("\n提示: 请重新 NETLOAD 最新编译的 DLL 后重试。");
                    Logger._.Error($"ed.Command({cmdToExecute}) 失败: {cmdEx.Message}", cmdEx);
                }
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nCOMMANDTESTS 执行失败: {ex.Message}");
                Logger._.Error($"COMMANDTESTS 执行失败: {ex.Message}", ex);
            }
        }
    }
}