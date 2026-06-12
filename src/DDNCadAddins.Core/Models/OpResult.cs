namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     带数据的操作结果 - Core 层统一返回值类型
    /// </summary>
    public class OpResult<T>
    {
        public OpResult()
        {
        }

        public OpResult(bool isSuccess, string message, T data)
        {
            IsSuccess = isSuccess;
            Message = message;
            Data = data;
        }

        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public static OpResult<T> Success(T data, string message = "")
        {
            return new OpResult<T>(true, message, data);
        }

        public static OpResult<T> Fail(string message)
        {
            return new OpResult<T>(false, message, default(T));
        }
    }

    /// <summary>
    ///     无数据的操作结果
    /// </summary>
    public class OpResult
    {
        public OpResult()
        {
        }

        public OpResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public bool IsSuccess { get; set; }
        public string Message { get; set; }

        public static OpResult Success(string message = "")
        {
            return new OpResult(true, message);
        }

        public static OpResult Fail(string message)
        {
            return new OpResult(false, message);
        }
    }
}
