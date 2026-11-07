using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Configuration;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;
using HuntAndPeck.Tests.Services;
using HuntAndPeck.ViewModels;
using Xunit;

namespace HuntAndPeck.Tests.ViewModels
{
    public class ShellViewModelTest
    {
        private const string ExePath = @"C:\Program Files\Hunt And Peck\hap.exe";

        [Fact]
        public void StartWithWindows_Toggle_EnablesThenDisables()
        {
            var registry = new FakeRegistryRunKey();
            var warnings = new List<string>();
            var shell = CreateShell(registry, warnings);
            Assert.False(shell.StartWithWindows);

            shell.ToggleStartWithWindowsCommand.Execute(null);
            Assert.True(shell.StartWithWindows);
            Assert.Equal("\"" + ExePath + "\"", registry.Run[StartupRegistrationService.ValueName]);

            shell.ToggleStartWithWindowsCommand.Execute(null);
            Assert.False(shell.StartWithWindows);
            Assert.Empty(registry.Run);
            Assert.Empty(warnings);
        }

        [Fact]
        public void StartWithWindows_ToggleFails_WarnsAndRevertsCheckbox()
        {
            var registry = new FakeRegistryRunKey { WriteError = new UnauthorizedAccessException("denied") };
            var warnings = new List<string>();
            var shell = CreateShell(registry, warnings);
            var changes = new List<string>();
            shell.PropertyChanged += (sender, args) => changes.Add(args.PropertyName);

            shell.ToggleStartWithWindowsCommand.Execute(null);

            Assert.False(shell.StartWithWindows);
            Assert.Single(warnings);
            Assert.Contains(nameof(ShellViewModel.StartWithWindows), changes);
        }

        [Fact]
        public void StartWithWindows_Refresh_PicksUpExternalChange()
        {
            var registry = new FakeRegistryRunKey();
            var shell = CreateShell(registry, new List<string>());
            shell.ToggleStartWithWindowsCommand.Execute(null);

            // Disabled in Task Manager while the app runs
            registry.StartupApproved[StartupRegistrationService.ValueName] = new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            shell.RefreshStartWithWindows();
            Assert.False(shell.StartWithWindows);

            // Toggling turns it back on, re-approving it
            shell.ToggleStartWithWindowsCommand.Execute(null);
            Assert.True(shell.StartWithWindows);
        }
        [Fact]
        public void HotKey_WhileEnumerating_IsIgnored_AndFlagResetsAfterwards()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                CreateShell(provider, keys, vm => { }, (context, ex) => errors.Add(ex));

                keys.Press();
                keys.Press();
                Assert.Single(provider.Pending);

                provider.Pending[0].SetResult(null);
                keys.Press();
                Assert.Equal(2, provider.Pending.Count);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void HotKey_WhileOverlayOpen_IsIgnored_AndFailureResetsFlag()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var shows = 0;
                CreateShell(provider, keys, vm =>
                {
                    shows++;
                    keys.Press(); // re-entrant press while the (modal) overlay is open
                    throw new InvalidOperationException("overlay failed");
                }, (context, ex) => errors.Add(ex));

                keys.Press();
                provider.Pending[0].SetResult(new HintSession { Hints = new List<Hint> { new FakeHint() } });

                Assert.Equal(1, shows);
                Assert.Single(provider.Pending);
                Assert.IsType<InvalidOperationException>(Assert.Single(errors));

                keys.Press();
                Assert.Equal(2, provider.Pending.Count);
            }
        }

        [Fact]
        public void SessionWithoutHints_ShowsNoOverlay_AndFlagResets()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var shows = 0;
                CreateShell(provider, keys, vm => shows++, (context, ex) => { });

                keys.Press();
                provider.Pending[0].SetResult(new HintSession { Hints = new List<Hint>() });

                Assert.Equal(0, shows);
                keys.Press();
                Assert.Equal(2, provider.Pending.Count);
            }
        }

        [Fact]
        public void SessionWithoutHints_ShowsNothingToClickNotice_OverTheWindow()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var notices = new List<NoticeViewModel>();
                CreateShell(provider, keys, vm => { }, (context, ex) => { }, notices.Add);
                var bounds = new System.Windows.Rect(10, 20, 300, 200);

                keys.Press();
                provider.Pending[0].SetResult(new HintSession { Hints = new List<Hint>(), OwningWindowBounds = bounds });

                var notice = Assert.Single(notices);
                Assert.Equal(ShellViewModel.NothingToClickNotice, notice.Message);
                Assert.Equal(bounds, notice.Bounds);
            }
        }

        [Fact]
        public void FailedEnumeration_ShowsNoResponseNotice_AndHotKeyWorksAgain()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var notices = new List<NoticeViewModel>();
                CreateShell(provider, keys, vm => { }, (context, ex) => { }, notices.Add);

                keys.Press();
                provider.Pending[0].SetResult(null);

                var notice = Assert.Single(notices);
                Assert.Equal(ShellViewModel.NoResponseNotice, notice.Message);
                Assert.True(notice.Bounds.IsEmpty);
                keys.Press();
                Assert.Equal(2, provider.Pending.Count);
            }
        }

        [Fact]
        public void Hover_StartsScrolling_OnceTheInvocationCompletes()
        {
            using (UseNoSynchronizationContext())
            {
                var invocation = new TaskCompletionSource<bool>();
                var provider = new FakeHintProvider { Invoke = hint => invocation.Task };
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var scrolls = new List<FakeScroll>();
                CreateShell(provider, keys, vm => Choose(vm, MouseAction.Hover), (context, ex) => errors.Add(ex), startHoverScroll: () => StartScroll(scrolls));

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());

                Assert.Empty(scrolls);
                invocation.SetResult(true);
                Assert.True(SpinWait.SpinUntil(() => scrolls.Count == 1, TimeSpan.FromSeconds(5)));
                Assert.Single(scrolls);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void Hover_InvocationFails_DoesNotStartScrolling()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider { Invoke = hint => Task.FromResult(false) };
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var scrolls = new List<FakeScroll>();
                CreateShell(provider, keys, vm => Choose(vm, MouseAction.Hover), (context, ex) => errors.Add(ex), startHoverScroll: () => StartScroll(scrolls));

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());

                Assert.Empty(scrolls);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void Hover_OnHintThatCannotHover_DoesNotStartScrolling()
        {
            using (UseNoSynchronizationContext())
            {
                var invoked = new List<Hint>();
                var provider = new FakeHintProvider { Invoke = hint => { invoked.Add(hint); return Task.FromResult(true); } };
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var scrolls = new List<FakeScroll>();
                CreateShell(provider, keys, vm => Choose(vm, MouseAction.Hover), (context, ex) => errors.Add(ex), startHoverScroll: () => StartScroll(scrolls));

                keys.Press();
                var original = new UnclickableFakeHint();
                provider.Pending[0].SetResult(new HintSession { Hints = new List<Hint> { original } });

                Assert.Same(original, Assert.Single(invoked));
                Assert.Empty(scrolls);
                Assert.Empty(errors);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(MouseAction.RightClick)]
        public void NonHoverAction_DoesNotStartScrolling(MouseAction? action)
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var scrolls = new List<FakeScroll>();
                CreateShell(provider, keys, vm => Choose(vm, action), (context, ex) => errors.Add(ex), startHoverScroll: () => StartScroll(scrolls));

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());

                Assert.Empty(scrolls);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void SecondHover_DisposesTheFirstScroller()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var scrolls = new List<FakeScroll>();
                CreateShell(provider, keys, vm => Choose(vm, MouseAction.Hover), (context, ex) => errors.Add(ex), startHoverScroll: () => StartScroll(scrolls));

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                var first = Assert.Single(scrolls);
                Assert.Equal(0, first.Disposals);

                keys.Press();
                provider.Pending[1].SetResult(OneHintSession());

                Assert.Equal(2, scrolls.Count);
                Assert.Equal(1, first.Disposals);
                Assert.Equal(0, scrolls[1].Disposals);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void Hover_HookCannotBeInstalled_IsNotReportedAsAnError()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var errors = new List<Exception>();
                var starts = 0;
                CreateShell(provider, keys, vm => Choose(vm, MouseAction.Hover), (context, ex) => errors.Add(ex), startHoverScroll: () =>
                {
                    starts++;
                    throw new InvalidOperationException("no hook");
                });

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());

                Assert.Equal(1, starts);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void Repeat_AfterAnInvokedHint_ScansAgainAndShowsAnOverlayStillInRepeatMode()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var errors = new List<Exception>();
                var overlays = new List<OverlayViewModel>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: overlays.Count == 1, choose: true);
                }, errors);

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());

                // The overlay is closed; the shell waits for the window to settle before scanning
                Assert.Single(provider.Pending);
                Assert.Equal(1, delay.PendingCount);
                delay.ReleaseAll();
                Assert.Equal(2, provider.Pending.Count);
                provider.Pending[1].SetResult(OneHintSession());

                Assert.Equal(2, overlays.Count);
                Assert.True(overlays[0].CanRepeat);
                Assert.True(overlays[1].CanRepeat);
                Assert.True(overlays[1].Repeat);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void Repeat_KeepsGoingUntilAnOverlayEndsWithoutAChoice()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var errors = new List<Exception>();
                var overlays = new List<OverlayViewModel>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    // Round 1 turns repeat on and chooses, round 2 chooses, round 3 is Esc
                    Act(vm, repeat: overlays.Count == 1, choose: overlays.Count < 3);
                }, errors);

                keys.Press();
                for (var round = 0; round < 3; round++)
                {
                    provider.Pending[round].SetResult(OneHintSession());
                    delay.ReleaseAll();
                }

                Assert.Equal(3, overlays.Count);
                Assert.Equal(3, provider.Pending.Count);
                Assert.Equal(0, delay.PendingCount);
                Assert.Empty(errors);

                // The hotkey works as usual afterwards
                keys.Press();
                Assert.Equal(4, provider.Pending.Count);
            }
        }

        [Fact]
        public void Repeat_OnButNothingInvoked_DoesNotScanAgain()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var overlays = new List<OverlayViewModel>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: true, choose: false); // Esc
                }, new List<Exception>());

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();

                Assert.Single(overlays);
                Assert.True(overlays[0].Repeat);
                Assert.Single(provider.Pending);
                Assert.Equal(0, delay.PendingCount);
            }
        }

        [Fact]
        public void Repeat_Off_DoesNotScanAgainAfterAnInvokedHint()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var overlays = new List<OverlayViewModel>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: false, choose: true);
                }, new List<Exception>());

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();

                Assert.Single(overlays);
                Assert.False(overlays[0].Repeat);
                Assert.Single(provider.Pending);
                Assert.Equal(0, delay.PendingCount);
            }
        }

        [Fact]
        public void Repeat_NextScanHasNothingToClick_ShowsANoticeAndEndsRepeating()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var overlays = new List<OverlayViewModel>();
                var notices = new List<NoticeViewModel>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: true, choose: true);
                }, new List<Exception>(), notices.Add);

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();
                provider.Pending[1].SetResult(new HintSession { Hints = new List<Hint>() });
                delay.ReleaseAll();

                Assert.Single(overlays);
                Assert.Equal(ShellViewModel.NothingToClickNotice, Assert.Single(notices).Message);
                Assert.Equal(2, provider.Pending.Count);
                Assert.Equal(0, delay.PendingCount);

                // Repeating is over, but the hotkey works again at once
                keys.Press();
                Assert.Equal(3, provider.Pending.Count);
            }
        }

        [Fact]
        public void Repeat_NextScanGetsNoResponse_ShowsANoticeAndEndsRepeating()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var notices = new List<NoticeViewModel>();
                CreateRepeatShell(provider, keys, delay, vm => Act(vm, repeat: true, choose: true), new List<Exception>(), notices.Add);

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();
                provider.Pending[1].SetResult(null);
                delay.ReleaseAll();

                Assert.Equal(ShellViewModel.NoResponseNotice, Assert.Single(notices).Message);
                Assert.Equal(2, provider.Pending.Count);
            }
        }

        [Fact]
        public void Repeat_HotKeyPressedWhileWaiting_EndsRepeatingAndTheHotKeysOverlayIsNotRepeating()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var overlays = new List<OverlayViewModel>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: overlays.Count == 1, choose: overlays.Count == 1);
                }, new List<Exception>());
                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                Assert.Equal(1, delay.PendingCount);

                // The user presses the hotkey before the settle delay is over
                keys.Press();
                Assert.Equal(2, provider.Pending.Count);
                delay.ReleaseAll();

                // Repeat mode did not scan on top of the user's own scan
                Assert.Equal(2, provider.Pending.Count);
                provider.Pending[1].SetResult(OneHintSession());
                Assert.Equal(2, overlays.Count);
                Assert.False(overlays[1].Repeat);
                Assert.Equal(0, delay.PendingCount);
            }
        }

        [Fact]
        public void Repeat_AfterAHover_StartsScrollingInsteadOfScanningAgain()
        {
            using (UseNoSynchronizationContext())
            {
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var delay = new ControlledDelay();
                var scrolls = new List<FakeScroll>();
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    vm.ToggleRepeat();
                    Choose(vm, MouseAction.Hover);
                }, new List<Exception>(), startHoverScroll: () => StartScroll(scrolls));

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();

                Assert.Single(scrolls);
                Assert.Single(provider.Pending);
                Assert.Equal(0, delay.PendingCount);
            }
        }

        [Fact]
        public void Repeat_ActionOpenedAMenu_ShowsTheMenuOverlayAndStopsRepeating()
        {
            using (UseNoSynchronizationContext())
            {
                var windows = new FakeWindows();
                var delay = new ControlledDelay();
                var provider = new FakeHintProvider { PopupSession = OneHintSession() };
                var keys = new FakeKeyListener();
                var overlays = new List<OverlayViewModel>();
                var menuOverlays = new List<OverlayViewModel>();
                var watcher = new MenuPopupWatcher(windows, delay.Delay, () => delay.Now);
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: true, choose: true);
                    windows.Add(new TopLevelWindow(new IntPtr(5), 10, "#32768", true)); // the action opened a menu
                }, new List<Exception>(), menuPopups: watcher, showMenuOverlay: (vm, menus) => menuOverlays.Add(vm));

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();

                var menu = Assert.Single(menuOverlays);
                Assert.False(menu.CanRepeat);
                Assert.False(menu.Repeat);
                delay.ReleaseAll();
                Assert.Single(overlays);
                Assert.Single(provider.Pending);
            }
        }

        [Fact]
        public void Repeat_NoMenuOpened_ScansAgainAfterTheSettleDelay()
        {
            using (UseNoSynchronizationContext())
            {
                var windows = new FakeWindows();
                var delay = new ControlledDelay();
                var provider = new FakeHintProvider();
                var keys = new FakeKeyListener();
                var overlays = new List<OverlayViewModel>();
                var watcher = new MenuPopupWatcher(windows, delay.Delay, () => delay.Now);
                CreateRepeatShell(provider, keys, delay, vm =>
                {
                    overlays.Add(vm);
                    Act(vm, repeat: overlays.Count == 1, choose: overlays.Count == 1);
                }, new List<Exception>(), menuPopups: watcher, showMenuOverlay: (vm, menus) => { });

                keys.Press();
                provider.Pending[0].SetResult(OneHintSession());
                delay.ReleaseAll();
                Assert.Equal(2, provider.Pending.Count);
                provider.Pending[1].SetResult(OneHintSession());

                Assert.Equal(2, overlays.Count);
                Assert.True(overlays[1].Repeat);
            }
        }

        /// <summary>The overlay's user turns repeat on if asked (Space), then chooses the only hint (or presses Esc: nothing)</summary>
        private static void Act(OverlayViewModel vm, bool repeat, bool choose)
        {
            if (repeat && !vm.Repeat)
            {
                Assert.True(vm.ToggleRepeat());
            }
            if (choose)
            {
                Choose(vm, null);
            }
        }

        private static ShellViewModel CreateRepeatShell(
            FakeHintProvider provider, FakeKeyListener keys, ControlledDelay delay, Action<OverlayViewModel> showOverlay, List<Exception> errors,
            Action<NoticeViewModel> showNotice = null, Func<IDisposable> startHoverScroll = null,
            MenuPopupWatcher menuPopups = null, Action<OverlayViewModel, IReadOnlyList<IntPtr>> showMenuOverlay = null)
        {
            return new ShellViewModel(
                showOverlay, vm => { }, vm => { }, text => { }, (context, ex) => errors.Add(ex),
                new HintLabelService(), provider, null, keys,
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                new FakeUserSettings(), message => { }, showNotice, menuPopups, showMenuOverlay, startHoverScroll,
                delay: delay.Delay);
        }

        /// <summary>
        /// Settle delays (repeat mode's and the menu watcher's) wait until released, as real ones yield to the
        /// message loop, after the modal overlay returned; other waits end at once
        /// </summary>
        private sealed class ControlledDelay
        {
            private readonly List<TaskCompletionSource<bool>> _pending = new List<TaskCompletionSource<bool>>();

            public TimeSpan Now { get; private set; }

            public int PendingCount => _pending.Count;

            public Task Delay(TimeSpan duration)
            {
                Now += duration;
                if (duration != ShellViewModel.RepeatSettleDelay && duration != MenuPopupWatcher.SettleDelay)
                {
                    return Task.CompletedTask;
                }
                var waiting = new TaskCompletionSource<bool>();
                _pending.Add(waiting);
                return waiting.Task;
            }

            /// <summary>Ends every wait, including those started by the continuations meanwhile</summary>
            public void ReleaseAll()
            {
                while (_pending.Count > 0)
                {
                    var next = _pending[0];
                    _pending.RemoveAt(0);
                    next.SetResult(true);
                }
            }
        }

        private sealed class FakeWindows : ITopLevelWindowSource
        {
            private readonly List<TopLevelWindow> _windows = new List<TopLevelWindow> { new TopLevelWindow(new IntPtr(1), 10, "App", false) };

            public void Add(TopLevelWindow window) => _windows.Add(window);

            public IReadOnlyList<TopLevelWindow> VisibleWindows() => new List<TopLevelWindow>(_windows);

            public uint ProcessOf(IntPtr hWnd) => 10;
        }

        private static HintSession OneHintSession() => new HintSession { Hints = new List<Hint> { new FakeHint() } };

        /// <summary>Types the only hint's label, as the view does, with the action the modifiers chose</summary>
        private static void Choose(OverlayViewModel vm, MouseAction? action)
        {
            vm.ForcedAction = action;
            vm.MatchString = vm.Hints[0].Label;
        }

        private static IDisposable StartScroll(List<FakeScroll> scrolls)
        {
            var scroll = new FakeScroll();
            scrolls.Add(scroll);
            return scroll;
        }

        private sealed class FakeScroll : IDisposable
        {
            public int Disposals { get; private set; }

            public void Dispose() => Disposals++;
        }

        private sealed class FakeHint : Hint
        {
            public FakeHint()
                : base(IntPtr.Zero, new System.Windows.Rect(0, 0, 10, 10))
            {
            }

            /// <summary>Can be clicked (or hovered) instead, as a UI Automation hint can</summary>
            public override Hint CreateClickHint(MouseAction action) => new FakeHint();

            public override void Invoke()
            {
            }
        }

        /// <summary>Has no click (or hover) variant, so a hover runs its own action</summary>
        private sealed class UnclickableFakeHint : Hint
        {
            public UnclickableFakeHint()
                : base(IntPtr.Zero, new System.Windows.Rect(0, 0, 10, 10))
            {
            }

            public override void Invoke()
            {
            }
        }

        /// <summary>
        /// Without a context, await continuations run inline when the fake's task completes, keeping the
        /// tests deterministic (xUnit installs its own context around each test method)
        /// </summary>
        private static IDisposable UseNoSynchronizationContext()
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            return new Restore(() => SynchronizationContext.SetSynchronizationContext(previous));
        }

        private sealed class Restore : IDisposable
        {
            private readonly Action _restore;
            public Restore(Action restore) { _restore = restore; }
            public void Dispose() => _restore();
        }

        private static ShellViewModel CreateShell(
            FakeHintProvider provider, FakeKeyListener keys, Action<OverlayViewModel> showOverlay, Action<string, Exception> reportError,
            Action<NoticeViewModel> showNotice = null, Func<IDisposable> startHoverScroll = null)
        {
            return new ShellViewModel(
                showOverlay, vm => { }, vm => { }, text => { }, reportError,
                new HintLabelService(), provider, null, keys,
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                new FakeUserSettings(), message => { }, showNotice, startHoverScroll: startHoverScroll);
        }

        private static ShellViewModel CreateShell(FakeRegistryRunKey registry, List<string> warnings)
        {
            return new ShellViewModel(
                vm => { }, vm => { }, vm => { }, text => { }, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, new FakeKeyListener(),
                new StartupRegistrationService(registry, ExePath, path => true),
                new FakeUserSettings(), warnings.Add);
        }

        private static ShellViewModel CreateShell(FakeKeyListener keys, FakeUserSettings settings, Action<OptionsViewModel> showOptions)
        {
            return new ShellViewModel(
                vm => { }, vm => { }, showOptions, text => { }, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, keys,
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                settings, message => { });
        }

        [Fact]
        public void BuildAboutText_HasVersionRepositoryAndUpstreamCredit()
        {
            var text = ShellViewModel.BuildAboutText("9.8.7");

            Assert.Contains("HuntAndPeck 9.8.7", text);
            Assert.Contains("https://github.com/tingtmu/hunt-and-peck", text);
            Assert.Contains("A fork of zsims/hunt-and-peck by Zachary Sims", text);
        }

        [Fact]
        public void ShowAboutCommand_ShowsAboutTextWithAssemblyInformationalVersion_Once()
        {
            var assembly = typeof(ShellViewModel).Assembly;
            var version = ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyInformationalVersionAttribute))).InformationalVersion;
            var shown = new List<string>();
            var shell = new ShellViewModel(
                vm => { }, vm => { }, vm => { }, shown.Add, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, new FakeKeyListener(),
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                new FakeUserSettings(), message => { });

            shell.ShowAboutCommand.Execute(null);

            var text = Assert.Single(shown);
            Assert.Contains("HuntAndPeck " + version, text);
            Assert.Contains(ShellViewModel.RepositoryUrl, text);
        }

        [Fact]
        public void CheckForUpdatesCommand_RunsTheCheck_Once()
        {
            var checks = 0;
            var shell = new ShellViewModel(
                vm => { }, vm => { }, vm => { }, text => { }, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, new FakeKeyListener(),
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                new FakeUserSettings(), message => { }, checkForUpdates: () => checks++);

            shell.CheckForUpdatesCommand.Execute(null);

            Assert.Equal(1, checks);
        }

        [Fact]
        public void CheckForUpdatesCommand_WithoutACheck_DoesNothing()
        {
            CreateShell(new FakeRegistryRunKey(), new List<string>()).CheckForUpdatesCommand.Execute(null);
        }

        [Fact]
        public void HotKeys_ComeFromSettings()
        {
            var keys = new FakeKeyListener();
            var settings = new FakeUserSettings(new UserSettingsSnapshot(14, "SADF", CtrlAltH, CtrlAltJ));

            var shell = CreateShell(keys, settings, vm => { });

            Assert.True(CtrlAltH.Matches(keys.HotKey));
            Assert.True(CtrlAltJ.Matches(keys.TaskbarHotKey));
            Assert.StartsWith("HuntAndPeck\nCtrl+Alt+H\nCtrl+Alt+J", shell.ToolTipText);
        }

        [Fact]
        public void OptionsSave_ReplacesHotKeysAndRefreshesToolTip()
        {
            var keys = new FakeKeyListener();
            var settings = new FakeUserSettings();
            OptionsViewModel options = null;
            var shell = CreateShell(keys, settings, vm => options = vm);
            var changes = new List<string>();
            shell.PropertyChanged += (sender, args) => changes.Add(args.PropertyName);

            shell.ShowOptionsCommand.Execute(null);
            options.MainHotKey.CaptureCommand.Execute(CtrlAltH);
            Assert.True(options.Save());

            Assert.True(CtrlAltH.Matches(keys.HotKey));
            Assert.Equal(CtrlAltH, settings.Current.MainHotKey);
            Assert.StartsWith("HuntAndPeck\nCtrl+Alt+H\nCtrl+;", shell.ToolTipText);
            Assert.Contains(nameof(ShellViewModel.ToolTipText), changes);
        }

        [Fact]
        public void OptionsSave_HotKeyTaken_KeepsOldHotKey()
        {
            var keys = new FakeKeyListener();
            keys.Taken.Add(CtrlAltH);
            var settings = new FakeUserSettings();
            OptionsViewModel options = null;
            var shell = CreateShell(keys, settings, vm => options = vm);
            shell.ShowOptionsCommand.Execute(null);
            options.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.False(options.Save());
            Assert.Equal(UserSettingsSnapshot.Defaults.MainHotKey, settings.Current.MainHotKey);
            Assert.True(UserSettingsSnapshot.Defaults.MainHotKey.Matches(keys.HotKey));
            Assert.NotNull(options.MainHotKey.Error);
        }

        [Fact]
        public void ResumeFailures_WarnOnce()
        {
            var keys = new FakeKeyListener();
            var warnings = new List<string>();
            OptionsViewModel options = null;
            var shell = new ShellViewModel(
                vm => { }, vm => { }, vm => options = vm, text => { }, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, keys,
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                new FakeUserSettings(), warnings.Add);
            shell.ShowOptionsCommand.Execute(null);

            options.BeginHotKeyCapture();
            keys.ResumeFailures.Add(keys.HotKey);
            keys.ResumeFailures.Add(keys.TaskbarHotKey);
            options.EndHotKeyCapture();
            options.BeginHotKeyCapture();
            options.EndHotKeyCapture();

            var warning = Assert.Single(warnings);
            Assert.Contains("Alt+;", warning);
            Assert.Contains("Ctrl+;", warning);
        }

        private static readonly HotKeyCombination CtrlAltH =
            new HotKeyCombination(KeyModifier.Control | KeyModifier.Alt, System.Windows.Forms.Keys.H);

        private static readonly HotKeyCombination CtrlAltJ =
            new HotKeyCombination(KeyModifier.Control | KeyModifier.Alt, System.Windows.Forms.Keys.J);

        private sealed class FakeHintProvider : IHintProviderService
        {
            public List<TaskCompletionSource<HintSession>> Pending { get; } = new List<TaskCompletionSource<HintSession>>();

            public Task<HintSession> EnumHintsAsync()
            {
                var tcs = new TaskCompletionSource<HintSession>();
                Pending.Add(tcs);
                return tcs.Task;
            }

            public Task<HintSession> EnumHintsAsync(IntPtr handle) => EnumHintsAsync();

            public Task<HintSession> EnumBarHintsAsync(IReadOnlyList<IntPtr> windows, System.Windows.Rect monitor) => EnumHintsAsync();

            public List<IReadOnlyList<IntPtr>> PopupEnumerations { get; } = new List<IReadOnlyList<IntPtr>>();

            public HintSession PopupSession { get; set; }

            public Task<HintSession> EnumPopupHintsAsync(IReadOnlyList<IntPtr> windows)
            {
                PopupEnumerations.Add(windows);
                return Task.FromResult(PopupSession);
            }

            /// <summary>The result of invoking a hint; succeeds at once by default</summary>
            public Func<Hint, Task<bool>> Invoke { get; set; } = hint => Task.FromResult(true);

            public Task<bool> InvokeHintAsync(Hint hint) => Invoke(hint);
        }

        private sealed class FakeKeyListener : IKeyListenerService
        {
            public event EventHandler OnHotKeyActivated;
            public event EventHandler OnTaskbarHotKeyActivated { add { } remove { } }
            public event EventHandler OnDebugHotKeyActivated { add { } remove { } }

            private HotKey _hotKey;
            private HotKey _taskbarHotKey;
            private HotKey _debugHotKey;

            /// <summary>Combinations another app owns</summary>
            public HashSet<HotKeyCombination> Taken { get; } = new HashSet<HotKeyCombination>();

            public HotKey TaskbarHotKey { get { return _taskbarHotKey; } set { _taskbarHotKey = Register(value); } }
            public HotKey HotKey { get { return _hotKey; } set { _hotKey = Register(value); } }
            public HotKey DebugHotKey { get { return _debugHotKey; } set { _debugHotKey = Register(value); } }

            public IReadOnlyList<HotKeyFailure> ReplaceHotKeys(HotKey hotKey, HotKey taskbarHotKey)
            {
                var failures = new[] { hotKey, taskbarHotKey }
                    .Where(x => Taken.Contains(new HotKeyCombination(x.Modifier, x.Keys)))
                    .Select(x => new HotKeyFailure(x, HotKeyFailure.ErrorHotKeyAlreadyRegistered))
                    .ToList();
                if (failures.Count == 0)
                {
                    HotKey = hotKey;
                    TaskbarHotKey = taskbarHotKey;
                }
                return failures;
            }

            /// <summary>Hotkeys reported as not re-registered on the next resume</summary>
            public List<HotKey> ResumeFailures { get; } = new List<HotKey>();

            public IReadOnlyList<HotKeyFailure> SetSuspended(bool suspended)
            {
                if (suspended)
                {
                    return new HotKeyFailure[0];
                }
                var failures = ResumeFailures
                    .Select(x => new HotKeyFailure(x, HotKeyFailure.ErrorHotKeyAlreadyRegistered, isRestore: true))
                    .ToList();
                ResumeFailures.Clear();
                return failures;
            }

            public void Press() => OnHotKeyActivated?.Invoke(this, EventArgs.Empty);

            private HotKey Register(HotKey hotKey)
            {
                hotKey.IsRegistered = !Taken.Contains(new HotKeyCombination(hotKey.Modifier, hotKey.Keys));
                return hotKey;
            }
        }
    }
}
