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
        /// <summary>
        ///     颜色索引常量
        /// </summary>
        public static class Colors
        {
            public const short ByBlock = 0;
            public const short ByLayer = 256;
            public const short Red = 1;
            public const short Yellow = 2;
            public const short Green = 3;
            public const short Cyan = 4;
            public const short Blue = 5;
            public const short Magenta = 6;
            public const short White = 7;
        }

        /// <summary>
        ///     图层相关常量
        /// </summary>
        public static class Layers
        {
            public const string Default = "0";
            public const string ByLayer = "BYLAYER";
        }

        /// <summary>
        ///     线型相关常量
        /// </summary>
        public static class Linetypes
        {
            public const string ByBlock = "BYBLOCK";
            public const string ByLayer = "BYLAYER";
            public const string Continuous = "Continuous";
        }

        /// <summary>
        ///     实体属性名称常量（用于反射或字典键）
        /// </summary>
        public static class PropNames
        {
            public const string Layer = "Layer";
            public const string ColorIndex = "ColorIndex";
            public const string Linetype = "Linetype";
            public const string LineWeight = "LineWeight";
            public const string LinetypeScale = "LinetypeScale";
            public const string Normal = "Normal";
            public const string Position = "Position";
            public const string Height = "Height";
            public const string TextString = "TextString";
            public const string Tag = "Tag";
            public const string Prompt = "Prompt";
            public const string TextStyleId = "TextStyleId";
            public const string TypeName = "TypeName";
            public const string StartPoint = "StartPoint";
            public const string EndPoint = "EndPoint";
            public const string Center = "Center";
            public const string Radius = "Radius";
        }

        /// <summary>
        ///     实体类型名称常量
        /// </summary>
        public static class EntityTypes
        {
            public const string Line = "Line";
            public const string Circle = "Circle";
            public const string DbText = "DBText";
            public const string AttributeDefinition = "AttributeDefinition";
        }

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

                if (currentDocument == null)
                {
                    return null;
                }

                if (_currentDocumentService == null || _lastActiveDocument != currentDocument)
                {
                    _lastActiveDocument = currentDocument;
                    _currentDocumentService = new DocumentService(currentDocument);
                }

                return _currentDocumentService;
            }
        }

        /// <summary>
        ///     文档激活事件处理程序
        /// </summary>
        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
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
