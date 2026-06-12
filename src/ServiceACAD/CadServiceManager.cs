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

        #region Obsolete Constants - Use nested classes instead

        [Obsolete("Use Layers.Default instead")]
        public const string Layer0 = Layers.Default;

        [Obsolete("Use Colors.ByBlock instead")]
        public const short ColorIndexByBlock = Colors.ByBlock;

        [Obsolete("Use Linetypes.ByBlock instead")]
        public const string StrByBlock = Linetypes.ByBlock;

        [Obsolete("Use Colors.ByLayer instead")]
        public const short ColorIndexByLayer = Colors.ByLayer;

        [Obsolete("Use Colors.Green instead")]
        public const short ColorIndexGreen = Colors.Green;

        [Obsolete("Use Colors.White instead")]
        public const short ColorIndexWhite = Colors.White;

        [Obsolete("Use Colors.Red instead")]
        public const short ColorIndexRed = Colors.Red;

        [Obsolete("Use Colors.Yellow instead")]
        public const short ColorIndexYellow = Colors.Yellow;

        [Obsolete("Use Colors.Blue instead")]
        public const short ColorIndexBlue = Colors.Blue;

        [Obsolete("Use Colors.Magenta instead")]
        public const short ColorIndexMagenta = Colors.Magenta;

        [Obsolete("Use Colors.Cyan instead")]
        public const short ColorIndexCyan = Colors.Cyan;

        [Obsolete("Use PropNames.Layer instead")]
        public const string StrLayer = PropNames.Layer;

        [Obsolete("Use PropNames.Linetype instead")]
        public const string StrLinetype = PropNames.Linetype;

        [Obsolete("Use PropNames.LineWeight instead")]
        public const string StrLineWeight = PropNames.LineWeight;

        [Obsolete("Use PropNames.ColorIndex instead")]
        public const string StrColorIndex = PropNames.ColorIndex;

        [Obsolete("Use Layers.ByLayer instead")]
        public const string StrByLayer = Layers.ByLayer;

        [Obsolete("Use PropNames.TextString instead")]
        public const string StrTextString = PropNames.TextString;

        [Obsolete("Use PropNames.Tag instead")]
        public const string StrTag = PropNames.Tag;

        [Obsolete("Use PropNames.Prompt instead")]
        public const string StrPrompt = PropNames.Prompt;

        [Obsolete("Use PropNames.Position instead")]
        public const string StrPosition = PropNames.Position;

        [Obsolete("Use PropNames.TextStyleId instead")]
        public const string StrTextStyleId = PropNames.TextStyleId;

        [Obsolete("Use PropNames.Height instead")]
        public const string StrHeight = PropNames.Height;

        [Obsolete("Use PropNames.TypeName instead")]
        public const string StrTypeName = PropNames.TypeName;

        [Obsolete("Use EntityTypes.Line instead")]
        public const string StrLine = EntityTypes.Line;

        [Obsolete("Use PropNames.StartPoint instead")]
        public const string StrStartPoint = PropNames.StartPoint;

        [Obsolete("Use PropNames.EndPoint instead")]
        public const string StrEndPoint = PropNames.EndPoint;

        [Obsolete("Use PropNames.LinetypeScale instead")]
        public const string StrLinetypeScale = PropNames.LinetypeScale;

        [Obsolete("Use Linetypes.Continuous instead")]
        public const string LineTypeContinuous = Linetypes.Continuous;

        [Obsolete("Use EntityTypes.Circle instead")]
        public const string StrCircle = EntityTypes.Circle;

        [Obsolete("Use PropNames.Center instead")]
        public const string StrCenter = PropNames.Center;

        [Obsolete("Use PropNames.Radius instead")]
        public const string StrRadius = PropNames.Radius;

        [Obsolete("Use EntityTypes.DbText instead")]
        public const string StrDbText = EntityTypes.DbText;

        [Obsolete("Use EntityTypes.AttributeDefinition instead")]
        public const string StrAttributeDefinition = EntityTypes.AttributeDefinition;

        [Obsolete("Use PropNames.Normal instead")]
        public const string StrNormal = PropNames.Normal;

        #endregion

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
