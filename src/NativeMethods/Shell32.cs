using System;
using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    public static class Shell32
    {
        /// <summary>Gets the taskbar's auto-hide and always-on-top state; the result is a set of ABS_ flags</summary>
        public const uint ABM_GETSTATE = 0x00000004;

        /// <summary>Gets the taskbar's bounding rectangle, in rc. For an auto-hide taskbar this is where it shows.</summary>
        public const uint ABM_GETTASKBARPOS = 0x00000005;

        /// <summary>ABM_GETSTATE flag: the taskbar is in auto-hide mode</summary>
        public const uint ABS_AUTOHIDE = 0x0000001;

        /// <summary>
        /// Sends an appbar message to the system. The return value depends on the message.
        /// </summary>
        [DllImport("shell32.dll")]
        public static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
    }
}
