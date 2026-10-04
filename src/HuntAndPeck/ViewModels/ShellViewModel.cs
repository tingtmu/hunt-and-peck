using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;
using Application = System.Windows.Application;

namespace HuntAndPeck.ViewModels
{
    internal class ShellViewModel
    {
        private readonly Action<OverlayViewModel> _showOverlay;
        private readonly Action<DebugOverlayViewModel> _showDebugOverlay;
        private readonly Action<OptionsViewModel> _showOptions;
        private readonly Action<string, Exception> _reportError;
        private readonly IHintLabelService _hintLabelService;
        private readonly IHintProviderService _hintProviderService;
        private readonly IDebugHintProviderService _debugHintProviderService;

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
            IKeyListenerService keyListener)
        {
            _showOverlay = showOverlay;
            _showDebugOverlay = showDebugOverlay;
            _showOptions = showOptions;
            _reportError = reportError;
            _hintLabelService = hintLabelService;
            _hintProviderService = hintProviderService;
            _debugHintProviderService = debugHintProviderService;

            var hotKeys = RegisterHotKeys(keyListener);
            UnavailableHotKeys = hotKeys.Where(x => !x.IsRegistered).ToList();
            ToolTipText = BuildToolTipText(hotKeys);

            keyListener.OnHotKeyActivated += _keyListener_OnHotKeyActivated;
            keyListener.OnTaskbarHotKeyActivated += _keyListener_OnTaskbarHotKeyActivated;
            keyListener.OnDebugHotKeyActivated += _keyListener_OnDebugHotKeyActivated;

            ShowOptionsCommand = new DelegateCommand(ShowOptions);
            ExitCommand = new DelegateCommand(Exit);
        }

        public DelegateCommand ShowOptionsCommand { get; }
        public DelegateCommand ExitCommand { get; }

        /// <summary>
        /// Tray icon tooltip, listing the hotkeys and whether they are available
        /// </summary>
        public string ToolTipText { get; }

        /// <summary>
        /// Hotkeys that could not be registered (e.g. already used by another app)
        /// </summary>
        public IReadOnlyList<HotKey> UnavailableHotKeys { get; }

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
                return string.Format("{0} {1} already used by another app, so HuntAndPeck can't use it. Close that app and restart HuntAndPeck.", names, verb);
            }
        }

        private static List<HotKey> RegisterHotKeys(IKeyListenerService keyListener)
        {
            keyListener.HotKey = new HotKey
            {
                Keys = Keys.OemSemicolon,
                Modifier = KeyModifier.Alt
            };

            keyListener.TaskbarHotKey = new HotKey
            {
                Keys = Keys.OemSemicolon,
                Modifier = KeyModifier.Control
            };

            var hotKeys = new List<HotKey> { keyListener.HotKey, keyListener.TaskbarHotKey };
#if DEBUG
            keyListener.DebugHotKey = new HotKey
            {
                Keys = Keys.OemSemicolon,
                Modifier = KeyModifier.Alt | KeyModifier.Shift
            };
            hotKeys.Add(keyListener.DebugHotKey);
#endif
            return hotKeys;
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
            var taskbarHWnd = Taskbar.FindPrimaryTaskbar();
            if (taskbarHWnd == IntPtr.Zero)
            {
                return;
            }

            RunSession(() => _hintProviderService.EnumHintsAsync(taskbarHWnd), ShowHintOverlay);
        }

        private void _keyListener_OnDebugHotKeyActivated(object sender, EventArgs e)
        {
            RunSession(() => _debugHintProviderService.EnumDebugHintsAsync(), session => _showDebugOverlay(new DebugOverlayViewModel(session)));
        }

        private void ShowHintOverlay(HintSession session)
        {
            _showOverlay(new OverlayViewModel(session, _hintLabelService, _hintProviderService.InvokeHintAsync));
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

        public void ShowOptions()
        {
            var vm = new OptionsViewModel();
            _showOptions(vm);
        }
    }
}
