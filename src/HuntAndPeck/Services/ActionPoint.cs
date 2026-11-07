using System;
using System.Windows;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Where a synthesized mouse action lands on an element. Pure; physical screen pixels.
    /// </summary>
    internal static class ActionPoint
    {
        /// <summary>Average width of a bold Arial capital, in ems</summary>
        public const double LetterWidthEm = 0.75;

        /// <summary>Line height of the label text, in ems</summary>
        public const double LineHeightEm = 1.2;

        /// <summary>The label's 1-px margin and 1-px border, per side</summary>
        public const double LabelChrome = 2;

        /// <summary>
        /// About the size of a hint label as the overlay draws it, before it is shrunk to fit a smaller element.
        /// The overlay's content unit is one physical pixel, so this is in physical pixels too.
        /// </summary>
        public static Size EstimateLabelSize(string label, double fontSize)
        {
            var letters = string.IsNullOrEmpty(label) ? 1 : label.Length;
            return new Size(letters * fontSize * LetterWidthEm + 2 * LabelChrome, fontSize * LineHeightEm + 2 * LabelChrome);
        }

        /// <summary>The centre of the rectangle; where clicks land</summary>
        public static Point Centre(int left, int top, int right, int bottom)
        {
            return new Point((left + right) / 2, (top + bottom) / 2);
        }

        /// <summary>
        /// The centre of the hint label, which the overlay draws at the element's top left corner, shrunk to fit
        /// the element if it is smaller (uniformly, as the overlay's Viewbox does); where a hover lands, so the
        /// cursor ends up where the hint was shown
        /// </summary>
        public static Point LabelCentre(int left, int top, int right, int bottom, Size labelSize)
        {
            var width = Math.Max(right - left, 0);
            var height = Math.Max(bottom - top, 0);
            if (labelSize.IsEmpty || labelSize.Width <= 0 || labelSize.Height <= 0)
            {
                return Centre(left, top, right, bottom);
            }
            var scale = Math.Min(1, Math.Min(width / labelSize.Width, height / labelSize.Height));
            return new Point(left + (int)(labelSize.Width * scale / 2), top + (int)(labelSize.Height * scale / 2));
        }
    }
}
