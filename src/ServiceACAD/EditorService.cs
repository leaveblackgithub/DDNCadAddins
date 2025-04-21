<<<<<<< HEAD
=======
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
>>>>>>> 1f1ac48d411f700a4cb10cdc8df145885fa85ebd
using Autodesk.AutoCAD.EditorInput;

namespace ServiceACAD
{
    /// <summary>
<<<<<<< HEAD
    ///     编辑器服务实现，提供与AutoCAD编辑器相关的操作
=======
    /// 编辑器服务实现，提供与AutoCAD编辑器相关的操作
>>>>>>> 1f1ac48d411f700a4cb10cdc8df145885fa85ebd
    /// </summary>
    public class EditorService : IEditorService
    {
        public EditorService(Editor editor)
        {
            CadEd = editor;
        }

        public Editor CadEd { get; set; }
<<<<<<< HEAD
    }
}
=======
       
    }
} 
>>>>>>> 1f1ac48d411f700a4cb10cdc8df145885fa85ebd
