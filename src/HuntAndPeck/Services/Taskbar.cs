using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Locates the Windows taskbar
    /// </summary>
    /// <remarks>
    /// On Windows 11 the primary taskbar is still a top level "Shell_TrayWnd" window; its content is a XAML island
    /// hosted in a "Windows.UI.Composition.DesktopWindowContentBridge" child, which UI Automation exposes as
    /// descendants of the Shell_TrayWnd element.
    /// </remarks>
    internal static class Taskbar
    {
        public const string PrimaryTaskbarClassName = "Shell_TrayWnd";

        /// <summary>
        /// Finds the primary taskbar window
        /// </summary>
        /// <returns>The taskbar window handle, else IntPtr.Zero if there is none (e.g. Explorer is not running)</returns>
        public static IntPtr FindPrimaryTaskbar()
        {
            var hWnd = User32.FindWindow(PrimaryTaskbarClassName, null);
            if (hWnd == IntPtr.Zero)
            {
                Trace.TraceWarning("Taskbar window '{0}' not found, error {1}", PrimaryTaskbarClassName, Marshal.GetLastWin32Error());
            }

            return hWnd;
        }
    }
}
