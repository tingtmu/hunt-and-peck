using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class BarsHintSourceTest
    {
        private static readonly IntPtr Taskbar = TaskbarRevealerTest.Taskbar;
        private static readonly IntPtr App = TaskbarRevealerTest.App;
        private static readonly IntPtr Bar = new IntPtr(0x40);
        private static readonly Rect Monitor = new Rect(0, 0, 1920, 1080);

        [Fact]
        public async Task Revealed_SessionIsOwnedByTaskbar_AndRemembersPreviousForeground()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = App };
            var provider = new FakeProvider { Result = NewSession() };

            var session = await CreateSource(shell, provider).EnumHintsAsync();

            Assert.Equal(Taskbar, session.OverlayOwner);
            Assert.Equal(App, session.ForegroundToRestore);
            Assert.Same(provider.Result.Hints, session.Hints);
            Assert.Equal(provider.Result.OwningWindowBounds, session.OwningWindowBounds);
        }

        [Fact]
        public async Task NotRevealed_SessionIsUnchanged()
        {
            var shell = new FakeTaskbarShell { AutoHide = false, Shown = true, Foreground = App };
            var provider = new FakeProvider { Result = NewSession() };

            var session = await CreateSource(shell, provider).EnumHintsAsync();

            Assert.Same(provider.Result, session);
            Assert.Equal(IntPtr.Zero, session.OverlayOwner);
        }

        [Fact]
        public async Task TaskbarAndBars_AreEnumeratedTogether_TaskbarFirst()
        {
            var shell = new FakeTaskbarShell { AutoHide = false, Shown = true };
            var provider = new FakeProvider { Result = NewSession() };

            await CreateSource(shell, provider, Taskbar, Bar, Taskbar).EnumHintsAsync();

            Assert.Equal(new[] { Taskbar, Bar }, Assert.Single(provider.Enumerated));
            Assert.Equal(Monitor, Assert.Single(provider.Monitors));
        }

        [Fact]
        public async Task NoBars_EnumeratesTaskbarOnly()
        {
            var shell = new FakeTaskbarShell { AutoHide = false, Shown = true };
            var provider = new FakeProvider { Result = NewSession() };

            await CreateSource(shell, provider, Taskbar).EnumHintsAsync();

            Assert.Equal(new[] { Taskbar }, Assert.Single(provider.Enumerated));
        }

        [Fact]
        public async Task NoTaskbar_EnumeratesBarsWithoutRevealing()
        {
            var shell = new FakeTaskbarShell { AutoHide = true };
            var provider = new FakeProvider { Result = NewSession() };

            var session = await CreateSource(shell, provider, IntPtr.Zero, Bar).EnumHintsAsync();

            Assert.NotNull(session);
            Assert.Equal(new[] { Bar }, Assert.Single(provider.Enumerated));
            Assert.Empty(shell.Activated);
        }

        [Fact]
        public async Task NothingFound_ReturnsNull_WithoutEnumerating()
        {
            var shell = new FakeTaskbarShell { AutoHide = true };
            var provider = new FakeProvider { Result = NewSession() };

            Assert.Null(await CreateSource(shell, provider, IntPtr.Zero).EnumHintsAsync());
            Assert.Empty(provider.Enumerated);
        }

        [Fact]
        public async Task Revealed_NoSession_RestoresForeground()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = App };
            var provider = new FakeProvider { Result = null, OnEnumerate = () => shell.Foreground = Taskbar };

            Assert.Null(await CreateSource(shell, provider).EnumHintsAsync());
            Assert.Equal(new[] { Taskbar, App }, shell.Activated);
        }

        [Fact]
        public async Task Revealed_NoHints_ReturnsNull_AndRestoresForeground()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = App };
            var provider = new FakeProvider { Result = NewSession(0), OnEnumerate = () => shell.Foreground = Taskbar };

            Assert.Null(await CreateSource(shell, provider).EnumHintsAsync());
            Assert.Equal(new[] { Taskbar, App }, shell.Activated);
        }

        [Fact]
        public async Task Revealed_EnumerationThrows_RestoresForeground_AndRethrows()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = App };
            var provider = new FakeProvider { Error = new InvalidOperationException("boom"), OnEnumerate = () => shell.Foreground = Taskbar };

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSource(shell, provider).EnumHintsAsync());

            Assert.Equal(new[] { Taskbar, App }, shell.Activated);
        }

        [Fact]
        public async Task UnknownMonitor_ReturnsNull_WithoutRevealing()
        {
            var shell = new FakeTaskbarShell { AutoHide = true };
            var provider = new FakeProvider { Result = NewSession() };
            var source = new BarsHintSource(provider, TaskbarRevealerTest.CreateRevealer(shell, new FakeClock()), () => null, s => new[] { Bar });

            Assert.Null(await source.EnumHintsAsync());
            Assert.Empty(provider.Enumerated);
            Assert.Empty(shell.Activated);
        }

        [Fact]
        public void CollectWindows_CapsAtMaxWindows_KeepingTaskbarFirst()
        {
            var bars = new List<IntPtr>();
            for (var i = 1; i <= 12; i++)
            {
                bars.Add(new IntPtr(0x100 + i));
            }

            var windows = BarsHintSource.CollectWindows(Taskbar, bars);

            Assert.Equal(BarsHintSource.MaxWindows, windows.Count);
            Assert.Equal(Taskbar, windows[0]);
        }

        [Fact]
        public void CollectWindows_SkipsZeroAndDuplicates()
        {
            Assert.Equal(new[] { Taskbar, Bar }, BarsHintSource.CollectWindows(Taskbar, new[] { IntPtr.Zero, Bar, Taskbar, Bar }));
            Assert.Equal(new[] { Bar }, BarsHintSource.CollectWindows(IntPtr.Zero, new[] { Bar }));
        }

        private static HintSession NewSession(int hintCount = 1)
        {
            var hints = new List<Hint>();
            for (var i = 0; i < hintCount; i++)
            {
                hints.Add(new FakeHint());
            }
            return new HintSession { Hints = hints, OwningWindow = Taskbar, OwningWindowBounds = new Rect(0, 1008, 1920, 72) };
        }

        private static BarsHintSource CreateSource(FakeTaskbarShell shell, FakeProvider provider, IntPtr? taskbar = null, params IntPtr[] bars)
        {
            var foundTaskbar = taskbar ?? Taskbar;
            var scope = new BarsScope(new IntPtr(0x99), Monitor, 1.5, foundTaskbar);
            return new BarsHintSource(provider, TaskbarRevealerTest.CreateRevealer(shell, new FakeClock()), () => scope, s => bars);
        }

        private sealed class FakeHint : Hint
        {
            public FakeHint()
                : base(IntPtr.Zero, new Rect(0, 0, 10, 10))
            {
            }

            public override void Invoke()
            {
            }
        }

        private sealed class FakeProvider : IHintProviderService
        {
            public HintSession Result { get; set; }

            public Exception Error { get; set; }

            public Action OnEnumerate { get; set; }

            public List<IntPtr[]> Enumerated { get; } = new List<IntPtr[]>();

            public Task<HintSession> EnumHintsAsync() => throw new NotSupportedException();

            public Task<HintSession> EnumHintsAsync(IntPtr handle) => throw new NotSupportedException();

            public List<Rect> Monitors { get; } = new List<Rect>();

            public async Task<HintSession> EnumBarHintsAsync(IReadOnlyList<IntPtr> windows, Rect monitor)
            {
                Enumerated.Add(new List<IntPtr>(windows).ToArray());
                Monitors.Add(monitor);
                OnEnumerate?.Invoke();
                await Task.Yield();
                if (Error != null)
                {
                    throw Error;
                }
                return Result;
            }

            public Task InvokeHintAsync(Hint hint) => Task.CompletedTask;
        }
    }
}
