using System;
using System.Linq;
using System.Threading.Tasks;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class PollingWaitTest
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(16);
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(600);

        [Fact]
        public async Task AlreadyTrue_ReturnsWithoutWaiting()
        {
            var clock = new FakeClock();
            var result = await PollingWait.UntilAsync(() => true, Interval, Timeout, clock.Delay, () => clock.Now);

            Assert.True(result);
            Assert.Empty(clock.Delays);
        }

        [Fact]
        public async Task BecomesTrue_ReturnsAtFirstPollAfterwards()
        {
            var clock = new FakeClock();
            var result = await PollingWait.UntilAsync(() => clock.Now >= TimeSpan.FromMilliseconds(250), Interval, Timeout, clock.Delay, () => clock.Now);

            Assert.True(result);
            Assert.Equal(16, clock.Delays.Count);
            Assert.All(clock.Delays, x => Assert.Equal(Interval, x));
        }

        [Fact]
        public async Task NeverTrue_GivesUpAtTimeout_WithoutOvershooting()
        {
            var clock = new FakeClock();
            var polls = 0;
            var result = await PollingWait.UntilAsync(() => { polls++; return false; }, Interval, Timeout, clock.Delay, () => clock.Now);

            Assert.False(result);
            Assert.Equal(Timeout, clock.Now);
            // Whole intervals, then one shortened delay that ends exactly at the timeout
            var wholeIntervals = Timeout.Ticks / Interval.Ticks;
            Assert.Equal(wholeIntervals + 1, clock.Delays.Count);
            Assert.Equal(Timeout - TimeSpan.FromTicks(Interval.Ticks * wholeIntervals), clock.Delays.Last());
            Assert.Equal(clock.Delays.Count + 1, polls);
        }
    }
}
