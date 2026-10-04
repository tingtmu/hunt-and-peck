using System;
using System.Diagnostics;
using System.Windows;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Monitor bounds and scale queries
    /// </summary>
    internal static class MonitorBounds
    {
        private const double DefaultDpi = 96;

        /// <summary>
        /// Gets the bounds (physical pixels) of the monitor nearest to the window
        /// </summary>
        /// <returns>False if the monitor is unknown</returns>
        public static bool TryGetForWindow(IntPtr hWnd, out Rect bounds)
        {
            return TryGet(User32.MonitorFromWindow(hWnd, User32.MONITOR_DEFAULTTONEAREST), out bounds);
        }

        /// <summary>
        /// Gets the bounds (physical pixels) of the monitor
        /// </summary>
        /// <returns>False if the monitor is unknown</returns>
        public static bool TryGet(IntPtr monitor, out Rect bounds)
        {
            var info = MONITORINFO.Create();
            if (monitor == IntPtr.Zero || !User32.GetMonitorInfo(monitor, ref info))
            {
                bounds = Rect.Empty;
                return false;
            }
            bounds = info.rcMonitor;
            return true;
        }

        /// <summary>
        /// The monitor's scale factor (effective DPI / 96), else 1 if unknown (logged)
        /// </summary>
        public static double GetScale(IntPtr monitor)
        {
            uint dpiX, dpiY;
            var hr = MonitorQueries.GetDpiForMonitor(monitor, MonitorQueries.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
            if (hr != 0 || dpiX == 0)
            {
                Trace.TraceWarning("GetDpiForMonitor failed for monitor {0}, HRESULT 0x{1:X8}; assuming 96 DPI", monitor, hr);
                return 1.0;
            }
            return dpiX / DefaultDpi;
        }
    }
}
