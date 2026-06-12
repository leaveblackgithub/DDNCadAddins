using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Interfaces
{
    /// <summary>
    ///     计算器服务接口 - HelloWorld 示例
    /// </summary>
    public interface ICalculatorService
    {
        /// <summary>
        ///     加法运算
        /// </summary>
        /// <param name="a">第一个操作数</param>
        /// <param name="b">第二个操作数</param>
        /// <returns>计算结果</returns>
        OpResult<double> Add(double a, double b);

        /// <summary>
        ///     减法运算
        /// </summary>
        OpResult<double> Subtract(double a, double b);
    }
}
