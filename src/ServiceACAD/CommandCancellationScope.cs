using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     在长耗时命令中检测 ESC 并支持事务回滚
    /// </summary>
    public sealed class CommandCancellationScope : ICommandCancellation, IDisposable
    {
        /// <summary>
        ///     用户取消时的标准消息
        /// </summary>
        public const string UserCancelledMessage = "用户已取消操作。";

        private bool _isCancelled;

        /// <inheritdoc />
        public bool IsCancellationRequested
        {
            get
            {
                if (_isCancelled)
                {
                    return true;
                }

                try
                {
                    // AutoCAD 模态命令期间 WinForms IMessageFilter 收不到 ESC，
                    // 必须通过 DoEvents + UserBreak 主动泵送消息。
                    if (HostApplicationServices.Current.UserBreakWithMessagePump())
                    {
                        _isCancelled = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger._.Error("检测用户中断时发生错误", ex);
                }

                return _isCancelled;
            }
        }

        /// <summary>
        ///     释放作用域（保留 IDisposable 供 using 语义）
        /// </summary>
        public void Dispose()
        {
        }
    }
}
