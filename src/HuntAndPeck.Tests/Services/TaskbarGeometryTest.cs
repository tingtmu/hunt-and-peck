using System.Windows;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class TaskbarGeometryTest
    {
        // Windows 11 at 1920x1080: the shown taskbar, and the auto-hidden one with a 2 pixel strip on screen
        private static readonly Rect Shown = new Rect(0, 1008, 1920, 72);
        private static readonly Rect Hidden = new Rect(0, 1078, 1920, 72);
        private static readonly Rect Monitor = new Rect(0, 0, 1920, 1080);

        [Fact]
        public void ShownTaskbar_IsShown_AgainstItsPositionAndItsMonitor()
        {
            Assert.Equal(1.0, TaskbarGeometry.VisibleFraction(Shown, Shown), 6);
            Assert.True(TaskbarGeometry.IsShown(Shown, Shown));
            Assert.True(TaskbarGeometry.IsShown(Shown, Monitor));
        }

        [Fact]
        public void AutoHiddenTaskbar_IsNotShown()
        {
            Assert.Equal(2.0 / 72, TaskbarGeometry.VisibleFraction(Hidden, Monitor), 6);
            Assert.False(TaskbarGeometry.IsShown(Hidden, Shown));
            Assert.False(TaskbarGeometry.IsShown(Hidden, Monitor));
        }

        [Fact]
        public void HalfwayThroughSlide_IsNotShown()
        {
            var sliding = new Rect(0, 1044, 1920, 72);
            Assert.Equal(0.5, TaskbarGeometry.VisibleFraction(sliding, Monitor), 6);
            Assert.False(TaskbarGeometry.IsShown(sliding, Monitor));
        }

        [Fact]
        public void NearlyShown_WithinTolerance_IsShown()
        {
            var almost = new Rect(0, 1010, 1920, 72);
            Assert.True(TaskbarGeometry.IsShown(almost, Monitor));
        }

        [Fact]
        public void TopTaskbarOnSecondMonitor_HiddenAboveIt()
        {
            var monitor = new Rect(1920, -200, 2560, 1440);
            Assert.False(TaskbarGeometry.IsShown(new Rect(1920, -270, 2560, 72), monitor));
            Assert.True(TaskbarGeometry.IsShown(new Rect(1920, -200, 2560, 72), monitor));
        }

        [Fact]
        public void EmptyOrDisjointRects_HaveNoVisibleShare()
        {
            Assert.Equal(0, TaskbarGeometry.VisibleFraction(Rect.Empty, Monitor));
            Assert.Equal(0, TaskbarGeometry.VisibleFraction(Shown, Rect.Empty));
            Assert.Equal(0, TaskbarGeometry.VisibleFraction(new Rect(0, 1008, 0, 72), Monitor));
            Assert.Equal(0, TaskbarGeometry.VisibleFraction(new Rect(0, 2000, 1920, 72), Monitor));
        }
    }
}
