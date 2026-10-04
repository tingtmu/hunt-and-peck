using System.Globalization;
using System.Threading;
using HuntAndPeck.Configuration;
using Xunit;

namespace HuntAndPeck.Tests.Configuration
{
    public class FontSizeSettingTest
    {
        [Theory]
        [InlineData("14", 14)]
        [InlineData(" 8 ", 8)]
        [InlineData("10.5", 10.5)]
        [InlineData("6", 6)]
        [InlineData("72", 72)]
        public void TryParse_Valid(string text, double expected)
        {
            double size;
            string error;

            Assert.True(FontSizeSetting.TryParse(text, out size, out error), error);
            Assert.Equal(expected, size);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("5.9")]
        [InlineData("72.5")]
        [InlineData("-14")]
        [InlineData("10,5")]
        [InlineData("1e1")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        public void TryParse_Invalid_FallsBackToDefault(string text)
        {
            double size;
            string error;

            Assert.False(FontSizeSetting.TryParse(text, out size, out error));
            Assert.Equal(FontSizeSetting.Default, size);
            Assert.NotNull(error);
        }

        [Fact]
        public void ParseAndFormat_IgnoreCurrentCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                double size;
                string error;

                Assert.True(FontSizeSetting.TryParse("10.5", out size, out error));
                Assert.Equal("10.5", FontSizeSetting.Format(size));
                Assert.Equal("14", FontSizeSetting.Format(14));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
