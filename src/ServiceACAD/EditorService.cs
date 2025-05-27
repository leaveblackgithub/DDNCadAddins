using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace ServiceACAD
{
    /// <summary>
    ///     编辑器服务实现，提供与AutoCAD编辑器相关的操作
    /// </summary>
    public class EditorService : IEditorService
    {
        public EditorService(Editor editor)
        {
            CadEd = editor;
        }

        public Editor CadEd { get; set; }
        public void WriteMessage(string message) => CadEd.WriteMessage(message);
        public void Update() => CadEd.UpdateScreen();
        /// <summary>
        ///     获取要处理的图块引用
        /// </summary>
        /// <returns>图块引用的ObjectId列表</returns>
        public List<ObjectId> GetSelectedBlockReferences(string message)
        {
            var result = new List<ObjectId>();

            // 检查是否有预选的图块
            var preSelectedIds = CadEd.GetSelection();
            if (preSelectedIds.Status == PromptStatus.OK && preSelectedIds.Value.Count > 0)
            {
                // 过滤出图块引用
                result.AddRange(preSelectedIds.Value.GetObjectIds()
                    .Where(id => id.ObjectClass.DxfName == "INSERT"));
            }

            // 如果没有预选的图块，提示用户选择
            if (result.Count == 0)
            {
                // 创建过滤器，只允许选择图块
                var filterList = new[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };

                // 创建选择过滤器
                var filter = new SelectionFilter(filterList);

                // 创建选择选项
                var options = new PromptSelectionOptions
                {
                    MessageForAdding = message
                };

                // 提示用户选择图块
                var selectionResult = CadEd.GetSelection(options, filter);
                if (selectionResult.Status == PromptStatus.OK)
                {
                    result.AddRange(selectionResult.Value.GetObjectIds());
                }
            }

            return result;
        }
    }
}
