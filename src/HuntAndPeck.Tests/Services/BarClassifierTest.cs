using System.Windows;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    /// <summary>
    /// Real windows from a 1920x1080 monitor at 150% scaling (physical pixels), plus edge cases
    /// </summary>
    public class BarClassifierTest
    {
        private static readonly Rect Monitor = new Rect(0, 0, 1920, 1080);
        private const uint ToolWindow = WindowEnumeration.WS_EX_TOOLWINDOW | 0x100;   // Zebar: 0x180
        private const uint Normal = 0x100;                                            // WS_EX_WINDOWEDGE

        [Fact]
        public void Zebar_TopToolWindow_IsTopBar()
        {
            Assert.Equal(BarEdge.Top, BarClassifier.Classify(Window("Tauri Window", 0, 0, 1920, 60, ToolWindow)));
        }

        [Fact]
        public void TopmostBottomBar_IsBottomBar()
        {
            Assert.Equal(BarEdge.Bottom, BarClassifier.Classify(Window("SomeBar", 0, 1008, 1920, 72, WindowEnumeration.WS_EX_TOPMOST)));
        }

        [Fact]
        public void VerticalBars_AreLeftAndRightBars()
        {
            Assert.Equal(BarEdge.Left, BarClassifier.Classify(Window("Dock", 0, 0, 64, 1080, ToolWindow)));
            Assert.Equal(BarEdge.Right, BarClassifier.Classify(Window("Dock", 1856, 100, 64, 800, ToolWindow)));
        }

        [Fact]
        public void BarOnSecondMonitor_IsBar()
        {
            var candidate = Window("SomeBar", 1920, -300, 2560, 48, ToolWindow);
            candidate.Monitor = new Rect(1920, -300, 2560, 1440);
            Assert.Equal(BarEdge.Top, BarClassifier.Classify(candidate));
        }

        [Fact]
        public void SlightGapToEdge_IsStillBar()
        {
            Assert.Equal(BarEdge.Top, BarClassifier.Classify(Window("SomeBar", 0, 6, 1920, 40, ToolWindow)));
        }

        [Fact]
        public void PrimaryTaskbarAndDesktop_AreNeverBars()
        {
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window(Taskbar.PrimaryTaskbarClassName, 0, 1008, 1920, 72, 0x88)));
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("WorkerW", 0, 1040, 1920, 40, ToolWindow)));
        }

        [Fact]
        public void ClickThroughTopmostClockWidget_IsNotBar()
        {
            // Catime: 209x37 at the top, WS_EX_LAYERED|WS_EX_TOOLWINDOW|WS_EX_TRANSPARENT|WS_EX_TOPMOST
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("CatimeWindowClass", 1082, 0, 209, 37, 0x000800A8)));
            // Same window, if it spanned the edge: still click-through
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("CatimeWindowClass", 0, 0, 1920, 37, 0x000800A8)));
        }

        [Fact]
        public void ShortWidget_AtEdge_IsNotBar()
        {
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Widget", 1082, 0, 900, 37, ToolWindow)));
        }

        [Fact]
        public void NormalAppWindow_IsNotBar()
        {
            // Thin and docked, but neither a tool window nor topmost
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Notepad", 0, 0, 1920, 60, Normal)));
            // Maximized
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Chrome_WidgetWin_1", 6, 69, 1908, 1011, WindowEnumeration.WS_EX_TOPMOST)));
        }

        [Fact]
        public void TooThinOrTooThick_IsNotBar()
        {
            // 1897x4 edge input window, a 15x15 helper
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("EdgeUiInputTopWndClass", 23, 0, 1897, 4, ToolWindow)));
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Helper", 0, 0, 15, 15, ToolWindow)));
            // Thicker than 15% of 1080 (162)
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Panel", 0, 0, 1920, 200, ToolWindow)));
        }

        [Fact]
        public void NotAtEdge_IsNotBar()
        {
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Strip", 0, 500, 1920, 40, ToolWindow)));
        }

        [Fact]
        public void HiddenCloakedMinimizedOrOwn_IsNotBar()
        {
            var hidden = Window("Tauri Window", 0, 0, 1920, 60, ToolWindow);
            hidden.Visible = false;
            var cloaked = Window("Tauri Window", 0, 0, 1920, 60, ToolWindow);
            cloaked.Cloaked = true;
            var minimized = Window("Tauri Window", 0, 0, 1920, 60, ToolWindow);
            minimized.Minimized = true;
            var own = Window("HwndWrapper", 0, 0, 1920, 60, ToolWindow);
            own.OwnProcess = true;

            Assert.Equal(BarEdge.None, BarClassifier.Classify(hidden));
            Assert.Equal(BarEdge.None, BarClassifier.Classify(cloaked));
            Assert.Equal(BarEdge.None, BarClassifier.Classify(minimized));
            Assert.Equal(BarEdge.None, BarClassifier.Classify(own));
        }

        [Fact]
        public void MostlyOffscreen_AutoHiddenBar_IsNotBar()
        {
            // An auto-hidden secondary taskbar: 2 pixels on screen
            Assert.Equal(BarEdge.None, BarClassifier.Classify(Window("Shell_SecondaryTrayWnd", 0, 1078, 1920, 72, 0x88)));
        }

        private static BarCandidate Window(string className, double x, double y, double width, double height, uint exStyle)
        {
            return new BarCandidate
            {
                ClassName = className,
                Bounds = new Rect(x, y, width, height),
                Monitor = Monitor,
                ExStyle = exStyle,
                Visible = true,
            };
        }

        [Theory]
        [InlineData(1.5, 15, true)]     // 15 px gap at 150%: tolerance 18 px
        [InlineData(1.5, 20, false)]
        [InlineData(1.0, 15, false)]    // tolerance 12 px at 100%
        [InlineData(1.0, 12, true)]
        [InlineData(0, 12, true)]       // unknown scale counts as 100%
        public void EdgeGapTolerance_ScalesWithMonitorDpi(double scale, double gap, bool isTopBar)
        {
            var candidate = Window("SomeBar", 0, gap, 1920, 60, ToolWindow);
            candidate.MonitorScale = scale;
            Assert.Equal(isTopBar ? BarEdge.Top : BarEdge.None, BarClassifier.Classify(candidate));
        }

        [Fact]
        public void SamplePoints_HorizontalBar_AlongItsLength()
        {
            var points = BarClassifier.SamplePoints(new Rect(0, 0, 1920, 60));
            Assert.Equal(new[] { new Point(192, 30), new Point(960, 30), new Point(1728, 30) }, points);
        }

        [Fact]
        public void SamplePoints_VerticalBar_AlongItsHeight()
        {
            var points = BarClassifier.SamplePoints(new Rect(1856, 100, 64, 800));
            Assert.Equal(new[] { new Point(1888, 180), new Point(1888, 500), new Point(1888, 820) }, points);
        }
    }
}