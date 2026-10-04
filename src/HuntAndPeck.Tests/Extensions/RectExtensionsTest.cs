using System.Windows;
using HuntAndPeck.Extensions;
using Xunit;

namespace HuntAndPeck.Tests.Extensions
{
    public class RectExtensionsTest
    {
        [Fact]
        public void ScreenToWindowCoordinates_OffsetsByWindowOrigin()
        {
            var element = new Rect(1100, 650, 80, 24);
            var window = new Rect(1000, 600, 800, 600);

            Assert.Equal(new Rect(100, 50, 80, 24), element.ScreenToWindowCoordinates(window));
        }

        [Fact]
        public void ScreenToWindowCoordinates_WindowOnMonitorWithNegativeOrigin()
        {
            var element = new Rect(-2800, -250, 120, 30);
            var window = new Rect(-2880, -300, 1440, 900);

            Assert.Equal(new Rect(80, 50, 120, 30), element.ScreenToWindowCoordinates(window));
        }

        [Theory]
        [InlineData(100, 100, 50, 20, true)]     // fully inside
        [InlineData(-20, -10, 50, 20, true)]     // partially overlapping top-left corner
        [InlineData(780, 590, 50, 20, true)]     // partially overlapping bottom-right corner
        [InlineData(-50, 100, 50, 20, false)]    // touching left edge only
        [InlineData(800, 100, 50, 20, false)]    // touching right edge only
        [InlineData(100, 600, 50, 20, false)]    // touching bottom edge only
        [InlineData(2000, 2000, 50, 20, false)]  // far outside
        [InlineData(-3000, 100, 50, 20, false)]  // on another monitor
        public void OverlapsWith_WindowBounds(double x, double y, double width, double height, bool expected)
        {
            var window = new Rect(0, 0, 800, 600);

            Assert.Equal(expected, new Rect(x, y, width, height).OverlapsWith(window));
        }

        [Fact]
        public void OverlapsWith_EmptyRect_IsFalse()
        {
            var window = new Rect(0, 0, 800, 600);

            Assert.False(Rect.Empty.OverlapsWith(window));
            Assert.False(window.OverlapsWith(Rect.Empty));
        }
    }
}
