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

        /// <summary>
        ///     在内存侧数据库（new Database(true, true)）中执行操作。
        ///     不与活动文档交互，不修改当前图纸，适合自动化测试.
        /// </summary>
        /// <param name="action">在侧数据库事务中执行的逻辑.</param>
        void ExecuteInSideDatabase(Action<ITransactionService> action);
    }
}
