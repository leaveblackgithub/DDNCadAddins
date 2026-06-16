using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using ServiceACAD;

[assembly: CommandClass(typeof(AddinsACAD.Commands.CommandTestsCommand))]

namespace AddinsACAD.Commands
{
    /// <summary>
    ///     统一测试菜单 — 两步交互：先选几何类型，再选操作类型.
    /// </summary>
    public class CommandTestsCommand
    {
        [CommandMethod("COMMANDTESTS")]
        public void Execute()
        {
            try
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;

                // Step 1: 选择几何类型
                var typeKw = new PromptKeywordOptions("\n选择要裁剪的对象类型 [直线(L)/多段线(P)/圆弧(A)/圆(C)/全部对象(O)]: ", "L P A C O");
                typeKw.Keywords.Add("L", "直线(L)", "直线 Line");
                typeKw.Keywords.Add("P", "多段线(P)", "多段线 Polyline");
                typeKw.Keywords.Add("A", "圆弧(A)", "圆弧 Arc");
                typeKw.Keywords.Add("C", "圆(C)", "圆 Circle");
                typeKw.Keywords.Add("O", "全部对象(O)", "所有可裁剪对象");
                typeKw.Keywords.Default = "O";
                typeKw.AllowNone = true;

                var typeRes = ed.GetKeywords(typeKw);
                if (typeRes.Status != PromptStatus.OK && typeRes.Status != PromptStatus.Keyword)
                    return;

                var typeCh = string.IsNullOrEmpty(typeRes.StringResult) ? "O" : typeRes.StringResult;

                // Step 2: 选择操作模式
                var modeKw = new PromptKeywordOptions("\n选择操作模式 [单选(M)/全选(A)]: ", "M A");
                modeKw.Keywords.Add("M", "单选(M)", "手动选择对象");
                modeKw.Keywords.Add("A", "全选(A)", "自动选择全部匹配对象");
                modeKw.Keywords.Default = "A";
                modeKw.AllowNone = true;

                var modeRes = ed.GetKeywords(modeKw);
                if (modeRes.Status != PromptStatus.OK && modeRes.Status != PromptStatus.Keyword)
                    return;

                var selectAll = string.IsNullOrEmpty(modeRes.StringResult) || modeRes.StringResult == "A";

                // 映射到具体命令
                string cmd = null;
                if (typeCh == "L") cmd = selectAll ? "CROPALLLINES" : "CROPLINE";
                else if (typeCh == "P") cmd = selectAll ? "CROPALLPOLYLINES" : "CROPPOLYLINE";
                else if (typeCh == "A") cmd = selectAll ? "CROPALLARCS" : "CROPARC";
                else if (typeCh == "C") cmd = selectAll ? "CROPALLCIRCLES" : "CROPCIRCLE";
                else if (typeCh == "O") cmd = "CROPINSIDE";

                if (string.IsNullOrEmpty(cmd))
                {
                    ed.WriteMessage("\n无效的选择。");
                    return;
                }

                ed.WriteMessage($"\n执行命令: {cmd}\n");
                try
                {
                    ed.Command(cmd);
                }
                catch (System.Exception cmdEx)
                {
                    ed.WriteMessage($"\n执行命令 {cmd} 失败: {cmdEx.Message}");
                    ed.WriteMessage("\n提示: 请重新 NETLOAD 最新编译的 DLL 后重试。");
                    Logger._.Error($"ed.Command({cmd}) 失败: {cmdEx.Message}", cmdEx);
                }
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\nCOMMANDTESTS 执行失败: {ex.Message}");
                Logger._.Error($"COMMANDTESTS 执行失败: {ex.Message}", ex);
            }
        }
    }
}