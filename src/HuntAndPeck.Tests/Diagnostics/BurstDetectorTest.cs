using System;
using HuntAndPeck.Diagnostics;
using Xunit;

namespace HuntAndPeck.Tests.Diagnostics
{
    public class BurstDetectorTest
    {
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Record_MoreThanMaxWithinWindow_IsBurst()
        {
            var detector = new BurstDetector(5, TimeSpan.FromSeconds(30));

            for (var i = 0; i < 5; ++i)
            {
                Assert.False(detector.Record(Start.AddSeconds(i)));
            }
            Assert.True(detector.Record(Start.AddSeconds(5)));
        }

        [Fact]
        public void Record_EventsOutsideWindow_DoNotCount()
        {
            var detector = new BurstDetector(5, TimeSpan.FromSeconds(30));

            for (var i = 0; i < 5; ++i)
            {
                Assert.False(detector.Record(Start.AddSeconds(i * 10)));
            }
            // Only the events at 30 s, 40 s and now (50 s) are within the last 30 s
            Assert.False(detector.Record(Start.AddSeconds(50)));
        }
    }
}
