using System;
using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>
    /// Shell appbar message data for <see cref="Shell32.SHAppBarMessage"/>
    /// </summary>
    /// <remarks>
    /// Sequential layout with natural alignment matches the native struct: 48 bytes in a 64-bit process
    /// (4 bytes of padding after cbSize, pointer sized hWnd and lParam), 36 bytes in a 32-bit one.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;

        /// <summary>
        /// Creates the struct with cbSize set, as every appbar message requires
        /// </summary>
        public static APPBARDATA Create()
        {
            return new APPBARDATA { cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)) };
        }
    }
}
