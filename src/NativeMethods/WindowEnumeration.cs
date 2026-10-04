using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>
    /// user32 functions for enumerating and inspecting top-level windows
    /// </summary>
    public static class WindowEnumeration
    {
        /// <summary>GetWindowLong(Ptr) index of the extended window styles</summary>
        public const int GWL_EXSTYLE = -20;

        public const uint WS_EX_TOPMOST = 0x00000008;
        public const uint WS_EX_TRANSPARENT = 0x00000020;
        public const uint WS_EX_TOOLWINDOW = 0x00000080;

        /// <summary>Callback for <see cref="EnumWindows"/>; return true to continue enumerating</summary>
        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>Enumerates the top-level windows, in z-order (topmost first)</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>True if the window is minimized</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        /// <returns>The class name length, else 0 on failure</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        /// <summary>GetAncestor flag: the root window (top-level window) of the window</summary>
        public const uint GA_ROOT = 2;

        /// <summary>The visible, enabled window at the screen point (physical pixels for a per-monitor aware caller)</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        /// <summary>The window's extended styles (WS_EX_*)</summary>
        public static uint GetExStyle(IntPtr hWnd)
        {
            // GetWindowLongPtr is only exported by 64-bit user32
            return IntPtr.Size == 8
                ? unchecked((uint)GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64())
                : unchecked((uint)GetWindowLong32(hWnd, GWL_EXSTYLE));
        }

        /// <summary>The window's class name, else an empty string</summary>
        public static string GetClassName(IntPtr hWnd)
        {
            var name = new StringBuilder(256);
            return GetClassName(hWnd, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
    }
}
