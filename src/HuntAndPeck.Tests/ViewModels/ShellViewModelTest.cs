using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Models;
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
                provider.Pending[0].SetResult(new HintSession { Hints = new List<Hint>() });

                Assert.Equal(1, shows);
                Assert.Single(provider.Pending);
                Assert.IsType<InvalidOperationException>(Assert.Single(errors));

                keys.Press();
                Assert.Equal(2, provider.Pending.Count);
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
                new StartupRegistrationService(new FakeRegistryRunKey(), ExePath, path => true), message => { });
        }

        private static ShellViewModel CreateShell(FakeRegistryRunKey registry, List<string> warnings)
        {
            return new ShellViewModel(
                vm => { }, vm => { }, vm => { }, (context, ex) => { },
                new HintLabelService(), new FakeHintProvider(), null, new FakeKeyListener(),
                new StartupRegistrationService(registry, ExePath, path => true), warnings.Add);
        }

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

            public Task InvokeHintAsync(Hint hint) => Task.CompletedTask;
        }

        private sealed class FakeKeyListener : IKeyListenerService
        {
            public event EventHandler OnHotKeyActivated;
            public event EventHandler OnTaskbarHotKeyActivated { add { } remove { } }
            public event EventHandler OnDebugHotKeyActivated { add { } remove { } }

            public HotKey TaskbarHotKey { get; set; }
            public HotKey HotKey { get; set; }
            public HotKey DebugHotKey { get; set; }

            public void Press() => OnHotKeyActivated?.Invoke(this, EventArgs.Empty);
        }
    }
}
