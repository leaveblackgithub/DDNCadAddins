namespace DDNCadAddins.Models
{
    /// <summary>
    ///     简单操作结果类 - 适用于不需要执行时间的场景.
    /// </summary>
    public class OpResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpResult"/> class.
        ///     构造函数.
        /// </summary>
        /// <param name="success">操作是否成功.</param>
        /// <param name="message">操作消息.</param>
        public OpResult(bool success, string message)
        {
            this.Success = success;
            this.Message = message;
        }

        /// <summary>
        ///     Gets a value indicating whether 操作是否成功.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        ///     Gets 消息（成功或错误消息）.
        /// </summary>
        public string Message { get; }

        /// <summary>
        ///     Gets 错误消息（如果操作失败）.
        /// </summary>
        public string ErrorMessage => !this.Success ? this.Message : string.Empty;
    }

    /// <summary>
    ///     泛型简单操作结果类 - 用于返回带数据的结果.
    /// </summary>
    /// <typeparam name="T">返回数据类型.</typeparam>
    public class OpResult<T> : OpResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpResult{T}"/> class.
        ///     构造函数.
        /// </summary>
        /// <param name="success">操作是否成功.</param>
        /// <param name="message">操作消息.</param>
        /// <param name="data">返回数据.</param>
        public OpResult(bool success, string message, T data)
            : base(success, message)
        {
            this.Data = data;
        }

        /// <summary>
        ///     Gets 返回的数据.
        /// </summary>
        public T Data { get; private set; }
    }
}
