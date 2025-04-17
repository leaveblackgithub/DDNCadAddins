using System;

namespace DDNCadAddins.Infrastructure
{
    /// <summary>
    ///     用户消息服务接口 - 负责向用户显示消息.
    /// </summary>
    public interface IUserMessageService
    {
        /// <summary>
        ///     显示普通信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        void ShowMessage(string message);

        /// <summary>
        ///     显示成功信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        void ShowSuccess(string message);

        /// <summary>
        ///     显示警告信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        void ShowWarning(string message);

        /// <summary>
        ///     显示错误信息.
        /// </summary>
        /// <param name="message">消息内容.</param>
        /// <param name="ex">可选的异常对象.</param>
        void ShowError(string message, Exception ex = null);

        /// <summary>
        ///     显示提示对话框.
        /// </summary>
        /// <param name="message">消息内容.</param>
        void ShowAlert(string message);
    }
}
