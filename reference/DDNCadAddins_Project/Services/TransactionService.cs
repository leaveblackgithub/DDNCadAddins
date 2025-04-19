using System;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
using SystemException = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    ///     事务服务实现类 - 负责处理AutoCAD事务相关的所有操作.
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionService"/> class.
        ///     构造函数.
        /// </summary>
        /// <param name="logger">日志记录接口.</param>
        public TransactionService(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            if (database == null)
            {
                return OperationResult<T>.ErrorResult("数据库为空", TimeSpan.Zero);
            }

            DateTime startTime = DateTime.Now;

            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        T result = action(tr);
                        tr.Commit();

                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult<T>.SuccessResult(result, duration);
                    }
                    catch (SystemException ex)
                    {
                        tr.Abort();
                        throw new SystemException($"{errorMessagePrefix}: {ex.Message}", ex);
                    }
                }
            }
            catch (SystemException ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"{errorMessagePrefix}: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }

                this.logger.LogError(errorMessage, ex);
                return OperationResult<T>.ErrorResult(errorMessage, duration);
            }
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
            if (database == null)
            {
                return OperationResult.ErrorResult("数据库为空", TimeSpan.Zero);
            }

            DateTime startTime = DateTime.Now;

            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        action(tr);
                        tr.Commit();

                        TimeSpan duration = DateTime.Now - startTime;
                        return OperationResult.SuccessResult(duration);
                    }
                    catch (SystemException ex)
                    {
                        tr.Abort();
                        throw new SystemException($"{errorMessagePrefix}: {ex.Message}", ex);
                    }
                }
            }
            catch (SystemException ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                string errorMessage = $"{errorMessagePrefix}: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部异常: {ex.InnerException.Message}";
                }

                this.logger.LogError(errorMessage, ex);
                return OperationResult.ErrorResult(errorMessage, duration);
            }
        }
    }
}
