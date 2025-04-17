namespace DDNCadAddins.Commands
{
    /// <summary>
    ///     测试框架接口 - 定义测试固件的生命周期方法.
    /// </summary>
    public interface ITestFixture
    {
        /// <summary>
        ///     在所有测试方法执行之前调用一次.
        /// </summary>
        void SetUpFixture();

        /// <summary>
        ///     在每个测试方法执行之前调用.
        /// </summary>
        void SetUp();

        /// <summary>
        ///     在每个测试方法执行之后调用.
        /// </summary>
        void TearDown();

        /// <summary>
        ///     在所有测试方法执行之后调用一次.
        /// </summary>
        void TearDownFixture();
    }
}
