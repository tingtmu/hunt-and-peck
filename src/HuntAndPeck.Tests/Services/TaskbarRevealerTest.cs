using System;
using System.Threading.Tasks;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class TaskbarRevealerTest
    {
        internal static readonly IntPtr Taskbar = new IntPtr(0x10);
        internal static readonly IntPtr App = new IntPtr(0x20);
        private static readonly IntPtr Other = new IntPtr(0x30);

        [Fact]
        public async Task AlwaysVisibleTaskbar_IsLeftAlone()
        {
            var shell = new FakeTaskbarShell { AutoHide = false, Shown = true };
            var clock = new FakeClock();

            var reveal = await CreateRevealer(shell, clock).RevealAsync(Taskbar);

            Assert.False(reveal.Revealed);
            Assert.Empty(shell.Activated);
            Assert.Empty(clock.Delays);
        }

        [Fact]
        public async Task AutoHiddenTaskbar_IsActivated_AndAwaitedUntilShown()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = false, Foreground = App };
            var clock = new FakeClock { OnAdvanced = now => shell.Shown = now >= TimeSpan.FromMilliseconds(200) };

            var reveal = await CreateRevealer(shell, clock).RevealAsync(Taskbar);

            Assert.True(reveal.Revealed);
            Assert.Equal(App, reveal.PreviousForeground);
            Assert.Equal(new[] { Taskbar }, shell.Activated);
            Assert.InRange(clock.Now, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(216));
            Assert.Equal(1, shell.ShownBoundsQueries);
        }

        [Fact]
        public async Task AutoHideTaskbarAlreadyShown_IsStillActivated_SoItStaysShown()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = App };
            var clock = new FakeClock();

            var reveal = await CreateRevealer(shell, clock).RevealAsync(Taskbar);

            Assert.True(reveal.Revealed);
            Assert.Equal(new[] { Taskbar }, shell.Activated);
            Assert.Empty(clock.Delays);
        }

        [Fact]
        public async Task TaskbarAlreadyForeground_HasNothingToRestore()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = Taskbar };

            var reveal = await CreateRevealer(shell, new FakeClock()).RevealAsync(Taskbar);

            Assert.True(reveal.Revealed);
            Assert.Equal(IntPtr.Zero, reveal.PreviousForeground);
        }

        [Fact]
        public async Task TaskbarThatNeverShows_TimesOut_AndFallsBack()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = false, Foreground = App };
            var clock = new FakeClock();

            var reveal = await CreateRevealer(shell, clock).RevealAsync(Taskbar);

            Assert.False(reveal.Revealed);
            Assert.Equal(TaskbarRevealer.RevealTimeout, clock.Now);
        }

        [Fact]
        public async Task HungTaskbar_IsNotActivated()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Hung = true, Foreground = App };

            var reveal = await CreateRevealer(shell, new FakeClock()).RevealAsync(Taskbar);

            Assert.False(reveal.Revealed);
            Assert.Empty(shell.Activated);
        }

        [Fact]
        public async Task UnknownShownPosition_IsNotActivated()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, ShownBoundsKnown = false, Foreground = App };

            var reveal = await CreateRevealer(shell, new FakeClock()).RevealAsync(Taskbar);

            Assert.False(reveal.Revealed);
            Assert.Empty(shell.Activated);
        }

        [Fact]
        public async Task FailingDelay_IsReportedAsNotRevealed()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = false, Foreground = App };
            var revealer = new TaskbarRevealer(shell, duration => { throw new InvalidOperationException("boom"); }, () => TimeSpan.Zero);

            var reveal = await revealer.RevealAsync(Taskbar);

            Assert.False(reveal.Revealed);
        }

        [Fact]
        public void Restore_AfterEscape_ReactivatesPreviousWindow()
        {
            var shell = new FakeTaskbarShell { Foreground = Taskbar };

            CreateRevealer(shell, new FakeClock()).RestoreForeground(RevealedSession(), false);

            Assert.Equal(new[] { App }, shell.Activated);
        }

        [Fact]
        public void Restore_WithNoForegroundWindow_ReactivatesPreviousWindow()
        {
            var shell = new FakeTaskbarShell { Foreground = IntPtr.Zero };

            CreateRevealer(shell, new FakeClock()).RestoreForeground(RevealedSession(), false);

            Assert.Equal(new[] { App }, shell.Activated);
        }

        [Fact]
        public void Restore_AfterHintInvoked_DoesNothing()
        {
            var shell = new FakeTaskbarShell { Foreground = Taskbar };

            CreateRevealer(shell, new FakeClock()).RestoreForeground(RevealedSession(), true);

            Assert.Empty(shell.Activated);
        }

        [Fact]
        public void Restore_WhenAnotherWindowTookForeground_DoesNothing()
        {
            var shell = new FakeTaskbarShell { Foreground = Other };

            CreateRevealer(shell, new FakeClock()).RestoreForeground(RevealedSession(), false);

            Assert.Empty(shell.Activated);
        }

        [Fact]
        public void Restore_ForUnrevealedSession_DoesNothing()
        {
            var shell = new FakeTaskbarShell { Foreground = Taskbar };

            CreateRevealer(shell, new FakeClock()).RestoreForeground(new HintSession { OwningWindow = Taskbar }, false);

            Assert.Empty(shell.Activated);
        }

        private static HintSession RevealedSession()
        {
            return new HintSession { OwningWindow = Taskbar }.WithOverlayOwner(Taskbar, App);
        }

        internal static TaskbarRevealer CreateRevealer(FakeTaskbarShell shell, FakeClock clock)
        {
            return new TaskbarRevealer(shell, clock.Delay, () => clock.Now);
        }
    }
}
