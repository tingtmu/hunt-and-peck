using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HuntAndPeck.Tests.Services
{
    /// <summary>
    /// Manual clock whose delay completes at once after advancing the time by the requested amount
    /// </summary>
    internal sealed class FakeClock
    {
        public TimeSpan Now { get; private set; }

        public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

        /// <summary>Called after each delay with the new time, e.g. to change what is polled</summary>
        public Action<TimeSpan> OnAdvanced { get; set; }

        /// <summary>Moves the time on without recording a delay (e.g. time spent working)</summary>
        public void Advance(TimeSpan duration)
        {
            Now += duration;
        }

        public Task Delay(TimeSpan duration)
        {
            Delays.Add(duration);
            Now += duration;
            OnAdvanced?.Invoke(Now);
            return Task.CompletedTask;
        }
    }
}
