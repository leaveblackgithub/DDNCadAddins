using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public static class HostApplicationServicesExtensions
    {
        public static bool UserBreakWithMessagePump(this HostApplicationServices hostapp)
        {
            System.Windows.Forms.Application.DoEvents();
            return hostapp.UserBreak();
        }
    }
}
