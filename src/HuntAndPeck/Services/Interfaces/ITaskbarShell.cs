using System;
using System.Windows;

namespace HuntAndPeck.Services.Interfaces
{
    /// <summary>
    /// The Win32 operations needed to reveal an auto-hide taskbar; faked in tests
    /// </summary>
    internal interface ITaskbarShell
    {
        /// <summary>True if the taskbar is set to auto-hide</summary>
        bool IsAutoHide();

        /// <summary>True if the window's thread is not responding</summary>
        bool IsHung(IntPtr hWnd);

        /// <summary>
        /// Where the taskbar shows, in physical screen pixels (also while it is auto-hidden)
        /// </summary>
        /// <returns>False if unknown (logged)</returns>
        bool TryGetShownBounds(IntPtr taskbar, out Rect bounds);

        /// <summary>The window's current bounds, in physical screen pixels</summary>
        /// <returns>False if unknown (logged)</returns>
        bool TryGetWindowBounds(IntPtr hWnd, out Rect bounds);

        IntPtr GetForegroundWindow();

        /// <summary>
        /// Makes the window the foreground window, as far as Windows allows. The result is not reported: it is
        /// unreliable (SetForegroundWindow may report failure yet switch), so callers check the effect.
        /// </summary>
        void Activate(IntPtr hWnd);
    }
}
