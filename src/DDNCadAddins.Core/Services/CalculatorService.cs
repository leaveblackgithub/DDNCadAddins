using System;
using DDNCadAddins.Core.Interfaces;
using DDNCadAddins.Core.Models;

namespace DDNCadAddins.Core.Services
{
    /// <summary>
    ///     计算器服务实现 - 纯业务逻辑，无 CAD 依赖
    /// </summary>
    public class CalculatorService : ICalculatorService
    {
        public CalculationResult Add(double a, double b)
        {
            try
            {
                if (double.IsNaN(a) || double.IsNaN(b))
                {
                    return CalculationResult.Fail("输入包含无效数值（NaN）");
                }

                if (double.IsInfinity(a) || double.IsInfinity(b))
                {
                    return CalculationResult.Fail("输入包含无穷大值");
                }

                var result = a + b;
                
                if (double.IsInfinity(result))
                {
                    return CalculationResult.Fail("计算结果溢出");
                }

                return CalculationResult.Success(result, $"计算成功: {a} + {b} = {result}");
            }
            catch (Exception ex)
            {
                return CalculationResult.Fail($"计算异常: {ex.Message}");
            }
        }

        public CalculationResult Subtract(double a, double b)
        {
            try
            {
                if (double.IsNaN(a) || double.IsNaN(b))
                {
                    return CalculationResult.Fail("输入包含无效数值（NaN）");
                }

                var result = a - b;
                return CalculationResult.Success(result, $"计算成功: {a} - {b} = {result}");
            }
            catch (Exception ex)
            {
                return CalculationResult.Fail($"计算异常: {ex.Message}");
            }
        }
    }
}
