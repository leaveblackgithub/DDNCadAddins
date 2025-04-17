using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;

// 使用别名解决命名冲突
using Application = System.Windows.Forms.Application;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     扩展方法类.
    /// </summary>
    public static class AcadExtensions
    {
        /// <summary>
        ///     扩展方法：处理Windows消息队列并检查用户是否按下了ESC键.
        /// </summary>
        /// <param name="hostapp">HostApplicationServices实例.</param>
        /// <returns>用户是否按下了ESC键.</returns>
        public static bool UserBreakWithMessagePump(this HostApplicationServices hostapp)
        {
            Application.DoEvents();
            return hostapp.UserBreak();
        }
    }

    /// <summary>
    ///     AutoCAD API服务组合实现类 - 实现多个接口并委托给专用服务.
    /// </summary>
    public class AcadService : IAcadService, IDocumentService, ITransactionService, IBlockReferenceService
    {
        // 先定义所有private成员
        private readonly IBlockReferenceService blockReferenceService;
        private readonly IDocumentService documentService;
        private readonly ILogger logger;
        private readonly ITransactionService transactionService;

        // 然后是所有public成员
        /// <summary>
        /// Initializes a new instance of the <see cref="AcadService"/> class.
        ///     构造函数 - 使用依赖注入.
        /// </summary>
        /// <param name="logger">日志记录接口.</param>
        public AcadService(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 创建专用服务（此处为临时方案，应通过依赖注入容器获取）
            this.documentService = new DocumentService(logger);
            this.transactionService = new TransactionService(logger);
            this.blockReferenceService = new BlockReferenceService(logger, this.transactionService);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AcadService"/> class.
        ///     构造函数 - 接受所有服务依赖.
        /// </summary>
        /// <param name="logger">日志记录接口.</param>
        /// <param name="documentService">文档服务.</param>
        /// <param name="transactionService">事务服务.</param>
        /// <param name="blockReferenceService">块参照服务.</param>
        public AcadService(
            ILogger logger,
            IDocumentService documentService,
            ITransactionService transactionService,
            IBlockReferenceService blockReferenceService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            this.transactionService =
                transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            this.blockReferenceService =
                blockReferenceService ?? throw new ArgumentNullException(nameof(blockReferenceService));
        }

        /// <summary>
        ///     获取当前活动文档对象.
        /// </summary>
        /// <returns>当前活动文档对象，如果没有打开的文档则返回null.</returns>
        public Document GetMdiActiveDocument()
        {
            return this.documentService.GetMdiActiveDocument();
        }

        /// <summary>
        ///     显示警告对话框.
        /// </summary>
        /// <param name="message">显示的消息.</param>
        public void ShowAlertDialog(string message)
        {
            this.documentService.ShowAlertDialog(message);
        }

        /// <summary>
        ///     获取当前活动文档.
        /// </summary>
        /// <returns>当前文档是否可用.</returns>
        public bool GetActiveDocument(out Database database, out Editor editor)
        {
            return this.documentService.GetActiveDocument(out database, out editor);
        }

        /// <summary>
        ///     写入消息到命令行.
        /// </summary>
        /// <param name="message">消息内容.</param>
        public void WriteMessage(string message)
        {
            this.documentService.WriteMessage(message);
        }

        public Editor GetEditor()
        {
            // 获取当前编辑器
            return this.GetMdiActiveDocument()?.Editor;
        }

        /// <summary>
        ///     执行事务操作.
        /// </summary>
        /// <typeparam name="T">返回数据类型.</typeparam>
        /// <param name="database">数据库.</param>
        /// <param name="action">要在事务中执行的操作.</param>
        /// <param name="errorMessagePrefix">错误消息前缀.</param>
        /// <returns>操作结果.</returns>
        public OperationResult<T> ExecuteInTransaction<T>(Database database, Func<Transaction, T> action,
            string errorMessagePrefix)
        {
            return this.transactionService.ExecuteInTransaction(database, action, errorMessagePrefix);
        }

        /// <summary>
        ///     执行事务操作（无返回值）.
        /// </summary>
        /// <param name="database">数据库.</param>
        /// <param name="action">要在事务中执行的操作.</param>
        /// <param name="errorMessagePrefix">错误消息前缀.</param>
        /// <returns>操作结果.</returns>
        public OperationResult ExecuteInTransaction(Database database, Action<Transaction> action,
            string errorMessagePrefix)
        {
            return this.transactionService.ExecuteInTransaction(database, action, errorMessagePrefix);
        }

        /// <summary>
        ///     获取块参照对象.
        /// </summary>
        /// <param name="tr">事务.</param>
        /// <param name="blockRefId">块参照ID.</param>
        /// <param name="openMode">打开模式.</param>
        /// <returns>块参照对象，如果获取失败则返回null.</returns>
        public BlockReference GetBlockReference(Transaction tr, ObjectId blockRefId,
            OpenMode openMode = OpenMode.ForRead)
        {
            return this.blockReferenceService.GetBlockReference(tr, blockRefId, openMode);
        }

        /// <summary>
        ///     获取块的几何边界.
        /// </summary>
        /// <param name="blockRef">块参照.</param>
        /// <returns>几何边界，如果获取失败则返回null.</returns>
        public Extents3d? GetBlockGeometricExtents(BlockReference blockRef)
        {
            return this.blockReferenceService.GetBlockGeometricExtents(blockRef);
        }

        /// <summary>
        ///     获取块的属性信息.
        /// </summary>
        /// <param name="tr">事务.</param>
        /// <param name="blockRef">块参照.</param>
        /// <returns>块名称和定义的元组，如果获取失败则返回null.</returns>
        public (string BlockName, BlockTableRecord BlockDef)? GetBlockInfo(Transaction tr, BlockReference blockRef)
        {
            return this.blockReferenceService.GetBlockInfo(tr, blockRef);
        }

        /// <summary>
        ///     创建测试块.
        /// </summary>
        /// <param name="tr">事务.</param>
        /// <param name="blockName">块名称.</param>
        /// <param name="insertionPoint">插入点.</param>
        /// <returns>创建的块参照ID.</returns>
        public ObjectId CreateTestBlock(Transaction tr, string blockName, Point3d insertionPoint)
        {
            return this.blockReferenceService.CreateTestBlock(tr, blockName, insertionPoint);
        }

        /// <summary>
        ///     查找所有块参照.
        /// </summary>
        /// <param name="editor">编辑器.</param>
        /// <returns>块参照ID数组.</returns>
        public ObjectId[] FindAllBlockReferences(Editor editor)
        {
            return this.blockReferenceService.FindAllBlockReferences(editor);
        }

        /// <summary>
        ///     检查块是否被XClip.
        /// </summary>
        /// <param name="tr">事务.</param>
        /// <param name="blockRef">块参照.</param>
        /// <param name="detectionMethod">检测方法.</param>
        /// <returns>是否被XClip.</returns>
        public bool IsBlockXClipped(Transaction tr, BlockReference blockRef, out string detectionMethod)
        {
            return this.blockReferenceService.IsBlockXClipped(tr, blockRef, out detectionMethod);
        }

        /// <summary>
        ///     执行XClip命令.
        /// </summary>
        /// <param name="blockRefId">块参照ID.</param>
        /// <param name="minPoint">裁剪边界最小点.</param>
        /// <param name="maxPoint">裁剪边界最大点.</param>
        /// <returns>操作结果.</returns>
        public OperationResult ExecuteXClipCommand(ObjectId blockRefId, Point3d minPoint, Point3d maxPoint)
        {
            return this.blockReferenceService.ExecuteXClipCommand(blockRefId, minPoint, maxPoint);
        }
    }
}
