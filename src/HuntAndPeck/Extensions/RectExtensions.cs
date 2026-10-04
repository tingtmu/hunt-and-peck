using System.Windows;

namespace HuntAndPeck.Extensions
{
    public static class RectExtensions
    {
        /// <summary>
        /// Determines whether two rectangles overlap by a non-zero area (touching edges do not count)
        /// </summary>
        /// <param name="source">The source rectangle, e.g. an element's bounds</param>
        /// <param name="other">The other rectangle, e.g. the owning window's bounds</param>
        /// <returns>True if the rectangles share a non-empty area</returns>
        public static bool OverlapsWith(this Rect source, Rect other)
        {
            if (source.IsEmpty || other.IsEmpty)
            {
                return false;
            }

            return source.Left < other.Right && source.Right > other.Left &&
                   source.Top < other.Bottom && source.Bottom > other.Top;
        }

        /// <summary>
        /// Converts screen coordinates to window coordinates for a given window.
        /// Both rectangles must use the same unit (physical pixels under per-monitor DPI awareness).
        /// </summary>
        /// <param name="source">The screen coordinates</param>
        /// <param name="windowRect">The bounds of the window in which the source rect lies</param>
        /// <returns></returns>
        public static Rect ScreenToWindowCoordinates(this Rect source, Rect windowRect)
        {
            var result = new Rect(source.TopLeft, source.BottomRight);
            result.X -= windowRect.X;
            result.Y -= windowRect.Y;

            return result;
        }
    }
}
