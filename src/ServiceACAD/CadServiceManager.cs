using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace ServiceACAD
{
    /// <summary>
    ///     文档服务管理器，负责创建和提供全局的DocumentService引用
    /// </summary>
    public class CadServiceManager
    {
        private static readonly object _lockObj = new object();
        private static CadServiceManager _instance;

        // 当前活动文档的DocumentService
        private IDocumentService _currentDocumentService;


        // 上次使用的文档引用，用于检测文档变化
        private Document _lastActiveDocument;

        private CadServiceManager()
        {
            // 注册文档激活事件，以便在文档切换时更新服务
            Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
        }

        /// <summary>
        ///     获取当前活动文档的DocumentService
        /// </summary>
        public static CadServiceManager instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        if (_instance == null)
                        {
                            _instance = new CadServiceManager();
                        }
                    }
                }

                return _instance;
            }
        }
<<<<<<< HEAD

        public static IDocumentService _ => instance.ActiveServiceDoc;

        /// <summary>
        ///     获取当前活动文档的EditorService
        /// </summary>
        public static IEditorService ServiceEd => instance.ActiveServiceDoc.ServiceEd;

        /// <summary>
        ///     获取当前活动文档的EditorService
        /// </summary>
        /// <returns>当前活动文档的EditorService实例</returns>
        /// <summary>
        ///     获取当前活动文档的DocumentService
=======
        
        public static IDocumentService _=> instance.ActiveServiceDoc;
        
        /// <summary>
        /// 获取当前活动文档的EditorService
        /// </summary>
        public static IEditorService ServiceEd => instance.ActiveServiceDoc.ServiceEd;

        private CadServiceManager()
        {
            // 注册文档激活事件，以便在文档切换时更新服务
            Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
        }

        // 当前活动文档的DocumentService
        private IDocumentService _currentDocumentService;
        

        // 上次使用的文档引用，用于检测文档变化
        private Document _lastActiveDocument;

        /// <summary>
        /// 获取当前活动文档的EditorService
        /// </summary>
        /// <returns>当前活动文档的EditorService实例</returns>


        /// <summary>
        /// 获取当前活动文档的DocumentService
>>>>>>> 1f1ac48d411f700a4cb10cdc8df145885fa85ebd
        /// </summary>
        /// <returns>当前活动文档的DocumentService实例</returns>
        private IDocumentService ActiveServiceDoc
        {
            get
            {
                var currentDocument = Application.DocumentManager.MdiActiveDocument;

                // 如果当前没有活动文档，返回null
                if (currentDocument == null)
                {
                    return null;
                }

                // 检查文档是否发生变化或服务尚未创建
                if (_currentDocumentService == null || _lastActiveDocument != currentDocument)
                {
                    _lastActiveDocument = currentDocument;
                    _currentDocumentService = new DocumentService(currentDocument);
                }

                return _currentDocumentService;
            }
        }

        // /// <summary>
        // /// 获取指定文档标题的DocumentService
        // /// </summary>
        // /// <param name="drawingTitle">指定的文档标题</param>
        // /// <returns>匹配的DocumentService实例</returns>
        // public IDocumentService ServiceDocOf(string drawingTitle)
        // {
        //     // 如果未指定文档标题，返回当前活动文档的服务
        //     if (string.IsNullOrEmpty(drawingTitle))
        //         return ActiveServiceDoc;
        //         
        //     // 查找匹配标题的文档
        //     foreach (Document doc in Application.DocumentManager)
        //     {
        //         string docName = System.IO.Path.GetFileNameWithoutExtension(doc.Name);
        //         if (string.Equals(docName, drawingTitle, StringComparison.CurrentCultureIgnoreCase))
        //         {
        //             return new DocumentService(doc);
        //         }
        //     }
        //     
        //     // 如果未找到匹配的文档，返回当前活动文档的服务
        //     return ActiveServiceDoc;
        // }

        /// <summary>
        ///     文档激活事件处理程序
        /// </summary>
        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            // 清除当前服务引用，以便下次获取时创建新的服务
            _currentDocumentService = null;
            _lastActiveDocument = null;
        }

        /// <summary>
        ///     释放资源
        /// </summary>
        public void Dispose() => Application.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
    }
}
