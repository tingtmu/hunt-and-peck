using System.Windows;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class HintBoundsTest
    {
        private static readonly Rect Window = new Rect(1000, 600, 800, 600);

        [Fact]
        public void InsideWindow_ConvertsToWindowCoordinates()
        {
            Rect result;

            Assert.True(HintBounds.TryToWindowCoordinates(1100, 650, 1180, 674, Window, out result));
            Assert.Equal(new Rect(100, 50, 80, 24), result);
        }

        [Fact]
        public void PartiallyOverlapping_KeepsFullRect()
        {
            Rect result;

            Assert.True(HintBounds.TryToWindowCoordinates(980, 590, 1030, 610, Window, out result));
            Assert.Equal(new Rect(-20, -10, 50, 20), result);
        }

        [Theory]
        [InlineData(1100, 650, 1100, 674)]  // zero width
        [InlineData(1100, 650, 1180, 650)]  // zero height
        [InlineData(1180, 650, 1100, 674)]  // inverted
        [InlineData(0, 0, 0, 0)]            // UIA's empty rectangle
        [InlineData(950, 650, 1000, 674)]   // touching the left edge only
        [InlineData(3000, 3000, 3050, 3020)] // far outside
        public void EmptyOrOutside_ReturnsFalse(int left, int top, int right, int bottom)
        {
            Rect result;

            Assert.False(HintBounds.TryToWindowCoordinates(left, top, right, bottom, Window, out result));
            Assert.Equal(Rect.Empty, result);
        }
    }
}
