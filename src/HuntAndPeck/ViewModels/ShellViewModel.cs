using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using HuntAndPeck.Configuration;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;
using Application = System.Windows.Application;

namespace HuntAndPeck.ViewModels
{
    internal class ShellViewModel : NotifyPropertyChanged
    {
        private readonly Action<OverlayViewModel> _showOverlay;
        private readonly Action<DebugOverlayViewModel> _showDebugOverlay;
        private readonly Action<OptionsViewModel> _showOptions;
        private readonly Action<string, Exception> _reportError;
        private readonly IHintLabelService _hintLabelService;
        private readonly IHintProviderService _hintProviderService;
        private readonly IDebugHintProviderService _debugHintProviderService;
        private readonly IStartupRegistrationService _startupRegistration;
        private readonly IKeyListenerService _keyListener;
        private readonly IUserSettings _settings;
        private readonly Action<string> _notifyWarning;
        private readonly TaskbarHintSource _taskbarHints;
        private bool _startWithWindows;
        private string _toolTipText;

        /// <summary>
        /// True while a hint enumeration is running or an overlay is open; further hotkey presses are ignored
        /// </summary>
        private bool _sessionActive;

        public ShellViewModel(
            Action<OverlayViewModel> showOverlay,
            Action<DebugOverlayViewModel> showDebugOverlay,
            Action<OptionsViewModel> showOptions,
            Action<string, Exception> reportError,
            IHintLabelService hintLabelService,
            IHintProviderService hintProviderService,
            IDebugHintProviderService debugHintProviderService,
            IKeyListenerService keyListener,
            IStartupRegistrationService startupRegistration,
            IUserSettings settings,
            Action<string> notifyWarning)
        {
            _showOverlay = showOverlay;
            _showDebugOverlay = showDebugOverlay;
            _showOptions = showOptions;
            _reportError = reportError;
            _hintLabelService = hintLabelService;
            _hintProviderService = hintProviderService;
            _debugHintProviderService = debugHintProviderService;
            _startupRegistration = startupRegistration;
            _keyListener = keyListener;
            _settings = settings;
            _notifyWarning = notifyWarning;
            _taskbarHints = new TaskbarHintSource(hintProviderService);

            RegisterHotKeys(keyListener, settings.Load());
            RefreshHotKeyStatus();

            keyListener.OnHotKeyActivated += _keyListener_OnHotKeyActivated;
            keyListener.OnTaskbarHotKeyActivated += _keyListener_OnTaskbarHotKeyActivated;
            keyListener.OnDebugHotKeyActivated += _keyListener_OnDebugHotKeyActivated;

            ShowOptionsCommand = new DelegateCommand(ShowOptions);
            ExitCommand = new DelegateCommand(Exit);
            ToggleStartWithWindowsCommand = new DelegateCommand(ToggleStartWithWindows);
            RefreshStartWithWindows();
        }

        public DelegateCommand ShowOptionsCommand { get; }
        public DelegateCommand ExitCommand { get; }
        public DelegateCommand ToggleStartWithWindowsCommand { get; }

        /// <summary>
        /// Checked state of the tray menu's "Start with Windows" item: whether Windows starts HuntAndPeck at sign-in
        /// </summary>
        public bool StartWithWindows => _startWithWindows;

        /// <summary>
        /// Tray icon tooltip, listing the hotkeys and whether they are available
        /// </summary>
        public string ToolTipText
        {
            get { return _toolTipText; }
            private set { _toolTipText = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Hotkeys that could not be registered (e.g. already used by another app)
        /// </summary>
        public IReadOnlyList<HotKey> UnavailableHotKeys { get; private set; }

        /// <summary>
        /// User facing warning about unavailable hotkeys, else null if all were registered
        /// </summary>
        public string HotKeyWarning
        {
            get
            {
                if (UnavailableHotKeys.Count == 0)
                {
                    return null;
                }
                var names = string.Join(", ", UnavailableHotKeys.Select(x => x.ToString()));
                var verb = UnavailableHotKeys.Count == 1 ? "is" : "are";
                return string.Format("{0} {1} already used by another app, so HuntAndPeck can't use it. Choose another hotkey in Options, or close that app and restart HuntAndPeck.", names, verb);
            }
        }

        private static void RegisterHotKeys(IKeyListenerService keyListener, UserSettingsSnapshot settings)
        {
            keyListener.HotKey = settings.MainHotKey.ToHotKey();
            keyListener.TaskbarHotKey = settings.TaskbarHotKey.ToHotKey();
#if DEBUG
            keyListener.DebugHotKey = new HotKey
            {
                Keys = System.Windows.Forms.Keys.OemSemicolon,
                Modifier = KeyModifier.Alt | KeyModifier.Shift
            };
#endif
        }

        /// <summary>
        /// Updates <see cref="UnavailableHotKeys"/> and <see cref="ToolTipText"/> after hotkeys changed
        /// </summary>
        private void RefreshHotKeyStatus()
        {
            var hotKeys = new[] { _keyListener.HotKey, _keyListener.TaskbarHotKey, _keyListener.DebugHotKey }
                .Where(x => x != null)
                .ToList();
            UnavailableHotKeys = hotKeys.Where(x => !x.IsRegistered).ToList();
            ToolTipText = BuildToolTipText(hotKeys);
        }

        private IReadOnlyList<HotKeyFailure> ApplyHotKeys(HotKeyCombination main, HotKeyCombination taskbar)
        {
            var failures = _keyListener.ReplaceHotKeys(main.ToHotKey(), taskbar.ToHotKey());
            RefreshHotKeyStatus();
            if (failures.Any(x => !x.IsRestore))
            {
                // Not applied: the options window stays open and shows all failures
                return failures;
            }

            // Applied, so the options window closes: report hotkeys that stopped working here instead
            WarnRestoreFailures(failures);
            return new HotKeyFailure[0];
        }

        private void SuspendHotKeys(bool suspended)
        {
            var failures = _keyListener.SetSuspended(suspended);
            RefreshHotKeyStatus();
            WarnRestoreFailures(failures);
        }

        private void WarnRestoreFailures(IReadOnlyList<HotKeyFailure> failures)
        {
            if (failures.Count == 0)
            {
                return;
            }
            var messages = string.Join(" ", failures.Select(x => x.Message));
            _notifyWarning("Some hotkeys stopped working: " + messages + " Choose another hotkey in Options.");
        }

        private static string BuildToolTipText(IEnumerable<HotKey> hotKeys)
        {
            var lines = hotKeys.Select(x => string.Format("{0}{1}", x, x.IsRegistered ? "" : " (unavailable)"));
            return "HuntAndPeck\n" + string.Join("\n", lines);
        }

        private void _keyListener_OnHotKeyActivated(object sender, EventArgs e)
        {
            RunSession(() => _hintProviderService.EnumHintsAsync(), ShowHintOverlay);
        }

        private void _keyListener_OnTaskbarHotKeyActivated(object sender, EventArgs e)
        {
            // Started from the hotkey message, which gives this process the right to activate the taskbar
            RunSession(() => _taskbarHints.EnumHintsAsync(), ShowTaskbarOverlay);
        }

        private void _keyListener_OnDebugHotKeyActivated(object sender, EventArgs e)
        {
            RunSession(() => _debugHintProviderService.EnumDebugHintsAsync(), session => _showDebugOverlay(new DebugOverlayViewModel(session)));
        }

        private void ShowHintOverlay(HintSession session)
        {
            _showOverlay(CreateOverlayViewModel(session));
        }

        private void ShowTaskbarOverlay(HintSession session)
        {
            var vm = CreateOverlayViewModel(session);
            _showOverlay(vm);
            _taskbarHints.OnOverlayClosed(session, vm.HintInvoked);
        }

        private OverlayViewModel CreateOverlayViewModel(HintSession session)
        {
            var fontSize = _settings.Load().FontSize;
            return new OverlayViewModel(session, _hintLabelService, _hintProviderService.InvokeHintAsync, fontSize);
        }

        /// <summary>
        /// Enumerates hints off the UI thread and then shows them, ignoring the request if a session is active
        /// </summary>
        /// <remarks>Called on the UI thread; the await resumes on it, so the overlay is shown on the UI thread</remarks>
        private async void RunSession(Func<Task<HintSession>> enumerate, Action<HintSession> show)
        {
            if (_sessionActive)
            {
                return;
            }

            _sessionActive = true;
            try
            {
                var session = await enumerate();
                if (session != null)
                {
                    // Blocks (modal) until the overlay closes, keeping the session active meanwhile
                    show(session);
                }
            }
            catch (Exception ex)
            {
                // async void: an escaping exception would surface on whatever context posted the hotkey
                _reportError("Hint session failed", ex);
            }
            finally
            {
                _sessionActive = false;
            }
        }

        public void Exit()
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Re-reads <see cref="StartWithWindows"/>, which can change outside the app (e.g. in Task Manager);
        /// called whenever the tray menu opens
        /// </summary>
        /// <remarks>Always raises PropertyChanged, so the menu item drops any checked state it toggled itself</remarks>
        public void RefreshStartWithWindows()
        {
            try
            {
                _startWithWindows = _startupRegistration.IsEnabled;
            }
            catch (Exception ex) when (IsRegistryError(ex))
            {
                // Keep the last known state; the menu item still works and reports a failure if toggled
                Trace.TraceWarning("Startup: reading the start with Windows state failed: {0}", ex);
            }
            NotifyOfPropertyChange(nameof(StartWithWindows));
        }

        /// <summary>
        /// Turns start with Windows on or off; on failure tells the user and keeps the actual state
        /// </summary>
        public void ToggleStartWithWindows()
        {
            var enable = !_startWithWindows;
            try
            {
                if (enable)
                {
                    _startupRegistration.Enable();
                }
                else
                {
                    _startupRegistration.Disable();
                }
            }
            catch (Exception ex) when (IsRegistryError(ex))
            {
                Trace.TraceWarning("Startup: turning start with Windows {0} failed: {1}", enable ? "on" : "off", ex);
                _notifyWarning(string.Format("Couldn't turn {0} Start with Windows: {1}", enable ? "on" : "off", ex.Message));
            }

            // Show the actual state, which reverts the menu item's own toggle if the change failed
            RefreshStartWithWindows();
        }

        private static bool IsRegistryError(Exception ex)
        {
            return ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException;
        }

        public void ShowOptions()
        {
            var vm = new OptionsViewModel(_settings, ApplyHotKeys, SuspendHotKeys);
            _showOptions(vm);
        }
    }
}
