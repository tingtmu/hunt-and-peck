using System;
using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    public static class Dwmapi
    {
        /// <summary>
        /// DwmGetWindowAttribute: non-zero (DWORD) if the window is cloaked, i.e. "shown" but not drawn, as are
        /// windows on another virtual desktop or hidden by a tiling window manager (e.g. GlazeWM workspaces)
        /// </summary>
        public const int DWMWA_CLOAKED = 14;

        /// <returns>An HRESULT; 0 (S_OK) on success</returns>
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
    }
}
