using System.Windows;
using HuntAndPeck.Views;
using Xunit;

namespace HuntAndPeck.Tests.Views
{
    public class OverlayPlacementTest
    {
        private static readonly Rect Monitor = new Rect(0, 0, 1920, 1080);

        [Fact]
        public void OverlayCoveringMonitor_IsOnePixelShorter_SameTopLeft()
        {
            // Bars mode: Zebar at the top plus the taskbar at the bottom
            Assert.Equal(new Rect(0, 0, 1920, 1079), OverlayPlacement.AvoidCoveringMonitor(Monitor, Monitor));
        }

        [Fact]
        public void OverlayLargerThanMonitor_IsOnePixelShorter()
        {
            Assert.Equal(new Rect(-8, -8, 1936, 1095), OverlayPlacement.AvoidCoveringMonitor(new Rect(-8, -8, 1936, 1096), Monitor));
        }

        [Theory]
        [InlineData(0, 1008, 1920, 72)]    // taskbar only
        [InlineData(0, 0, 1920, 60)]       // a top bar only
        [InlineData(0, 0, 1920, 1079)]     // already short of the monitor
        [InlineData(100, 100, 800, 600)]   // a normal window
        public void OverlayNotCoveringMonitor_IsUnchanged(double x, double y, double width, double height)
        {
            var overlay = new Rect(x, y, width, height);
            Assert.Equal(overlay, OverlayPlacement.AvoidCoveringMonitor(overlay, Monitor));
        }

        [Fact]
        public void UnknownMonitor_IsUnchanged()
        {
            Assert.Equal(Monitor, OverlayPlacement.AvoidCoveringMonitor(Monitor, Rect.Empty));
        }

        [Fact]
        public void SecondMonitor_CoveredExactly_IsOnePixelShorter()
        {
            var second = new Rect(1920, -300, 2560, 1440);
            Assert.Equal(new Rect(1920, -300, 2560, 1439), OverlayPlacement.AvoidCoveringMonitor(second, second));
        }
    }
}
