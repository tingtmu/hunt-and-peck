using System.Windows;
using HuntAndPeck.Extensions;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Converts an element's screen bounding rectangle to hint bounds. Pure; no UI Automation calls.
    /// </summary>
    internal static class HintBounds
    {
        /// <summary>
        /// Converts an element's bounding rectangle (physical screen pixels, same unit as the window bounds)
        /// to owning window coordinates
        /// </summary>
        /// <returns>False if the rectangle is empty or lies outside the window, so its hint could never be seen</returns>
        public static bool TryToWindowCoordinates(int left, int top, int right, int bottom, Rect windowBounds, out Rect windowCoordinates)
        {
            windowCoordinates = Rect.Empty;
            if ((right <= left) || (bottom <= top))
            {
                return false;
            }

            var screenRect = new Rect(new Point(left, top), new Point(right, bottom));
            if (!screenRect.OverlapsWith(windowBounds))
            {
                return false;
            }

            windowCoordinates = screenRect.ScreenToWindowCoordinates(windowBounds);
            return true;
        }
    }
}
