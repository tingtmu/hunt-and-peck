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
        /// The label's size as the overlay draws it on an element: shrunk uniformly to fit an element smaller than
        /// the label (as the overlay's Viewbox does), else unchanged
        /// </summary>
        public static Size FitLabel(Size element, Size label)
        {
            if (!IsKnown(label))
            {
                return label;
            }
            var width = Math.Max(element.Width, 0);
            var height = Math.Max(element.Height, 0);
            var scale = Math.Min(1, Math.Min(width / label.Width, height / label.Height));
            return new Size(label.Width * scale, label.Height * scale);
        }

        /// <summary>
        /// The centre of the hint label, where a hover lands, so the cursor ends up where the hint was shown.
        /// <paramref name="label"/> is where the overlay drew the (fitted) label, relative to the element's top
        /// left corner (see <see cref="LabelLayout"/>). A label drawn outside the element still hovers the element:
        /// the point is kept inside it.
        /// </summary>
        /// <param name="label">The label, relative to the element's top left corner; empty if unknown (the centre)</param>
        public static Point LabelCentre(int left, int top, int right, int bottom, Rect label)
        {
            if (label.IsEmpty || !IsKnown(label.Size))
            {
                return Centre(left, top, right, bottom);
            }
            var x = left + (int)(label.X + label.Width / 2);
            var y = top + (int)(label.Y + label.Height / 2);
            return new Point(Clamp(x, left, right - 1), Clamp(y, top, bottom - 1));
        }

        private static bool IsKnown(Size size)
        {
            return !size.IsEmpty && size.Width > 0 && size.Height > 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            return max < min ? min : Math.Max(min, Math.Min(max, value));
        }
    }
}
