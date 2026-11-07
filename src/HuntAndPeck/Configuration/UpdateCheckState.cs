using System;
using System.Diagnostics;
using HuntAndPeck.Properties;
using HuntAndPeck.Updates;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// The time of the last update check, stored in the user settings (<see cref="Settings.LastUpdateCheckUtc"/>)
    /// </summary>
    /// <remarks>Use on the UI thread</remarks>
    internal sealed class UpdateCheckState : IUpdateCheckState
    {
        /// <summary>The invalid stored value already logged, so it is logged once rather than on every read</summary>
        private string _reportedInvalid;

        public DateTime? LastCheckUtc
        {
            get
            {
                var text = Settings.Default.LastUpdateCheckUtc;
                DateTime? last;
                string error;
                if (!UpdateSchedule.TryParse(text, out last, out error) && _reportedInvalid != text)
                {
                    _reportedInvalid = text;
                    Trace.TraceWarning("Settings: invalid {0} \"{1}\" ({2}); treating it as never checked", nameof(Settings.LastUpdateCheckUtc), text, error);
                }
                return last;
            }
            set
            {
                var settings = Settings.Default;
                settings.LastUpdateCheckUtc = value.HasValue ? UpdateSchedule.Format(value.Value) : string.Empty;
                settings.Save();
            }
        }
    }
}
