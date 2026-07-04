// 类型转发 — 所有 OpResult 引用已迁移到 DDNCadAddins.Core.Models.OpResult
// 此文件仅保留向后兼容的类型别名，新代码应直接使用 Core 层类型

namespace ServiceACAD
{
    /// <summary>向后兼容类型别名，委托到 <see cref="DDNCadAddins.Core.Models.OpResult"/>.</summary>
    public class OpResult : DDNCadAddins.Core.Models.OpResult
    {
        public OpResult() : base() { }
        public OpResult(bool isSuccess, string message) : base(isSuccess, message) { }
        public new static OpResult Success(string message = "")
        {
            var r = DDNCadAddins.Core.Models.OpResult.Success(message);
            return new OpResult(r.IsSuccess, r.Message);
        }
        public new static OpResult Fail(string message)
        {
            var r = DDNCadAddins.Core.Models.OpResult.Fail(message);
            return new OpResult(r.IsSuccess, r.Message);
        }
    }

    /// <summary>向后兼容类型别名，委托到 <see cref="DDNCadAddins.Core.Models.OpResult{T}"/>.</summary>
    public class OpResult<T> : DDNCadAddins.Core.Models.OpResult<T>
    {
        public OpResult() : base() { }
        public OpResult(bool isSuccess, string message, T data) : base(isSuccess, message, data) { }
        public new static OpResult<T> Success(T data, string message = "")
        {
            var r = DDNCadAddins.Core.Models.OpResult<T>.Success(data, message);
            return new OpResult<T>(r.IsSuccess, r.Message, r.Data);
        }
        public new static OpResult<T> Fail(string message)
        {
            var r = DDNCadAddins.Core.Models.OpResult<T>.Fail(message);
            return new OpResult<T>(r.IsSuccess, r.Message, r.Data);
        }
    }
}
