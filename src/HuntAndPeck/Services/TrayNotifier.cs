using System;
using System.Diagnostics;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Shows tray balloon notifications. Notifications before a tray icon is attached (e.g. headless mode)
    /// are only logged.
    /// </summary>
    internal sealed class TrayNotifier
    {
        private const string Title = "HuntAndPeck";

        private readonly Dispatcher _dispatcher;
        private TaskbarIcon _icon;

        public TrayNotifier(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Attach(TaskbarIcon icon)
        {
            _icon = icon;
        }

        /// <summary>
        /// Shows a warning balloon. Safe to call from any thread.
        /// </summary>
        public void ShowWarning(string message)
        {
            if (_dispatcher.HasShutdownStarted)
            {
                return;
            }
            _dispatcher.BeginInvoke(new Action(() => ShowOnUiThread(message)), DispatcherPriority.Background);
        }

        private void ShowOnUiThread(string message)
        {
            var icon = _icon;
            if (icon == null || icon.IsDisposed)
            {
                Trace.TraceInformation("Tray notification not shown (no tray icon): {0}", message);
                return;
            }

            try
            {
                icon.ShowBalloonTip(Title, message, BalloonIcon.Warning);
            }
            catch (Exception ex)
            {
                // Called from the global exception handlers: a failing balloon must not raise a new
                // unhandled exception (which would recurse), so log it and carry on
                Trace.TraceWarning("Showing tray notification failed: {0}", ex);
            }
        }
    }
}
