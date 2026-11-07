using System;
using HuntAndPeck.Updates;
using Xunit;

namespace HuntAndPeck.Tests.Updates
{
    public class UpdateScheduleTest
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void IsDue_NeverChecked()
        {
            Assert.True(UpdateSchedule.IsDue(null, Now));
        }

        [Theory]
        [InlineData(1, false)]
        [InlineData(23, false)]
        [InlineData(24, true)]
        [InlineData(25, true)]
        [InlineData(24 * 30, true)]
        public void IsDue_AfterAFullDay(int hoursAgo, bool expected)
        {
            Assert.Equal(expected, UpdateSchedule.IsDue(Now.AddHours(-hoursAgo), Now));
        }

        [Fact]
        public void IsDue_LastCheckInTheFuture_IsDue()
        {
            Assert.True(UpdateSchedule.IsDue(Now.AddDays(3), Now));
        }

        [Fact]
        public void FormatThenTryParse_RoundTrips()
        {
            DateTime? parsed;
            string error;

            Assert.True(UpdateSchedule.TryParse(UpdateSchedule.Format(Now), out parsed, out error));

            Assert.Equal(Now, parsed);
            Assert.Equal(DateTimeKind.Utc, parsed.Value.Kind);
            Assert.Null(error);
        }

        [Fact]
        public void Format_ConvertsLocalTimeToUtc()
        {
            Assert.EndsWith("Z", UpdateSchedule.Format(Now.ToLocalTime()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void TryParse_Empty_MeansNeverChecked(string text)
        {
            DateTime? parsed;
            string error;

            Assert.True(UpdateSchedule.TryParse(text, out parsed, out error));
            Assert.Null(parsed);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("yesterday")]
        [InlineData("2026-13-45")]
        [InlineData("12345")]
        public void TryParse_Invalid_IsNeverCheckedWithAnError(string text)
        {
            DateTime? parsed;
            string error;

            Assert.False(UpdateSchedule.TryParse(text, out parsed, out error));
            Assert.Null(parsed);
            Assert.NotNull(error);
        }
    }
}
