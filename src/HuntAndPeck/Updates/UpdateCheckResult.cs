using System;

namespace HuntAndPeck.Updates
{
    /// <summary>How an update check ended</summary>
    internal enum UpdateOutcome
    {
        /// <summary>The running version is the latest (or newer)</summary>
        UpToDate,

        /// <summary>A newer release exists</summary>
        Available,

        /// <summary>No answer: network or server problem, or an unreadable release</summary>
        Failed
    }

    /// <summary>
    /// The result of one update check
    /// </summary>
    internal sealed class UpdateCheckResult
    {
        public static readonly UpdateCheckResult UpToDate = new UpdateCheckResult(UpdateOutcome.UpToDate, null, null);
        public static readonly UpdateCheckResult Failed = new UpdateCheckResult(UpdateOutcome.Failed, null, null);

        private UpdateCheckResult(UpdateOutcome outcome, ReleaseInfo release, Version version)
        {
            Outcome = outcome;
            Release = release;
            Version = version;
        }

        public static UpdateCheckResult NewVersion(ReleaseInfo release, Version version)
        {
            return new UpdateCheckResult(UpdateOutcome.Available, release, version);
        }

        public UpdateOutcome Outcome { get; }

        /// <summary>The newer release; null unless <see cref="Outcome"/> is <see cref="UpdateOutcome.Available"/></summary>
        public ReleaseInfo Release { get; }

        /// <summary>The newer release's version; null unless available</summary>
        public Version Version { get; }
    }
}
