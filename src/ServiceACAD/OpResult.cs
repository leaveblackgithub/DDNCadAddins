namespace ServiceACAD
{
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
        public static OpResult<T> Success(T data) => new OpResult<T>(true, string.Empty, data);
        public static OpResult<T> Fail(string message) => new OpResult<T>(false, message, default(T));
    }

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
        public static OpResult Success() => new OpResult(true, string.Empty);
        public static OpResult Fail(string message) => new OpResult(false, message);
    }
}
