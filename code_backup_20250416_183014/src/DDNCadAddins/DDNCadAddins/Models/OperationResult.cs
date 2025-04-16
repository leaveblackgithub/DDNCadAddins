using System;

namespace DDNCadAddins.Models
{
    /// <summary>
    /// 操作结果接口 - 基础接口
    /// </summary>
    public interface IOperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        bool Success { get; }
        
        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        string ErrorMessage { get; }
        
        /// <summary>
        /// 成功消息（如果操作成功）
        /// </summary>
        string Message { get; }
        
        /// <summary>
        /// 操作执行时间
        /// </summary>
        TimeSpan ExecutionTime { get; }
        
        /// <summary>
        /// 结果类型
        /// </summary>
        OperationResultType Type { get; }
    }
    
    /// <summary>
    /// 泛型操作结果接口 - 带返回数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public interface IOperationResult<T> : IOperationResult
    {
        /// <summary>
        /// 返回的数据
        /// </summary>
        T Data { get; }
    }
    
    /// <summary>
    /// 结果类型枚举
    /// </summary>
    public enum OperationResultType
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success,
        
        /// <summary>
        /// 错误
        /// </summary>
        Error,
        
        /// <summary>
        /// 警告
        /// </summary>
        Warning,
        
        /// <summary>
        /// 信息
        /// </summary>
        Info
    }
    
    /// <summary>
    /// 操作结果基类 - 用于统一返回结果
    /// </summary>
    public class OperationResult : IOperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// 成功消息（如果操作成功）
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 操作执行时间
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
        
        /// <summary>
        /// 结果类型
        /// </summary>
        public OperationResultType Type { get; set; } = OperationResultType.Success;
        
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OperationResult()
        {
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="message">消息</param>
        /// <param name="executionTime">执行时间</param>
        /// <param name="type">结果类型</param>
        public OperationResult(bool success, string message, TimeSpan executionTime, OperationResultType type = OperationResultType.Success)
        {
            Success = success;
            if (success) {Message = message;}
            else {ErrorMessage = message;}
            ExecutionTime = executionTime;
            Type = type;
        }
        
        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="executionTime">执行时间</param>
        /// <returns>操作结果</returns>
        public static OperationResult SuccessResult(TimeSpan executionTime)
        {
            return new OperationResult 
            { 
                Success = true,
                ExecutionTime = executionTime,
                Type = OperationResultType.Success
            };
        }
        
        /// <summary>
        /// 创建带消息的成功结果
        /// </summary>
        /// <param name="executionTime">执行时间</param>
        /// <param name="message">成功消息</param>
        /// <returns>操作结果</returns>
        public static OperationResult SuccessResult(TimeSpan executionTime, string message)
        {
            return new OperationResult 
            { 
                Success = true,
                Message = message,
                ExecutionTime = executionTime,
                Type = OperationResultType.Success
            };
        }
        
        /// <summary>
        /// 创建错误结果
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        /// <param name="executionTime">执行时间</param>
        /// <returns>操作结果</returns>
        public static OperationResult ErrorResult(string errorMessage, TimeSpan executionTime)
        {
            return new OperationResult 
            { 
                Success = false,
                ErrorMessage = errorMessage,
                ExecutionTime = executionTime,
                Type = OperationResultType.Error
            };
        }
        
        /// <summary>
        /// 创建警告结果
        /// </summary>
        /// <param name="warningMessage">警告信息</param>
        /// <param name="executionTime">执行时间</param>
        /// <returns>操作结果</returns>
        public static OperationResult WarningResult(string warningMessage, TimeSpan executionTime)
        {
            return new OperationResult 
            { 
                Success = false,
                Message = warningMessage,
                ExecutionTime = executionTime,
                Type = OperationResultType.Warning
            };
        }
    }
    
    /// <summary>
    /// 泛型操作结果类 - 用于返回带数据的结果
    /// </summary>
    /// <typeparam name="T">返回数据类型</typeparam>
    public class OperationResult<T> : OperationResult, IOperationResult<T>
    {
        /// <summary>
        /// 返回的数据
        /// </summary>
        public T Data { get; set; }
        
        /// <summary>
        /// 创建成功结果并包含数据
        /// </summary>
        /// <param name="data">返回的数据</param>
        /// <param name="executionTime">执行时间</param>
        /// <returns>操作结果</returns>
        public static OperationResult<T> SuccessResult(T data, TimeSpan executionTime)
        {
            return new OperationResult<T> 
            { 
                Success = true,
                Data = data,
                ExecutionTime = executionTime,
                Type = OperationResultType.Success
            };
        }
        
        /// <summary>
        /// 创建成功结果并包含数据和消息
        /// </summary>
        /// <param name="data">返回的数据</param>
        /// <param name="executionTime">执行时间</param>
        /// <param name="message">成功消息</param>
        /// <returns>操作结果</returns>
        public static OperationResult<T> SuccessResult(T data, TimeSpan executionTime, string message)
        {
            return new OperationResult<T> 
            { 
                Success = true,
                Data = data,
                Message = message,
                ExecutionTime = executionTime,
                Type = OperationResultType.Success
            };
        }
        
        /// <summary>
        /// 创建错误结果
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        /// <param name="executionTime">执行时间</param>
        /// <returns>操作结果</returns>
        public static new OperationResult<T> ErrorResult(string errorMessage, TimeSpan executionTime)
        {
            return new OperationResult<T> 
            { 
                Success = false,
                ErrorMessage = errorMessage,
                ExecutionTime = executionTime,
                Type = OperationResultType.Error
            };
        }
        
        /// <summary>
        /// 创建警告结果
        /// </summary>
        /// <param name="warningMessage">警告信息</param>
        /// <param name="executionTime">执行时间</param>
        /// <returns>操作结果</returns>
        public static new OperationResult<T> WarningResult(string warningMessage, TimeSpan executionTime)
        {
            return new OperationResult<T> 
            { 
                Success = false,
                Message = warningMessage,
                ExecutionTime = executionTime,
                Type = OperationResultType.Warning
            };
        }
    }
} 