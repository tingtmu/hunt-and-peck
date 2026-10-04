using System.Globalization;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Parses and formats the hint font size, which is stored as text (invariant culture) for compatibility
    /// with existing settings files
    /// </summary>
    internal static class FontSizeSetting
    {
        public const double Default = 14;
        public const double Min = 6;
        public const double Max = 72;

        private const NumberStyles Style =
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowDecimalPoint;

        /// <summary>
        /// Parses a font size such as "14" or "10.5", which must be within <see cref="Min"/>-<see cref="Max"/>
        /// </summary>
        /// <param name="text">The text to parse (invariant culture)</param>
        /// <param name="size">The size, else <see cref="Default"/></param>
        /// <param name="error">Why the text is invalid, else null</param>
        public static bool TryParse(string text, out double size, out string error)
        {
            double parsed;
            if (!double.TryParse(text, Style, CultureInfo.InvariantCulture, out parsed) || !(parsed >= Min && parsed <= Max))
            {
                size = Default;
                error = string.Format(CultureInfo.InvariantCulture, "Enter a number from {0} to {1}.", Min, Max);
                return false;
            }

            size = parsed;
            error = null;
            return true;
        }

        /// <summary>
        /// Formats a font size for storage, e.g. "14" or "10.5"
        /// </summary>
        public static string Format(double size)
        {
            return size.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
