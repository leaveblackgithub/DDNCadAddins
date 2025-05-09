using System;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace ServiceACAD
{
    /// <summary>
    ///     文档服务管理器，负责创建和提供全局的DocumentService引用
    /// </summary>
    public class CadServiceManager
    {
        public const string Layer0 = "0";
        public const short ColorIndexByBlock = 0;
        public const string StrByBlock = "BYBLOCK";
        public const short ColorIndexByLayer = 256;
        public const short ColorIndexGreen = 3;
        public const short ColorIndexWhite = 7;
        public const short ColorIndexRed = 1;
        public const short ColorIndexYellow = 2;
        public const short ColorIndexBlue = 6;
        public const short ColorIndexMagenta = 5;
        public const short ColorIndexCyan = 4;
        public const string StrLayer = "Layer";
        public const string StrLinetype = "Linetype";
        public const string StrLineWeight = "LineWeight";
        public const string StrColorIndex = "ColorIndex";
        public const string StrByLayer = "BYLAYER";
        public const string StrTextString = "TextString";
        public const string StrTag = "Tag";
        public const string StrPrompt = "Prompt";
        public const string StrPosition = "Position";
        public const string StrTextStyleId = "TextStyleId";
        public const string StrHeight = "Height";
        public const string StrTypeName = "TypeName";
        public const string StrLine = "Line";
        public const string StrStartPoint = "StartPoint";
        public const string StrEndPoint = "EndPoint";


        public const string StrLinetypeScale = "LinetypeScale";

        public const string LineTypeContinuous = "Continuous";

        public const string StrCircle = "Circle";
        public const string StrCenter = "Center";
        public const string StrRadius = "Radius";
        public const string StrDbText = "DBText";
        public const string StrAttributeDefinition = "AttributeDefinition";
        public const string StrNormal = "Normal";

        // 单例模式的锁对象
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

        public static IDocumentService _ => instance.ActiveServiceDoc;

        /// <summary>
        ///     获取当前活动文档的EditorService
        /// </summary>
        public static IEditorService ServiceEd => instance.ActiveServiceDoc.ServiceEd;

        /// <summary>
        ///     获取当前活动文档的DocumentService
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

        public static string GetDefaultName() => DateTime.UtcNow.ToShortTimeString();
    }
}
