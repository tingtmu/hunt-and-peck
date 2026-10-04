using System;
using System.Windows;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Decides from window rectangles whether an auto-hide taskbar is shown. Pure; no Win32 calls.
    /// </summary>
    /// <remarks>
    /// An auto-hidden taskbar keeps its size but is moved almost entirely off its monitor (on Windows 11 all but
    /// a 2 pixel strip), so the share of its window rectangle that lies within where it shows tells hidden from
    /// shown. The reference is the taskbar's shown position (ABM_GETTASKBARPOS) or, failing that, its monitor.
    /// </remarks>
    internal static class TaskbarGeometry
    {
        /// <summary>Share of the window that must lie within the reference for the taskbar to count as shown</summary>
        public const double ShownFraction = 0.9;

        /// <summary>
        /// The share (0 to 1) of the window's area that lies within the reference rectangle
        /// </summary>
        /// <returns>0 if either rectangle is empty or has no area</returns>
        public static double VisibleFraction(Rect window, Rect reference)
        {
            if (window.IsEmpty || reference.IsEmpty)
            {
                return 0;
            }

            var windowArea = window.Width * window.Height;
            if (windowArea <= 0)
            {
                return 0;
            }

            var visible = Rect.Intersect(window, reference);
            if (visible.IsEmpty)
            {
                return 0;
            }
            return Math.Min(1.0, (visible.Width * visible.Height) / windowArea);
        }

        /// <summary>
        /// True if (nearly) all of the taskbar window lies within where it shows
        /// </summary>
        public static bool IsShown(Rect window, Rect reference)
        {
            return VisibleFraction(window, reference) >= ShownFraction;
        }
    }
}
