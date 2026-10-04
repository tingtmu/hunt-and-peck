using System;

namespace HuntAndPeck.Diagnostics
{
    /// <summary>
    /// Allows at most one notification per interval
    /// </summary>
    internal sealed class NotificationThrottle
    {
        private readonly TimeSpan _interval;
        private readonly object _lock = new object();
        private DateTime? _lastNotificationUtc;

        public NotificationThrottle(TimeSpan interval)
        {
            _interval = interval;
        }

        /// <summary>
        /// Returns true (and records the notification) if a notification may be shown at the given time
        /// </summary>
        public bool TryAcquire(DateTime utcNow)
        {
            lock (_lock)
            {
                if (_lastNotificationUtc.HasValue && utcNow - _lastNotificationUtc.Value < _interval)
                {
                    return false;
                }

                _lastNotificationUtc = utcNow;
                return true;
            }
        }
    }
}
