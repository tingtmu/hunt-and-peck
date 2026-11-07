using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// Checks for a newer release, automatically at start-up (at most once a day, if enabled) or when asked.
    /// Only tells the user where to get the update; it never downloads or replaces files.
    /// </summary>
    /// <remarks>Use on the UI thread: the awaits resume there, where the settings and the notification belong</remarks>
    internal sealed class UpdateChecker
    {
        private readonly IReleaseClient _client;
        private readonly IUpdateCheckState _state;
        private readonly Func<bool> _automaticEnabled;
        private readonly Version _current;
        private readonly bool _isScoop;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly Action<string, Action> _notify;
        private readonly Action<string> _openUrl;
        private bool _checking;

        /// <param name="client">Fetches the latest release</param>
        /// <param name="state">Remembers the last successful check</param>
        /// <param name="automaticEnabled">Whether the "Check for updates automatically" setting is on, read when needed</param>
        /// <param name="current">The running version</param>
        /// <param name="isScoop">Whether Scoop installed this copy (<see cref="ScoopInstall.IsScoopPath"/>)</param>
        /// <param name="utcNow">The current time</param>
        /// <param name="delay">Waits for the given time</param>
        /// <param name="notify">Shows a notification with the text and what a click on it does (null for nothing)</param>
        /// <param name="openUrl">Opens a web page</param>
        public UpdateChecker(
            IReleaseClient client, IUpdateCheckState state, Func<bool> automaticEnabled, Version current, bool isScoop,
            Func<DateTime> utcNow, Func<TimeSpan, Task> delay, Action<string, Action> notify, Action<string> openUrl)
        {
            _client = client;
            _state = state;
            _automaticEnabled = automaticEnabled;
            _current = current;
            _isScoop = isScoop;
            _utcNow = utcNow;
            _delay = delay;
            _notify = notify;
            _openUrl = openUrl;
        }

        /// <summary>
        /// The start-up check: waits, then checks if the setting is on and the last check is over a day old.
        /// Tells the user only about a new version. Never throws.
        /// </summary>
        public async Task RunAutomaticAsync()
        {
            try
            {
                await _delay(UpdateSchedule.StartupDelay);
                if (!_automaticEnabled())
                {
                    Trace.TraceInformation("Updates: automatic check is off");
                    return;
                }
                if (!UpdateSchedule.IsDue(_state.LastCheckUtc, _utcNow()))
                {
                    Trace.TraceInformation("Updates: last checked {0:u}, not due yet", _state.LastCheckUtc);
                    return;
                }

                var result = await CheckAsync();
                if (result != null && result.Outcome == UpdateOutcome.Available)
                {
                    ShowAvailable(result);
                }
            }
            catch (Exception ex)
            {
                // Called without awaiting: an escaping exception would go unobserved
                Trace.TraceWarning("Updates: the automatic check failed: {0}", ex);
            }
        }

        /// <summary>
        /// The tray menu's check: always tells the user the result (up to date, new version or couldn't check). Never throws.
        /// </summary>
        public async Task CheckManuallyAsync()
        {
            try
            {
                var result = await CheckAsync();
                if (result == null)
                {
                    _notify(UpdateMessages.AlreadyChecking, null);
                }
                else if (result.Outcome == UpdateOutcome.Available)
                {
                    ShowAvailable(result);
                }
                else
                {
                    _notify(result.Outcome == UpdateOutcome.UpToDate ? UpdateMessages.UpToDate(_current) : UpdateMessages.CheckFailed, null);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Updates: the manual check failed: {0}", ex);
            }
        }

        private void ShowAvailable(UpdateCheckResult result)
        {
            var onClick = _isScoop ? null : new Action(() => _openUrl(result.Release.PageUrl));
            _notify(UpdateMessages.Available(result.Version, _isScoop), onClick);
        }

        /// <returns>The result, or null if a check is already running</returns>
        private async Task<UpdateCheckResult> CheckAsync()
        {
            if (_checking)
            {
                return null;
            }

            _checking = true;
            try
            {
                return await FetchAndCompareAsync();
            }
            finally
            {
                _checking = false;
            }
        }

        private async Task<UpdateCheckResult> FetchAndCompareAsync()
        {
            ReleaseInfo release;
            try
            {
                release = await _client.GetLatestAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Updates: fetching the latest release failed: {0}", ex);
                return UpdateCheckResult.Failed;
            }

            Version latest;
            if (!ReleaseVersion.TryParse(release?.Tag, out latest))
            {
                // Nothing is offered; for the user it is a check that gave no answer
                Trace.TraceWarning("Updates: the latest release's tag \"{0}\" is not a version", release?.Tag);
                return UpdateCheckResult.Failed;
            }

            RememberCheck();
            Trace.TraceInformation("Updates: running {0}, latest release {1}", _current, release.Tag);
            return ReleaseVersion.IsNewer(release.Tag, _current, out latest)
                ? UpdateCheckResult.NewVersion(release, latest)
                : UpdateCheckResult.UpToDate;
        }

        private void RememberCheck()
        {
            try
            {
                _state.LastCheckUtc = _utcNow();
            }
            catch (Exception ex)
            {
                // Not worth telling the user: the next start just checks again
                Trace.TraceWarning("Updates: saving the time of the check failed: {0}", ex);
            }
        }
    }
}
