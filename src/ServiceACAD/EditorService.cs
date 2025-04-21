using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace ServiceACAD
{
    /// <summary>
    /// 编辑器服务实现，提供与AutoCAD编辑器相关的操作
    /// </summary>
    public class EditorService : IEditorService
    {
        public EditorService(Editor editor)
        {
            CadEd = editor;
        }

        public Editor CadEd { get; set; }
       
    }
} 
