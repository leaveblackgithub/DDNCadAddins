using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     编辑器服务接口，提供与AutoCAD编辑器相关的操作
    /// </summary>
    public interface IEditorService
    {
        void WriteMessage(string message);
        void Update();

        /// <summary>
        ///     获取要处理的图块引用
        /// </summary>
        /// <returns>图块引用的ObjectId列表</returns>
        List<ObjectId> GetSelectedBlockReferences(string message);
    }
}
