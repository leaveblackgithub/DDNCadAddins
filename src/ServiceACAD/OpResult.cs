// 此文件已迁移到 DDNCadAddins.Core/Models/OpResult.cs
// 保留此文件仅为向后兼容，所有新代码应使用 DDNCadAddins.Core.Models.OpResult

using CoreOpResult = DDNCadAddins.Core.Models.OpResult;
using CoreOpResultGeneric = DDNCadAddins.Core.Models.OpResult<object>;

namespace ServiceACAD
{
    /// <summary>
    ///     操作结果（无数据） - 已迁移到 Core 层
    ///     此类型别名用于向后兼容，所有实现已在 DDNCadAddins.Core.Models.OpResult
    /// </summary>
    public class OpResult : CoreOpResult
    {
        public OpResult() : base()
        {
        }

        public OpResult(bool isSuccess, string message) : base(isSuccess, message)
        {
        }

        public new static OpResult Success(string message = "")
        {
            var result = CoreOpResult.Success(message);
            return new OpResult(result.IsSuccess, result.Message);
        }

        public new static OpResult Fail(string message)
        {
            var result = CoreOpResult.Fail(message);
            return new OpResult(result.IsSuccess, result.Message);
        }
    }

    /// <summary>
    ///     操作结果（带数据） - 已迁移到 Core 层
    ///     此类型别名用于向后兼容，所有实现已在 DDNCadAddins.Core.Models.OpResult&lt;T&gt;
    /// </summary>
    public class OpResult<T> : DDNCadAddins.Core.Models.OpResult<T>
    {
        public OpResult() : base()
        {
        }

        public OpResult(bool isSuccess, string message, T data) : base(isSuccess, message, data)
        {
        }

        public new static OpResult<T> Success(T data, string message = "")
        {
            var result = DDNCadAddins.Core.Models.OpResult<T>.Success(data, message);
            return new OpResult<T>(result.IsSuccess, result.Message, result.Data);
        }

        public new static OpResult<T> Fail(string message)
        {
            var result = DDNCadAddins.Core.Models.OpResult<T>.Fail(message);
            return new OpResult<T>(result.IsSuccess, result.Message, result.Data);
        }
    }
}
