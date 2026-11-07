using System;
using System.Text.RegularExpressions;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// Reads release tags such as "v1.10.1" and compares them with the running version
    /// </summary>
    internal static class ReleaseVersion
    {
        /// <summary>2 to 4 dot-separated numbers, optionally after "v"; no pre-release or build suffix</summary>
        private static readonly Regex TagPattern = new Regex(@"^[vV]?\d{1,5}(\.\d{1,5}){1,3}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses a release tag, e.g. "v1.10.1" or "1.10.1" (surrounding white space is ignored)
        /// </summary>
        /// <param name="tag">The tag name</param>
        /// <param name="version">The version, with missing build and revision parts as 0 so it compares with an assembly version; else null</param>
        public static bool TryParse(string tag, out Version version)
        {
            version = null;
            var text = tag?.Trim();
            if (string.IsNullOrEmpty(text) || !TagPattern.IsMatch(text))
            {
                return false;
            }

            Version parsed;
            if (!Version.TryParse(text.TrimStart('v', 'V'), out parsed))
            {
                return false;
            }
            version = Normalize(parsed);
            return true;
        }

        /// <summary>
        /// Whether the tag names a version newer than the running one; false (nothing to offer) for a tag that
        /// is not a version
        /// </summary>
        /// <param name="tag">The latest release's tag name</param>
        /// <param name="current">The running version, e.g. the assembly version</param>
        /// <param name="latest">The tag's version, else null</param>
        public static bool IsNewer(string tag, Version current, out Version latest)
        {
            return TryParse(tag, out latest) && latest > Normalize(current);
        }

        /// <summary>Formats a version for people: "1.10.1", or four parts only if the revision is not 0</summary>
        public static string Format(Version version)
        {
            var normalized = Normalize(version);
            return normalized.ToString(normalized.Revision == 0 ? 3 : 4);
        }

        /// <summary>Fills in the unspecified build and revision (-1), which would otherwise sort below 0</summary>
        private static Version Normalize(Version version)
        {
            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
        }
    }
}
