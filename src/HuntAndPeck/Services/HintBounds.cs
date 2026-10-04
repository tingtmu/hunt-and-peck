using System.Collections.Generic;
using System.Windows;
using HuntAndPeck.Extensions;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Converts an element's screen bounding rectangle to hint bounds. Pure; no UI Automation calls.
    /// </summary>
    internal static class HintBounds
    {
        /// <summary>Share of its window's area from which an element counts as covering the window</summary>
        public const double WindowCoverFraction = 0.9;

        /// <summary>
        /// Converts an element's bounding rectangle (physical screen pixels, same unit as the window bounds)
        /// to owning window coordinates
        /// </summary>
        /// <returns>False if the rectangle is empty or lies outside the window, so its hint could never be seen</returns>
        public static bool TryToWindowCoordinates(int left, int top, int right, int bottom, Rect windowBounds, out Rect windowCoordinates)
        {
            return TryToOverlayCoordinates(left, top, right, bottom, windowBounds, windowBounds, out windowCoordinates);
        }

        /// <summary>
        /// Converts an element's bounding rectangle (physical screen pixels) to coordinates relative to an overlay
        /// that covers its window, and possibly other windows too (bars mode)
        /// </summary>
        /// <param name="left">Element bounds, physical screen pixels</param>
        /// <param name="top">Element bounds, physical screen pixels</param>
        /// <param name="right">Element bounds, physical screen pixels</param>
        /// <param name="bottom">Element bounds, physical screen pixels</param>
        /// <param name="windowBounds">The element's window; elements outside it are dropped</param>
        /// <param name="overlayBounds">The overlay; the result is relative to its top left corner</param>
        /// <param name="overlayCoordinates">The element bounds relative to the overlay</param>
        /// <returns>False if the rectangle is empty or lies outside the window, so its hint could never be seen</returns>
        public static bool TryToOverlayCoordinates(int left, int top, int right, int bottom, Rect windowBounds, Rect overlayBounds, out Rect overlayCoordinates)
        {
            overlayCoordinates = Rect.Empty;
            if ((right <= left) || (bottom <= top))
            {
                return false;
            }

            var screenRect = new Rect(new Point(left, top), new Point(right, bottom));
            if (!screenRect.OverlapsWith(windowBounds))
            {
                return false;
            }

            overlayCoordinates = screenRect.ScreenToWindowCoordinates(overlayBounds);
            return true;
        }

        /// <summary>
        /// The part of the window on the monitor, else <see cref="Rect.Empty"/> if none (or no area). A window
        /// partly off screen (e.g. an auto-hide taskbar that did not show) can't stretch the overlay off screen.
        /// </summary>
        public static Rect ClipToMonitor(Rect window, Rect monitor)
        {
            var clipped = Rect.Intersect(window, monitor);
            return clipped.IsEmpty || clipped.Width <= 0 || clipped.Height <= 0 ? Rect.Empty : clipped;
        }

        /// <summary>
        /// The smallest rectangle containing all the rectangles (the overlay covering several windows), else
        /// <see cref="Rect.Empty"/> if there are none; empty rectangles are ignored
        /// </summary>
        public static Rect UnionOf(IEnumerable<Rect> rects)
        {
            var union = Rect.Empty;
            foreach (var rect in rects)
            {
                union = Rect.Union(union, rect);
            }
            return union;
        }

        /// <summary>
        /// True if the element covers (nearly) its whole window, as page-level click handlers in web content
        /// (e.g. a WebView2 bar's body) do; their hint would sit at the window corner and mean nothing
        /// </summary>
        public static bool CoversWindow(Rect element, Rect windowBounds)
        {
            if (element.IsEmpty || windowBounds.IsEmpty || windowBounds.Width <= 0 || windowBounds.Height <= 0)
            {
                return false;
            }
            var covered = Rect.Intersect(element, windowBounds);
            if (covered.IsEmpty)
            {
                return false;
            }
            return covered.Width * covered.Height >= windowBounds.Width * windowBounds.Height * WindowCoverFraction;
        }
    }
}
