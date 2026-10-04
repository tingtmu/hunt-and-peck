using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// <see cref="ITaskbarShell"/> backed by the shell appbar API and user32. Call on the UI thread.
    /// </summary>
    internal sealed class Win32TaskbarShell : ITaskbarShell
    {
        private bool _rectFailureLogged;

        public bool IsAutoHide()
        {
            var data = APPBARDATA.Create();
            var state = (uint)Shell32.SHAppBarMessage(Shell32.ABM_GETSTATE, ref data);
            return (state & Shell32.ABS_AUTOHIDE) != 0;
        }

        public bool IsHung(IntPtr hWnd)
        {
            return User32.IsHungAppWindow(hWnd);
        }

        public bool TryGetWindowBounds(IntPtr hWnd, out Rect bounds)
        {
            var window = new RECT();
            if (User32.GetWindowRect(hWnd, ref window))
            {
                bounds = window;
                return true;
            }

            // Polled repeatedly while revealing: logged once per instance
            if (!_rectFailureLogged)
            {
                _rectFailureLogged = true;
                Trace.TraceWarning("Taskbar: GetWindowRect failed for window {0}, error {1}", hWnd, Marshal.GetLastWin32Error());
            }
            bounds = Rect.Empty;
            return false;
        }

        /// <summary>
        /// ABM_GETTASKBARPOS, which reports the shown position also while the taskbar is auto-hidden, else the
        /// bounds of the monitor nearest to it
        /// </summary>
        public bool TryGetShownBounds(IntPtr taskbar, out Rect bounds)
        {
            var data = APPBARDATA.Create();
            if (Shell32.SHAppBarMessage(Shell32.ABM_GETTASKBARPOS, ref data) != UIntPtr.Zero)
            {
                bounds = data.rc;
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    return true;
                }
            }

            var monitor = User32.MonitorFromWindow(taskbar, User32.MONITOR_DEFAULTTONEAREST);
            var info = MONITORINFO.Create();
            if (monitor != IntPtr.Zero && User32.GetMonitorInfo(monitor, ref info))
            {
                bounds = info.rcMonitor;
                return true;
            }

            Trace.TraceWarning("Taskbar: neither ABM_GETTASKBARPOS nor GetMonitorInfo reported where window {0} shows", taskbar);
            bounds = Rect.Empty;
            return false;
        }

        public IntPtr GetForegroundWindow()
        {
            return User32.GetForegroundWindow();
        }

        public void Activate(IntPtr hWnd)
        {
            ForegroundActivation.Activate(hWnd);
        }
    }
}
