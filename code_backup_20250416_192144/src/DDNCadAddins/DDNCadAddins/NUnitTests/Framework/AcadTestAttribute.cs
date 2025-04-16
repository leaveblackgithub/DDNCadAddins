using System;
using NUnit.Framework;

namespace DDNCadAddins.NUnitTests.Framework
{
    /// <summary>
    /// AutoCAD测试特性 - 标记这是一个AutoCAD环境中的测试方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AcadTestAttribute : TestAttribute
    {
        /// <summary>
        /// 测试类别
        /// </summary>
        public string Category { get; set; }
        
        /// <summary>
        /// 测试描述
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// 测试是否需要事务
        /// </summary>
        public bool RequiresTransaction { get; set; } = true;
        
        /// <summary>
        /// 测试是否需要先创建测试环境
        /// </summary>
        public bool RequiresTestSetup { get; set; } = true;
        
        /// <summary>
        /// 测试是否需要清理测试环境
        /// </summary>
        public bool RequiresTestCleanup { get; set; } = true;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="description">测试描述</param>
        public AcadTestAttribute(string description = null)
        {
            Description = description;
        }
    }
} 