using System;
using System.Collections.Generic;
using System.Windows;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Decides whether a top-level window is an edge-docked bar (a status bar such as Zebar or YASB, or a
    /// secondary taskbar). Pure; no Win32 calls. No application names are involved.
    /// </summary>
    /// <remarks>
    /// A bar is a visible, uncloaked, not minimized tool window or topmost window that is not click-through,
    /// lies within its monitor, touches one of its edges, is thin across that edge and spans most of it.
    /// Thresholds, from the windows on a 1920x1080 monitor at 150% scaling:
    /// <list type="bullet">
    /// <item>Zebar 1920x60 at the top (tool window), the taskbar 1920x72 at the bottom: thickness 5.6% and 6.7%
    /// of the monitor height, spanning 100% of the edge. Accepted.</item>
    /// <item>A clock widget 209x37 at the top (topmost, click-through): 11% of the edge, click-through. Rejected.</item>
    /// <item>A 1897x4 edge input window (cloaked) and 22x22 / 1x1 helper windows. Rejected.</item>
    /// <item>Maximized and snapped app windows touch edges but are hundreds of pixels thick. Rejected.</item>
    /// </list>
    /// <see cref="MaxThicknessFraction"/> leaves room for a two-row taskbar or a bar at 200% scaling;
    /// <see cref="MinThickness"/> rejects hairline helper windows. The edge gap tolerance is in DIPs so it means the
    /// same at every scaling. An auto-hide bar parked off screen (only a strip of a few pixels on its monitor) is
    /// rejected by <see cref="MinInsideFraction"/>; only the primary taskbar is revealed (see TaskbarRevealer).
    /// Being covered by other windows is checked separately (BarWindowFinder, with <see cref="SamplePoints"/>).
    /// </remarks>
    internal static class BarClassifier
    {
        /// <summary>Largest gap between a bar and the monitor edge it is docked to, in DIPs (scaled by the monitor's scale)</summary>
        public const double EdgeToleranceDip = 12;

        /// <summary>Thinnest bar, in physical pixels</summary>
        public const double MinThickness = 16;

        /// <summary>Thickest bar, as a share of the monitor's size across the edge</summary>
        public const double MaxThicknessFraction = 0.15;

        /// <summary>Least share of the edge's length a bar spans</summary>
        public const double MinSpanFraction = 0.5;

        /// <summary>Least share of the window's area that must lie within its monitor</summary>
        public const double MinInsideFraction = 0.9;

        /// <summary>Windows that are never bars: the primary taskbar (handled separately) and the desktop</summary>
        private static readonly HashSet<string> s_excludedClasses = new HashSet<string>(StringComparer.Ordinal)
        {
            Taskbar.PrimaryTaskbarClassName,
            "Progman",
            "WorkerW",
        };

        /// <returns>The monitor edge the window is docked to, else <see cref="BarEdge.None"/> if it is not a bar</returns>
        public static BarEdge Classify(BarCandidate window)
        {
            if (!window.Visible || window.Cloaked || window.Minimized || window.OwnProcess)
            {
                return BarEdge.None;
            }
            if (window.ClassName != null && s_excludedClasses.Contains(window.ClassName))
            {
                return BarEdge.None;
            }
            if (!HasBarStyle(window.ExStyle))
            {
                return BarEdge.None;
            }

            var bounds = window.Bounds;
            var monitor = window.Monitor;
            if (TaskbarGeometry.VisibleFraction(bounds, monitor) < MinInsideFraction)
            {
                return BarEdge.None;
            }

            var inside = Rect.Intersect(bounds, monitor);
            var tolerance = EdgeToleranceDip * (window.MonitorScale > 0 ? window.MonitorScale : 1.0);
            return inside.Width >= inside.Height ? ClassifyHorizontal(inside, monitor, tolerance) : ClassifyVertical(inside, monitor, tolerance);
        }

        /// <summary>
        /// Tool window (no taskbar button, as bars are) or topmost, and not click-through (WS_EX_TRANSPARENT:
        /// nothing to click on)
        /// </summary>
        private static bool HasBarStyle(uint exStyle)
        {
            if ((exStyle & WindowEnumeration.WS_EX_TRANSPARENT) != 0)
            {
                return false;
            }
            return (exStyle & (WindowEnumeration.WS_EX_TOOLWINDOW | WindowEnumeration.WS_EX_TOPMOST)) != 0;
        }

        private static BarEdge ClassifyHorizontal(Rect inside, Rect monitor, double tolerance)
        {
            if (!IsBarShaped(inside.Height, monitor.Height, inside.Width, monitor.Width))
            {
                return BarEdge.None;
            }
            if (inside.Top - monitor.Top <= tolerance)
            {
                return BarEdge.Top;
            }
            return monitor.Bottom - inside.Bottom <= tolerance ? BarEdge.Bottom : BarEdge.None;
        }

        private static BarEdge ClassifyVertical(Rect inside, Rect monitor, double tolerance)
        {
            if (!IsBarShaped(inside.Width, monitor.Width, inside.Height, monitor.Height))
            {
                return BarEdge.None;
            }
            if (inside.Left - monitor.Left <= tolerance)
            {
                return BarEdge.Left;
            }
            return monitor.Right - inside.Right <= tolerance ? BarEdge.Right : BarEdge.None;
        }

        /// <summary>
        /// Three points inside a bar, along its length at 10%, 50% and 90%, centred across it, for checking
        /// whether something covers it
        /// </summary>
        /// <param name="bar">The bar's on-monitor part, physical pixels</param>
        public static Point[] SamplePoints(Rect bar)
        {
            var horizontal = bar.Width >= bar.Height;
            var points = new Point[3];
            var fractions = new[] { 0.1, 0.5, 0.9 };
            for (var i = 0; i < fractions.Length; i++)
            {
                points[i] = horizontal
                    ? new Point(Math.Floor(bar.Left + (bar.Width * fractions[i])), Math.Floor(bar.Top + (bar.Height / 2)))
                    : new Point(Math.Floor(bar.Left + (bar.Width / 2)), Math.Floor(bar.Top + (bar.Height * fractions[i])));
            }
            return points;
        }

        private static bool IsBarShaped(double thickness, double monitorThickness, double length, double edgeLength)
        {
            return thickness >= MinThickness
                && thickness <= monitorThickness * MaxThicknessFraction
                && length >= edgeLength * MinSpanFraction;
        }
    }

    /// <summary>The monitor edge a bar is docked to</summary>
    internal enum BarEdge
    {
        None,
        Top,
        Bottom,
        Left,
        Right,
    }
}
