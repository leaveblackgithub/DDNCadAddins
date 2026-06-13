using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public interface IDocumentService
    {
        Database CadDb { get; }
        string DrawingFullPath { get; }
        IEditorService ServiceEd { get; }

        void ExecuteInTransactions(string drawingTitle, params Action<ITransactionService>[] testActions);

        /// <summary>
        ///     在单个事务中执行命令逻辑；失败或取消时 Abort，成功时 Commit
        /// </summary>
        /// <param name="action">命令主体逻辑</param>
        /// <returns>操作结果</returns>
        OpResult ExecuteInCommandTransaction(Func<ITransactionService, OpResult> action);

        OpResult<ObjectId[]> Isolate(ObjectId objectId, params ObjectId[] additionalObjectIds);
    }
}
