using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using HuntAndPeck.Configuration;
using HuntAndPeck.Diagnostics;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using HuntAndPeck.ViewModels;
using HuntAndPeck.Views;

namespace HuntAndPeck
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// How long a headless instance waits for the selected hint's invocation before exiting
        /// (exiting kills the background UIA worker thread and with it the invocation)
        /// </summary>
        private static readonly TimeSpan HeadlessInvocationWait =
            UiAutomationHintProviderService.InvocationTimeout + TimeSpan.FromSeconds(1);

        private readonly UserSettings _settings = new UserSettings();
        private ConfiguredHintLabelService _hintLabelService;
        private UiAutomationHintProviderService _hintProviderService;
        private SingleLaunchMutex _singleLaunchMutex;
        private KeyListenerService _keyListenerService;
        private TrayNotifier _trayNotifier;
        private GlobalExceptionHandlers _exceptionHandlers;

        /// <summary>
        /// Creates the overlay window; the view model may close it at any time, also after it already closed
        /// itself (e.g. on deactivation)
        /// </summary>
        private static OverlayView CreateOverlayView(OverlayViewModel vm)
        {
            var view = new OverlayView
            {
                DataContext = vm
            };
            if (vm.OverlayOwner != IntPtr.Zero && OverlayOwnerCheck.IsUsable(vm.OverlayOwner))
            {
                // E.g. a revealed auto-hide taskbar, which stays shown only while it or a window it owns is foreground
                new WindowInteropHelper(view).Owner = vm.OverlayOwner;
            }
            var closed = false;
            view.Closed += (sender, args) => closed = true;
            vm.CloseOverlay = () =>
            {
                if (!closed)
                {
                    view.Close();
                }
            };
            return view;
        }

        private void ShowOverlay(OverlayViewModel vm)
        {
            CreateOverlayView(vm).ShowDialog();
        }

        private void ShowDebugOverlay(DebugOverlayViewModel vm)
        {
            var view = new DebugOverlayView
            {
                DataContext = vm
            };
            view.ShowDialog();
        }

        private void ShowOptions(OptionsViewModel vm)
        {
            var view = new OptionsView
            {
                DataContext = vm
            };
            // Shown modally, so setting DialogResult closes it
            vm.Close = () => view.DialogResult = true;
            view.ShowDialog();
        }

        /// <summary>
        /// Shows the overlay for a headless (/hint, /tray) invocation, then shuts down once it closes and the
        /// selected hint has been invoked; shuts down straight away if there is nothing to show
        /// </summary>
        /// <param name="enumerate">Enumerates the hints</param>
        /// <param name="overlayClosed">Optional; called with the session and whether a hint was invoked once the overlay closed</param>
        private async void RunHeadless(Func<Task<HintSession>> enumerate, Action<HintSession, bool> overlayClosed = null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var session = await enumerate();
                if (!HintSession.IsShowable(session))
                {
                    Shutdown();
                    return;
                }

                var vm = new OverlayViewModel(session, _hintLabelService, _hintProviderService.InvokeHintAsync, _settings.Load().FontSize);
                var view = CreateOverlayView(vm);
                view.Closed += async (sender, args) =>
                {
                    try
                    {
                        overlayClosed?.Invoke(session, vm.HintInvoked);
                        await Task.WhenAny(vm.PendingInvocation, Task.Delay(HeadlessInvocationWait));
                    }
                    catch (Exception ex)
                    {
                        // async void handler: report here, an escaping exception would surface on the dispatcher
                        _exceptionHandlers.Report("Headless overlay close handling failed", ex);
                    }
                    finally
                    {
                        Shutdown();
                    }
                };
                view.Show();
            }
            catch (Exception ex)
            {
                _exceptionHandlers.Report("Headless hint session failed", ex);
                Shutdown();
            }
        }

        /// <summary>
        /// Starts the normal tray mode
        /// </summary>
        /// <returns>False if another instance is already running</returns>
        private bool StartTray()
        {
            // Prevent multiple startup in non-headless mode
            _singleLaunchMutex = new SingleLaunchMutex();
            if (_singleLaunchMutex.AlreadyRunning)
            {
                Trace.TraceInformation("Another instance is already running; exiting");
                Shutdown();
                return false;
            }

            string exePath;
            using (var process = Process.GetCurrentProcess())
            {
                exePath = process.MainModule.FileName;
            }
            var startupRegistration = new StartupRegistrationService(new RegistryRunKey(), exePath, File.Exists);
            UpdateStartupPathIfMoved(startupRegistration);

            // Create this as late as possible as it has a window
            _keyListenerService = new KeyListenerService();

            var shellViewModel = new ShellViewModel(
                ShowOverlay,
                ShowDebugOverlay,
                ShowOptions,
                _exceptionHandlers.Report,
                _hintLabelService,
                _hintProviderService,
                _hintProviderService,
                _keyListenerService,
                startupRegistration,
                _settings,
                _trayNotifier.ShowWarning);

            var shellView = new ShellView
            {
                DataContext = shellViewModel
            };
            // The window only hosts the tray icon: show it once (off-screen, not activated) so the icon and its
            // bindings initialise, then hide it so no stray window appears
            shellView.Show();
            shellView.Hide();

            _trayNotifier.Attach(shellView.TrayIcon);
            if (shellViewModel.HotKeyWarning != null)
            {
                _trayNotifier.ShowWarning(shellViewModel.HotKeyWarning);
            }
            return true;
        }

        /// <summary>
        /// Keeps an existing start with Windows registration working after the exe was moved
        /// </summary>
        private static void UpdateStartupPathIfMoved(StartupRegistrationService startupRegistration)
        {
            try
            {
                startupRegistration.UpdatePathIfMoved();
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                // Not worth a balloon at every start: the menu item shows the actual state and reports failures
                Trace.TraceWarning("Startup: updating the start with Windows path failed: {0}", ex);
            }
        }

        /// <summary>
        /// Loads the user settings, telling the user if a damaged settings file was reset
        /// </summary>
        private void InitializeSettings()
        {
            var warning = SettingsBootstrapper.Initialize();
            if (warning != null)
            {
                // Shown once the tray icon is attached (queued at background priority); headless only logs it
                _trayNotifier.ShowWarning(warning);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var isHint = e.Args.Contains("/hint");
            var isTray = e.Args.Contains("/tray");
            AppLog.Initialize(isHint ? "/hint" : isTray ? "/tray" : "tray icon");

            _trayNotifier = new TrayNotifier(Dispatcher);
            _exceptionHandlers = new GlobalExceptionHandlers(this, _trayNotifier, isHint || isTray);
            _exceptionHandlers.Register();
            InitializeSettings();
            _hintLabelService = new ConfiguredHintLabelService(() => _settings.Load().HintAlphabet);
            _hintProviderService = new UiAutomationHintProviderService(_trayNotifier.ShowWarning);

            if (isHint)
            {
                // support headless mode
                RunHeadless(() => _hintProviderService.EnumHintsAsync());
            }
            else if (isTray)
            {
                // support headless tray mode
                var barHints = new BarsHintSource(_hintProviderService);
                RunHeadless(barHints.EnumHintsAsync, barHints.OnOverlayClosed);
            }
            else if (!StartTray())
            {
                return;
            }
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Trace.TraceInformation("Exiting, code {0}", e.ApplicationExitCode);
            _keyListenerService?.Dispose();
            _hintProviderService?.Dispose();
            _singleLaunchMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
