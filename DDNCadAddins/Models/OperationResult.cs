using System;

namespace DDNCadAddins.Models
{
    /// <summary>
    /// 操作结果基类 - 用于统一返回结果
    /// </summary>
    public class OperationResult
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
        /// 操作执行时间
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
        
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
                ExecutionTime = executionTime
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
                ExecutionTime = executionTime
            };
        }
    }
    
    /// <summary>
    /// 泛型操作结果类 - 用于返回带数据的结果
    /// </summary>
    /// <typeparam name="T">返回数据类型</typeparam>
    public class OperationResult<T> : OperationResult
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
                ExecutionTime = executionTime
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
                ExecutionTime = executionTime
            };
        }
    }
} 