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

        /// <summary>What a click on the balloon shown last does; null if nothing (UI thread only)</summary>
        private Action _onClick;

        public TrayNotifier(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Attach(TaskbarIcon icon)
        {
            _icon = icon;
            icon.TrayBalloonTipClicked += (sender, e) => RunClickAction();
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
            _dispatcher.BeginInvoke(new Action(() => ShowOnUiThread(message, BalloonIcon.Warning, null)), DispatcherPriority.Background);
        }

        /// <summary>
        /// Shows an information balloon. Safe to call from any thread.
        /// </summary>
        /// <param name="message">The text</param>
        /// <param name="onClick">Run on the UI thread if the user clicks the balloon; null for nothing</param>
        public void ShowInfo(string message, Action onClick)
        {
            if (_dispatcher.HasShutdownStarted)
            {
                return;
            }
            _dispatcher.BeginInvoke(new Action(() => ShowOnUiThread(message, BalloonIcon.Info, onClick)), DispatcherPriority.Background);
        }

        private void RunClickAction()
        {
            var action = _onClick;
            _onClick = null;
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Tray notification click failed: {0}", ex);
            }
        }

        private void ShowOnUiThread(string message, BalloonIcon symbol, Action onClick)
        {
            var icon = _icon;
            if (icon == null || icon.IsDisposed)
            {
                Trace.TraceInformation("Tray notification not shown (no tray icon): {0}", message);
                return;
            }

            try
            {
                // A click belongs to the balloon shown last, so a newer one replaces (or clears) the action
                _onClick = onClick;
                icon.ShowBalloonTip(Title, message, symbol);
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
