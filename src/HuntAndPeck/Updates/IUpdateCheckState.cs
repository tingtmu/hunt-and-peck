using System;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// Remembers when the update check last succeeded (persisted)
    /// </summary>
    internal interface IUpdateCheckState
    {
        /// <summary>When the last check succeeded (UTC), null if never or unreadable</summary>
        DateTime? LastCheckUtc { get; set; }
    }
}
