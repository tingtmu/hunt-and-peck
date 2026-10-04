using System;
using System.Collections.Generic;
using System.Linq;
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

        private sealed class FakeHint : Hint
        {
            public FakeHint()
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
            FakeHintProvider provider, FakeKeyListener keys, Action<OverlayViewModel> showOverlay, Action<string, Exception> reportError)
        {
            return new ShellViewModel(
                showOverlay, vm => { }, vm => { }, reportError,
                new HintLabelService(), provider, null, keys,
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                new FakeUserSettings(), message => { });
        }

        private static ShellViewModel CreateShell(FakeRegistryRunKey registry, List<string> warnings)
        {
            return new ShellViewModel(
                vm => { }, vm => { }, vm => { }, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, new FakeKeyListener(),
                new StartupRegistrationService(registry, ExePath, path => true),
                new FakeUserSettings(), warnings.Add);
        }

        private static ShellViewModel CreateShell(FakeKeyListener keys, FakeUserSettings settings, Action<OptionsViewModel> showOptions)
        {
            return new ShellViewModel(
                vm => { }, vm => { }, showOptions, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, keys,
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true),
                settings, message => { });
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
                vm => { }, vm => { }, vm => options = vm, (context, ex) => { },
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

            public Task InvokeHintAsync(Hint hint) => Task.CompletedTask;
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
