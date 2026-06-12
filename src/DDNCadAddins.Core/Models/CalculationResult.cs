namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     计算结果 - 纯 POCO 模型，无 AutoCAD 依赖
    /// </summary>
    public class CalculationResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }

        public static CalculationResult Success(double value, string message = "")
        {
            return new CalculationResult
            {
                IsSuccess = true,
                Value = value,
                Message = message
            };
        }

        public static CalculationResult Fail(string message)
        {
            return new CalculationResult
            {
                IsSuccess = false,
                Message = message,
                Value = 0
            };
        }
    }
}
