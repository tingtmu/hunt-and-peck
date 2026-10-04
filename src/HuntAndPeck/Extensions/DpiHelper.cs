using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Extensions
{
    /// <summary>
    /// DPI helpers for a per-monitor (v2) DPI aware process.
    /// </summary>
    /// <remarks>
    /// Under PerMonitorV2, UI Automation bounding rectangles and GetWindowRect are physical pixels,
    /// while WPF lays out in device independent pixels (DIPs) of the monitor its window is on:
    /// dip = physical / scale, where scale = dpi / 96.
    /// </remarks>
    public static class DpiHelper
    {
        public const double DefaultDpi = 96.0;

        /// <summary>
        /// Converts a DPI value to a scale factor (1.0 at 96 DPI). Returns 1.0 for an unknown (0) DPI.
        /// </summary>
        public static double ScaleFromDpi(uint dpi)
        {
            return dpi == 0 ? 1.0 : dpi / DefaultDpi;
        }

        /// <summary>
        /// Gets the factor that maps physical pixels to DIPs for the given device scale (1 / scale).
        /// </summary>
        public static double PhysicalToDipFactor(double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be a positive finite number");
            }

            return 1.0 / scale;
        }

        /// <summary>
        /// Converts a rectangle in physical pixels to DIPs for the given device scale.
        /// </summary>
        public static Rect PhysicalToDip(Rect physical, double scale)
        {
            var factor = PhysicalToDipFactor(scale);
            if (physical.IsEmpty)
            {
                return Rect.Empty;
            }

            return new Rect(physical.X * factor, physical.Y * factor, physical.Width * factor, physical.Height * factor);
        }

        /// <summary>
        /// Gets the device scale WPF renders the given window with.
        /// Uses GetDpiForWindow, falling back to the WPF visual's DPI.
        /// </summary>
        /// <param name="hWnd">The window handle of the visual's window (may be IntPtr.Zero)</param>
        /// <param name="visual">The visual used as a fallback DPI source</param>
        public static double GetDeviceScale(IntPtr hWnd, Visual visual)
        {
            var dpi = TryGetDpiForWindow(hWnd);
            if (dpi != 0)
            {
                return ScaleFromDpi(dpi);
            }

            var wpfScale = VisualTreeHelper.GetDpi(visual).DpiScaleX;
            return wpfScale > 0 ? wpfScale : 1.0;
        }

        private static uint TryGetDpiForWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                return User32.GetDpiForWindow(hWnd);
            }
            catch (EntryPointNotFoundException ex)
            {
                // Pre Windows 10 1607
                Trace.TraceWarning("GetDpiForWindow unavailable, using WPF DPI: {0}", ex.Message);
                return 0;
            }
        }
    }
}
