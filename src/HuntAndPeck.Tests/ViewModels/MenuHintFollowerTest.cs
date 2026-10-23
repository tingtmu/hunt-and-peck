using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;
using HuntAndPeck.Tests.Services;
using HuntAndPeck.ViewModels;
using Xunit;

namespace HuntAndPeck.Tests.ViewModels
{
    public class MenuHintFollowerTest
    {
        private static readonly IntPtr Owner = new IntPtr(1);
        private static readonly IntPtr Menu = new IntPtr(5);

        [Fact]
        public void MenuOpenedByTheHint_GetsTheMenuOverlay()
        {
            var setup = new Setup();
            setup.MenuAppearsAfterInvoke();

            setup.Follower.FollowAsync(setup.Session, setup.InvokedOverlay(), setup.Before, CancellationToken.None).Wait();

            var shown = Assert.Single(setup.Shown);
            Assert.Equal(new[] { Menu }, shown);
            Assert.Equal(new[] { "begin", "end" }, setup.SessionEvents);
        }

        [Fact]
        public void NoHintInvoked_DoesNotWatch()
        {
            var setup = new Setup();
            setup.MenuAppearsAfterInvoke();
            var overlay = new OverlayViewModel(setup.Session, new HintLabelService(), hint => Task.FromResult(true));

            setup.Follower.FollowAsync(setup.Session, overlay, setup.Before, CancellationToken.None).Wait();

            Assert.Empty(setup.Provider.Enumerated);
            Assert.Empty(setup.Shown);
        }

        [Fact]
        public void NoMenu_ShowsNothing()
        {
            var setup = new Setup();

            setup.Follower.FollowAsync(setup.Session, setup.InvokedOverlay(), setup.Before, CancellationToken.None).Wait();

            Assert.Empty(setup.Provider.Enumerated);
            Assert.Empty(setup.Shown);
        }

        [Fact]
        public void UserMovedOn_ShowsNothing()
        {
            var setup = new Setup();
            setup.MenuAppearsAfterInvoke();
            var following = new CancellationTokenSource();
            following.Cancel();

            setup.Follower.FollowAsync(setup.Session, setup.InvokedOverlay(), setup.Before, following.Token).Wait();

            Assert.Empty(setup.Shown);
        }

        [Fact]
        public void AnotherSessionActive_ShowsNothing()
        {
            var setup = new Setup { SessionFree = false };
            setup.MenuAppearsAfterInvoke();

            setup.Follower.FollowAsync(setup.Session, setup.InvokedOverlay(), setup.Before, CancellationToken.None).Wait();

            Assert.Empty(setup.Shown);
            Assert.Empty(setup.SessionEvents);
        }

        private sealed class Setup
        {
            private readonly FakeWindows _windows = new FakeWindows();
            private readonly FakeClock _clock = new FakeClock();

            public Setup()
            {
                var watcher = new MenuPopupWatcher(_windows, _clock.Delay, () => _clock.Now);
                Before = watcher.Snapshot();
                Follower = new MenuHintFollower(
                    watcher,
                    Provider,
                    session => new OverlayViewModel(session, new HintLabelService(), hint => Task.FromResult(true)),
                    (vm, menus) => Shown.Add(menus),
                    _clock.Delay,
                    token =>
                    {
                        if (!SessionFree || token.IsCancellationRequested)
                        {
                            return false;
                        }
                        SessionEvents.Add("begin");
                        return true;
                    },
                    () => SessionEvents.Add("end"));
            }

            public HintSession Session { get; } = new HintSession { Hints = new List<Hint> { new FakeHint() }, OwningWindow = Owner };
            public HashSet<IntPtr> Before { get; }
            public MenuHintFollower Follower { get; }
            public FakeProvider Provider { get; } = new FakeProvider();
            public List<IReadOnlyList<IntPtr>> Shown { get; } = new List<IReadOnlyList<IntPtr>>();
            public List<string> SessionEvents { get; } = new List<string>();
            public bool SessionFree { get; set; } = true;

            public void MenuAppearsAfterInvoke()
            {
                _windows.Add(new TopLevelWindow(Menu, 10, "#32768", true));
            }

            /// <summary>An overlay whose single hint was chosen</summary>
            public OverlayViewModel InvokedOverlay()
            {
                var overlay = new OverlayViewModel(Session, new HintLabelService(), hint => Task.FromResult(true));
                overlay.MatchString = overlay.Hints[0].Label;
                Assert.True(overlay.HintInvoked);
                return overlay;
            }
        }

        private sealed class FakeWindows : ITopLevelWindowSource
        {
            private readonly List<TopLevelWindow> _windows = new List<TopLevelWindow> { new TopLevelWindow(Owner, 10, "App", false) };

            public void Add(TopLevelWindow window) => _windows.Add(window);

            public IReadOnlyList<TopLevelWindow> VisibleWindows() => new List<TopLevelWindow>(_windows);

            public uint ProcessOf(IntPtr hWnd) => 10;
        }

        private sealed class FakeProvider : IHintProviderService
        {
            public List<IReadOnlyList<IntPtr>> Enumerated { get; } = new List<IReadOnlyList<IntPtr>>();

            public Task<HintSession> EnumPopupHintsAsync(IReadOnlyList<IntPtr> windows)
            {
                Enumerated.Add(windows);
                return Task.FromResult(new HintSession { Hints = new List<Hint> { new FakeHint() }, OwningWindow = windows[0] });
            }

            public Task<HintSession> EnumHintsAsync() => throw new NotSupportedException();

            public Task<HintSession> EnumHintsAsync(IntPtr handle) => throw new NotSupportedException();

            public Task<HintSession> EnumBarHintsAsync(IReadOnlyList<IntPtr> windows, Rect monitor) => throw new NotSupportedException();

            public Task<bool> InvokeHintAsync(Hint hint) => Task.FromResult(true);
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
    }
}
