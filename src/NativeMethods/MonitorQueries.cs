using System;
using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>
    /// Cursor and monitor queries (user32, shcore)
    /// </summary>
    public static class MonitorQueries
    {
        /// <summary>MonitorFrom* flag: the monitor nearest to the point or rectangle if it is on none</summary>
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        /// <summary>GetDpiForMonitor type: the effective DPI (the user's scaling setting)</summary>
        public const int MDT_EFFECTIVE_DPI = 0;

        /// <summary>The cursor position in screen coordinates (physical pixels for a per-monitor aware caller)</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT point, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromRect(ref RECT rect, uint dwFlags);

        /// <returns>An HRESULT; 0 (S_OK) on success</returns>
        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}
