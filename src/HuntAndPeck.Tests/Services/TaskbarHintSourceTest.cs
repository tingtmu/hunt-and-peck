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
    public class TaskbarHintSourceTest
    {
        private static readonly IntPtr Taskbar = TaskbarRevealerTest.Taskbar;
        private static readonly IntPtr App = TaskbarRevealerTest.App;

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
            Assert.Equal(new[] { Taskbar }, provider.Enumerated);
        }

        [Fact]
        public async Task NotRevealed_SessionIsUnchanged()
        {
            var shell = new FakeTaskbarShell { AutoHide = false, Shown = true, Foreground = App };
            var provider = new FakeProvider { Result = NewSession() };

            var session = await CreateSource(shell, provider).EnumHintsAsync();

            Assert.Same(provider.Result, session);
            Assert.Equal(IntPtr.Zero, session.OverlayOwner);
            Assert.Equal(IntPtr.Zero, session.ForegroundToRestore);
        }

        [Fact]
        public async Task Revealed_NoSession_RestoresForeground()
        {
            var shell = new FakeTaskbarShell { AutoHide = true, Shown = true, Foreground = App };
            var provider = new FakeProvider { Result = null, OnEnumerate = () => shell.Foreground = Taskbar };

            var session = await CreateSource(shell, provider).EnumHintsAsync();

            Assert.Null(session);
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
        public async Task NoTaskbar_ReturnsNull_WithoutEnumerating()
        {
            var shell = new FakeTaskbarShell { AutoHide = true };
            var provider = new FakeProvider { Result = NewSession() };
            var source = new TaskbarHintSource(provider, TaskbarRevealerTest.CreateRevealer(shell, new FakeClock()), () => IntPtr.Zero);

            Assert.Null(await source.EnumHintsAsync());
            Assert.Empty(provider.Enumerated);
            Assert.Empty(shell.Activated);
        }

        private static HintSession NewSession()
        {
            return new HintSession { Hints = new List<Hint>(), OwningWindow = Taskbar, OwningWindowBounds = new Rect(0, 1008, 1920, 72) };
        }

        private static TaskbarHintSource CreateSource(FakeTaskbarShell shell, FakeProvider provider)
        {
            return new TaskbarHintSource(provider, TaskbarRevealerTest.CreateRevealer(shell, new FakeClock()), () => Taskbar);
        }

        private sealed class FakeProvider : IHintProviderService
        {
            public HintSession Result { get; set; }

            public Exception Error { get; set; }

            public Action OnEnumerate { get; set; }

            public List<IntPtr> Enumerated { get; } = new List<IntPtr>();

            public Task<HintSession> EnumHintsAsync() => throw new NotSupportedException();

            public async Task<HintSession> EnumHintsAsync(IntPtr handle)
            {
                Enumerated.Add(handle);
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
