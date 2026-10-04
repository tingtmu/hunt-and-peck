using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Where bars mode looks: the monitor under the mouse cursor, and the primary taskbar if it is on that monitor
    /// </summary>
    /// <remarks>
    /// One monitor keeps the overlay on one DPI (hint labels and placement stay consistent) and the hints few.
    /// Secondary taskbars on the monitor are found as bars (<see cref="BarClassifier"/>).
    /// </remarks>
    internal sealed class BarsScope
    {
        public BarsScope(IntPtr monitor, Rect monitorBounds, double monitorScale, IntPtr taskbar)
        {
            Monitor = monitor;
            MonitorBounds = monitorBounds;
            MonitorScale = monitorScale;
            Taskbar = taskbar;
        }

        /// <summary>The monitor handle (HMONITOR)</summary>
        public IntPtr Monitor { get; private set; }

        /// <summary>The monitor's bounds, physical pixels; the overlay is clipped to them</summary>
        public Rect MonitorBounds { get; private set; }

        /// <summary>The monitor's scale factor (1 = 96 DPI)</summary>
        public double MonitorScale { get; private set; }

        /// <summary>The primary taskbar if it is on this monitor, else IntPtr.Zero</summary>
        public IntPtr Taskbar { get; private set; }

        /// <summary>
        /// The scope for the monitor under the mouse cursor
        /// </summary>
        /// <returns>The scope, else null if the monitor is unknown (logged)</returns>
        public static BarsScope ForCursor()
        {
            POINT cursor;
            if (!MonitorQueries.GetCursorPos(out cursor))
            {
                // E.g. on the secure desktop; the primary monitor contains (0, 0)
                Trace.TraceWarning("Bars: GetCursorPos failed, error {0}; using the primary monitor", Marshal.GetLastWin32Error());
                cursor = new POINT(0, 0);
            }

            var monitor = MonitorQueries.MonitorFromPoint(cursor, MonitorQueries.MONITOR_DEFAULTTONEAREST);
            Rect bounds;
            if (!Services.MonitorBounds.TryGet(monitor, out bounds))
            {
                Trace.TraceWarning("Bars: monitor at cursor ({0}, {1}) unknown; no overlay shown", cursor.X, cursor.Y);
                return null;
            }

            var taskbar = Services.Taskbar.FindPrimaryTaskbar();
            if (taskbar != IntPtr.Zero && GetTaskbarMonitor(taskbar) != monitor)
            {
                Trace.TraceInformation("Bars: the primary taskbar is on another monitor than the cursor; leaving it out");
                taskbar = IntPtr.Zero;
            }
            return new BarsScope(monitor, bounds, Services.MonitorBounds.GetScale(monitor), taskbar);
        }

        /// <summary>
        /// The taskbar's monitor, from where it shows (ABM_GETTASKBARPOS), which also holds while it is auto-hidden
        /// and parked mostly off screen, possibly over a neighbouring monitor
        /// </summary>
        private static IntPtr GetTaskbarMonitor(IntPtr taskbar)
        {
            var data = APPBARDATA.Create();
            if (Shell32.SHAppBarMessage(Shell32.ABM_GETTASKBARPOS, ref data) != UIntPtr.Zero)
            {
                var rc = data.rc;
                return MonitorQueries.MonitorFromRect(ref rc, MonitorQueries.MONITOR_DEFAULTTONEAREST);
            }
            return User32.MonitorFromWindow(taskbar, User32.MONITOR_DEFAULTTONEAREST);
        }
    }
}
