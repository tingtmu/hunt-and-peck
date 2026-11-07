using System;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// The texts the update check shows
    /// </summary>
    internal static class UpdateMessages
    {
        public const string CheckFailed = "Couldn't check for updates. Check your internet connection and try again.";
        public const string AlreadyChecking = "Already checking for updates.";

        /// <summary>A newer version exists: how to get it (Scoop updates itself; otherwise the release page opens on a click)</summary>
        public static string Available(Version latest, bool isScoop)
        {
            var text = "HuntAndPeck " + ReleaseVersion.Format(latest) + " is available. ";
            return text + (isScoop
                ? "Run \"" + ScoopInstall.UpdateCommand + "\" to update."
                : "Click here to open the release page.");
        }

        public static string UpToDate(Version current)
        {
            return "HuntAndPeck " + ReleaseVersion.Format(current) + " is up to date.";
        }
    }
}
