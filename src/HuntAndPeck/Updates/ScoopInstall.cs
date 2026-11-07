using System;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// Tells whether hap.exe was installed by Scoop, which updates it itself
    /// </summary>
    internal static class ScoopInstall
    {
        /// <summary>The command that updates the Scoop package</summary>
        public const string UpdateCommand = "scoop update huntandpeck";

        private const string AppsFolder = @"\scoop\apps\";

        /// <summary>
        /// True if the path is under a Scoop apps folder, e.g. C:\Users\me\scoop\apps\huntandpeck\current\hap.exe
        /// (case-insensitive; forward slashes count as backslashes)
        /// </summary>
        public static bool IsScoopPath(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }
            return exePath.Replace('/', '\\').IndexOf(AppsFolder, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
