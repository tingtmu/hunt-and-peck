using System.Linq;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Validates the letters used for hint labels
    /// </summary>
    internal static class HintAlphabet
    {
        public const string Default = "SADFJKLEWCMPGH";
        public const int MinLength = 2;
        public const int MaxLength = 26;

        /// <summary>
        /// Validates an alphabet: 2-26 distinct letters A-Z, case-insensitive. Surrounding whitespace is ignored.
        /// </summary>
        /// <param name="text">The alphabet to check</param>
        /// <param name="alphabet">The alphabet in upper case, else null</param>
        /// <param name="error">Why the alphabet is invalid, else null</param>
        public static bool TryNormalize(string text, out string alphabet, out string error)
        {
            alphabet = null;
            var trimmed = (text ?? string.Empty).Trim();

            // Check before upper-casing: ToUpperInvariant maps some non-ASCII letters (e.g. 'ı') to A-Z
            if (!trimmed.All(IsAsciiLetter))
            {
                error = "Use only the letters A-Z.";
                return false;
            }

            var upper = trimmed.ToUpperInvariant();
            if (upper.Length < MinLength || upper.Length > MaxLength)
            {
                error = string.Format("Use {0} to {1} letters.", MinLength, MaxLength);
                return false;
            }

            var duplicates = upper.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            if (duplicates.Length > 0)
            {
                error = string.Format("Each letter can be used only once (repeated: {0}).", string.Join(", ", duplicates));
                return false;
            }

            alphabet = upper;
            error = null;
            return true;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }
    }
}
