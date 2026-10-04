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

        [Fact]
        public void OverlayCoordinates_AreRelativeToOverlay_ClippedByWindow()
        {
            // A top bar inside a full monitor overlay
            var bar = new Rect(0, 0, 1920, 60);
            var overlay = new Rect(0, 0, 1920, 1080);
            Rect result;

            Assert.True(HintBounds.TryToOverlayCoordinates(91, 9, 142, 51, bar, overlay, out result));
            Assert.Equal(new Rect(91, 9, 51, 42), result);

            // Taskbar button, overlay starting above it
            Assert.True(HintBounds.TryToOverlayCoordinates(149, 1008, 215, 1080, new Rect(0, 1008, 1920, 72), overlay, out result));
            Assert.Equal(new Rect(149, 1008, 66, 72), result);

            // Inside the overlay but outside its window: dropped
            Assert.False(HintBounds.TryToOverlayCoordinates(100, 500, 150, 540, bar, overlay, out result));
        }

        [Theory]
        [InlineData(0, 0, 1921, 61, true)]     // WebView2 page-level click handler
        [InlineData(0, 0, 1920, 56, true)]     // 93% of the window
        [InlineData(91, 9, 142, 51, false)]    // a workspace button
        [InlineData(0, 0, 960, 60, false)]     // half the window
        [InlineData(0, 0, 0, 0, false)]
        public void CoversWindow(double left, double top, double right, double bottom, bool expected)
        {
            var element = new Rect(new Point(left, top), new Point(right, bottom));
            Assert.Equal(expected, HintBounds.CoversWindow(element, new Rect(0, 0, 1920, 60)));
        }

        [Fact]
        public void ClipToMonitor_KeepsOnScreenPart()
        {
            var monitor = new Rect(0, 0, 1920, 1080);
            // An auto-hide taskbar that did not show: 2 pixels on screen
            Assert.Equal(new Rect(0, 1078, 1920, 2), HintBounds.ClipToMonitor(new Rect(0, 1078, 1920, 72), monitor));
            Assert.Equal(new Rect(0, 0, 1920, 60), HintBounds.ClipToMonitor(new Rect(0, 0, 1920, 60), monitor));
            // On another monitor, or only touching this one
            Assert.Equal(Rect.Empty, HintBounds.ClipToMonitor(new Rect(1920, 0, 2560, 48), monitor));
            Assert.Equal(Rect.Empty, HintBounds.ClipToMonitor(new Rect(0, 1080, 1920, 72), monitor));
        }

        [Fact]
        public void UnionOf_CoversAllWindows_IgnoringEmpty()
        {
            var union = HintBounds.UnionOf(new[] { new Rect(0, 0, 1920, 60), Rect.Empty, new Rect(0, 1008, 1920, 72) });
            Assert.Equal(new Rect(0, 0, 1920, 1080), union);
            Assert.Equal(new Rect(100, 100, 10, 10), HintBounds.UnionOf(new[] { new Rect(100, 100, 10, 10) }));
            Assert.Equal(Rect.Empty, HintBounds.UnionOf(new Rect[0]));
        }
    }
}