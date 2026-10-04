using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HuntAndPeck.Services;
using HuntAndPeck.Views;

namespace HuntAndPeck.Diagnostics
{
    /// <summary>
    /// Last-chance exception handling: logs every unhandled exception and tells the user (at most once per
    /// <see cref="NotificationInterval"/>) via a tray balloon.
    /// </summary>
    /// <remarks>
    /// Every handler body is guarded and never rethrows: an exception escaping a last-chance handler would
    /// recurse or crash the process.
    /// </remarks>
    internal sealed class GlobalExceptionHandlers
    {
        public static readonly TimeSpan NotificationInterval = TimeSpan.FromMinutes(10);

        /// <summary>More than this many UI thread exceptions within <see cref="BurstWindow"/> shuts the app down</summary>
        public const int MaxUiExceptionsPerWindow = 5;
        public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(30);

        private readonly Application _application;
        private readonly TrayNotifier _notifier;
        private readonly bool _headless;
        private readonly NotificationThrottle _throttle = new NotificationThrottle(NotificationInterval);
        private readonly BurstDetector _uiExceptionBurst = new BurstDetector(MaxUiExceptionsPerWindow, BurstWindow);

        /// <param name="application">The app</param>
        /// <param name="notifier">Tray notifications</param>
        /// <param name="headless">True for /hint and /tray: the process exits once no overlay remains</param>
        public GlobalExceptionHandlers(Application application, TrayNotifier notifier, bool headless)
        {
            _application = application;
            _notifier = notifier;
            _headless = headless;
        }

        public void Register()
        {
            _application.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // The hotkey listener is a WinForms window; without a handler WinForms shows its own error dialog
            System.Windows.Forms.Application.ThreadException += OnWinFormsThreadException;
        }

        /// <summary>
        /// Logs an exception that was caught but not otherwise handled, and notifies the user (throttled)
        /// </summary>
        public void Report(string context, Exception exception)
        {
            Guard("Report", () =>
            {
                Trace.TraceError("{0}: {1}", context, exception);
                NotifyUser();
            });
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Keep the tray app alive; a single failed hint session is not worth losing the process
            e.Handled = true;
            Guard("DispatcherUnhandledException", () =>
            {
                Trace.TraceError("Unhandled exception on the UI thread: {0}", e.Exception);
                RecoverUiThread();
            });
        }

        private void OnWinFormsThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Guard("ThreadException", () =>
            {
                Trace.TraceError("Unhandled exception in a WinForms window: {0}", e.Exception);
                RecoverUiThread();
            });
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Can't be prevented; the process terminates if IsTerminating
            Guard("UnhandledException", () =>
                Trace.TraceError("Unhandled exception (terminating: {0}): {1}", e.IsTerminating, e.ExceptionObject));
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            Guard("UnobservedTaskException", () =>
            {
                Trace.TraceError("Unobserved task exception: {0}", e.Exception);
                NotifyUser();
            });
        }

        /// <summary>
        /// After a UI thread exception: close overlays, shut down on a burst of exceptions (the app is
        /// likely stuck in a failure loop) or when a headless instance has nothing left to show, else notify
        /// </summary>
        private void RecoverUiThread()
        {
            CloseOverlays();

            if (_uiExceptionBurst.Record(DateTime.UtcNow))
            {
                Trace.TraceError("More than {0} UI thread exceptions within {1} s; shutting down", MaxUiExceptionsPerWindow, BurstWindow.TotalSeconds);
                _application.Shutdown(1);
                return;
            }

            if (_headless && !_application.Windows.OfType<ForegroundWindow>().Any())
            {
                Trace.TraceInformation("Headless instance has no overlay left after an exception; shutting down");
                _application.Shutdown(1);
                return;
            }

            NotifyUser();
        }

        private void NotifyUser()
        {
            if (!_throttle.TryAcquire(DateTime.UtcNow))
            {
                return;
            }

            var where = AppLog.LogPath ?? "the log";
            _notifier.ShowWarning("HuntAndPeck hit an error; details in " + where);
        }

        private void CloseOverlays()
        {
            var overlays = _application.Windows.OfType<ForegroundWindow>().ToArray();
            foreach (var overlay in overlays)
            {
                try
                {
                    overlay.Close();
                }
                catch (InvalidOperationException ex)
                {
                    // Close throws if the window is already closing; nothing more to do for it
                    Trace.TraceWarning("Could not close overlay after an unhandled exception: {0}", ex.Message);
                }
            }
        }

        private static void Guard(string handlerName, Action body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                // Never rethrow from a last-chance handler; a minimal trace is all that is safe here
                Trace.TraceError("Exception handler {0} failed: {1}", handlerName, ex.Message);
            }
        }
    }
}
