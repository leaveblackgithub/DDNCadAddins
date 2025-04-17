using System;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     事务服务接口 - 负责处理AutoCAD事务相关的所有操作.
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        ///     执行事务操作.
        /// </summary>
        /// <typeparam name="T">返回数据类型.</typeparam>
        /// <param name="database">数据库.</param>
        /// <param name="action">要在事务中执行的操作.</param>
        /// <param name="errorMessagePrefix">错误消息前缀.</param>
        /// <returns>操作结果.</returns>
        OperationResult<T> ExecuteInTransaction<T>(Database database, Func<Transaction, T> action,
            string errorMessagePrefix);

        /// <summary>
        ///     执行事务操作（无返回值）.
        /// </summary>
        /// <param name="database">数据库.</param>
        /// <param name="action">要在事务中执行的操作.</param>
        /// <param name="errorMessagePrefix">错误消息前缀.</param>
        /// <returns>操作结果.</returns>
        OperationResult ExecuteInTransaction(Database database, Action<Transaction> action, string errorMessagePrefix);
    }
}
