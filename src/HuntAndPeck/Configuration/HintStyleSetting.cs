using System;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Parses and formats the hint style, which is stored as its name
    /// </summary>
    internal static class HintStyleSetting
    {
        public const HintStyle Default = HintStyle.Classic;

        /// <summary>
        /// Parses a style name such as "Theme" (case-sensitive; numbers are not accepted)
        /// </summary>
        /// <param name="text">The stored text</param>
        /// <param name="style">The style, else <see cref="Default"/></param>
        /// <param name="error">Why the text is invalid, else null</param>
        public static bool TryParse(string text, out HintStyle style, out string error)
        {
            foreach (HintStyle candidate in Enum.GetValues(typeof(HintStyle)))
            {
                if (string.Equals(text, Format(candidate), StringComparison.Ordinal))
                {
                    style = candidate;
                    error = null;
                    return true;
                }
            }

            style = Default;
            error = "Not a known hint style.";
            return false;
        }

        /// <summary>
        /// Formats a style for storage, e.g. "Theme"
        /// </summary>
        public static string Format(HintStyle style)
        {
            return style.ToString();
        }
    }
}
