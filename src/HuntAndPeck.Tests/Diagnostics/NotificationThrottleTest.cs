using System;
using HuntAndPeck.Diagnostics;
using Xunit;

namespace HuntAndPeck.Tests.Diagnostics
{
    public class NotificationThrottleTest
    {
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void TryAcquire_AllowsOneNotificationPerInterval()
        {
            var throttle = new NotificationThrottle(TimeSpan.FromMinutes(10));

            Assert.True(throttle.TryAcquire(Start));
            Assert.False(throttle.TryAcquire(Start.AddMinutes(1)));
            Assert.False(throttle.TryAcquire(Start.AddMinutes(9.9)));
            Assert.True(throttle.TryAcquire(Start.AddMinutes(10)));
            Assert.False(throttle.TryAcquire(Start.AddMinutes(15)));
        }
    }
}
