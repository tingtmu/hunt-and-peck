using System;
using System.Globalization;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// When the automatic update check runs, and how the time of the last check is stored (as text, like the
    /// other settings)
    /// </summary>
    internal static class UpdateSchedule
    {
        /// <summary>How long after start-up the automatic check waits, so it never slows the start</summary>
        public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

        /// <summary>The automatic check runs at most this often</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        /// <summary>
        /// Whether an automatic check is due
        /// </summary>
        /// <param name="lastCheckUtc">When the last check succeeded, null if never</param>
        /// <param name="utcNow">The current time</param>
        /// <remarks>A last check in the future (the clock was set back) counts as due, so it can't stop checks for good</remarks>
        public static bool IsDue(DateTime? lastCheckUtc, DateTime utcNow)
        {
            return !lastCheckUtc.HasValue
                || lastCheckUtc.Value > utcNow
                || utcNow - lastCheckUtc.Value >= Interval;
        }

        /// <summary>
        /// Parses the stored time of the last check
        /// </summary>
        /// <param name="text">The stored text; empty means never checked</param>
        /// <param name="lastCheckUtc">The time in UTC, else null</param>
        /// <param name="error">Why the text is invalid, else null (also null for empty text)</param>
        /// <returns>False if the text is not empty and not a valid time</returns>
        public static bool TryParse(string text, out DateTime? lastCheckUtc, out string error)
        {
            lastCheckUtc = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            DateTime parsed;
            const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, styles, out parsed))
            {
                error = "Not a date and time.";
                return false;
            }

            lastCheckUtc = parsed;
            return true;
        }

        /// <summary>Formats a time for storage, e.g. "2026-11-07T10:30:00.0000000Z"</summary>
        public static string Format(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
