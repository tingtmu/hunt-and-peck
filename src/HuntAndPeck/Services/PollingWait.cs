using System;
using System.Threading.Tasks;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Waits asynchronously for a condition by polling it, without blocking the calling (UI) thread
    /// </summary>
    internal static class PollingWait
    {
        /// <summary>
        /// Polls <paramref name="condition"/> right away and then every <paramref name="interval"/> until it is
        /// true or <paramref name="timeout"/> has passed
        /// </summary>
        /// <param name="condition">Checked on the awaiting context (the UI thread when awaited there)</param>
        /// <param name="delay">Waits for the given time; Task.Delay outside tests</param>
        /// <param name="now">Monotonic clock</param>
        /// <returns>True if the condition became true in time</returns>
        public static async Task<bool> UntilAsync(
            Func<bool> condition,
            TimeSpan interval,
            TimeSpan timeout,
            Func<TimeSpan, Task> delay,
            Func<TimeSpan> now)
        {
            var deadline = now() + timeout;
            while (true)
            {
                if (condition())
                {
                    return true;
                }

                var remaining = deadline - now();
                if (remaining <= TimeSpan.Zero)
                {
                    return false;
                }

                await delay(remaining < interval ? remaining : interval);
            }
        }
    }
}
