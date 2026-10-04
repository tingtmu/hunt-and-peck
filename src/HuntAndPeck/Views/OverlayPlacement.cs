using System.Diagnostics;
using System.Windows;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Adjusts where the overlay window goes so the shell doesn't treat it as a full screen window
    /// </summary>
    /// <remarks>
    /// Verified on Windows 11 25H2: a topmost window that exactly covers its monitor while in the foreground
    /// ends up BELOW the taskbar, even when the taskbar owns it (owned windows are normally always above their
    /// owner); 1 pixel less and it stays above. In bars mode the overlay covers the whole monitor (a top bar plus
    /// the bottom taskbar), so the taskbar's hints were hidden under the taskbar. The overlay is therefore made
    /// 1 pixel shorter at the bottom when it would cover the monitor. Its top left corner, which all hint positions
    /// are relative to, is unchanged; hint labels sit at their element's top left, never on the last pixel row.
    /// </remarks>
    internal static class OverlayPlacement
    {
        /// <summary>
        /// The overlay bounds, 1 pixel shorter at the bottom if they would cover the whole monitor
        /// </summary>
        /// <param name="overlay">Overlay bounds, physical pixels</param>
        /// <param name="monitor">Bounds of the overlay's monitor, physical pixels; empty if unknown</param>
        public static Rect AvoidCoveringMonitor(Rect overlay, Rect monitor)
        {
            if (overlay.IsEmpty || monitor.IsEmpty || !overlay.Contains(monitor) || overlay.Height <= 1)
            {
                return overlay;
            }
            return new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height - 1);
        }

        /// <summary>
        /// <see cref="AvoidCoveringMonitor(Rect, Rect)"/> with the monitor the overlay is (mostly) on
        /// </summary>
        public static Rect AvoidCoveringMonitor(Rect overlay)
        {
            RECT rect = overlay;
            var monitor = MonitorQueries.MonitorFromRect(ref rect, MonitorQueries.MONITOR_DEFAULTTONEAREST);
            Rect monitorBounds;
            if (!MonitorBounds.TryGet(monitor, out monitorBounds))
            {
                Trace.TraceWarning("Overlay: monitor of {0} unknown; placing the overlay as given", overlay);
                return overlay;
            }

            var adjusted = AvoidCoveringMonitor(overlay, monitorBounds);
            if (adjusted != overlay)
            {
                Trace.TraceInformation("Overlay: {0} covers monitor {1}; placing it 1 pixel shorter so it stays above the taskbar", overlay, monitorBounds);
            }
            return adjusted;
        }
    }
}
