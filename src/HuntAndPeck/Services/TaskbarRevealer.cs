using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Brings an auto-hidden taskbar into view before its hints are enumerated
    /// </summary>
    /// <remarks>
    /// While auto-hidden, the Windows 11 taskbar is moved off screen and its XAML content (the buttons) is not
    /// in the UI Automation tree at all, so it must be shown first. Activating it (SetForegroundWindow, as
    /// clicking it or Win+T would) shows it; it stays shown while it, or a window it owns, is the foreground
    /// window, and hides again by itself once another window takes the foreground. No user setting is changed.
    /// </remarks>
    internal sealed class TaskbarRevealer
    {
        /// <summary>How often the taskbar position is checked while waiting for it to show</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);

        /// <summary>Longest wait for the taskbar to show (its slide animation takes about 200-300 ms)</summary>
        public static readonly TimeSpan RevealTimeout = TimeSpan.FromMilliseconds(600);

        private static readonly TaskbarReveal NotRevealed = new TaskbarReveal(false, IntPtr.Zero);
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private readonly ITaskbarShell _shell;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly Func<TimeSpan> _now;

        public TaskbarRevealer()
            : this(new Win32TaskbarShell(), Task.Delay, () => Clock.Elapsed)
        {
        }

        public TaskbarRevealer(ITaskbarShell shell, Func<TimeSpan, Task> delay, Func<TimeSpan> now)
        {
            _shell = shell;
            _delay = delay;
            _now = now;
        }

        /// <summary>
        /// If the taskbar auto-hides, activates it and waits (asynchronously) until it is fully shown
        /// </summary>
        /// <remarks>
        /// Await on the UI thread. Does not fault: any failure, including an unexpected exception, is logged and
        /// reported as not revealed, so the caller enumerates the taskbar as it is.
        /// </remarks>
        public async Task<TaskbarReveal> RevealAsync(IntPtr taskbar)
        {
            try
            {
                return await RevealCoreAsync(taskbar);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Taskbar: revealing taskbar {0} failed, enumerating it as it is: {1}", taskbar, ex);
                return NotRevealed;
            }
        }

        private async Task<TaskbarReveal> RevealCoreAsync(IntPtr taskbar)
        {
            if (!_shell.IsAutoHide())
            {
                return TaskbarReveal.NotNeeded;
            }

            if (_shell.IsHung(taskbar))
            {
                Trace.TraceWarning("Taskbar: auto-hide taskbar {0} is not responding, not revealing it", taskbar);
                return NotRevealed;
            }

            // Fixed for the session, so the poll below only reads the window position
            Rect shownBounds;
            if (!_shell.TryGetShownBounds(taskbar, out shownBounds))
            {
                return NotRevealed;
            }

            var previous = _shell.GetForegroundWindow();
            var wasShown = IsShown(taskbar, shownBounds);
            var started = _now();

            // Activated even if already shown (e.g. under the mouse), so it stays shown for the session
            _shell.Activate(taskbar);

            var shown = await PollingWait.UntilAsync(() => IsShown(taskbar, shownBounds), PollInterval, RevealTimeout, _delay, _now);
            if (!shown)
            {
                Trace.TraceWarning("Taskbar: auto-hide taskbar {0} did not show within {1} ms; enumerating it as it is", taskbar, RevealTimeout.TotalMilliseconds);
                return NotRevealed;
            }

            Trace.TraceInformation("Taskbar: auto-hide taskbar {0} {1} in {2:0} ms", taskbar, wasShown ? "was already shown" : "revealed", (_now() - started).TotalMilliseconds);
            return new TaskbarReveal(true, previous == taskbar ? IntPtr.Zero : previous);
        }

        private bool IsShown(IntPtr taskbar, Rect shownBounds)
        {
            Rect window;
            return _shell.TryGetWindowBounds(taskbar, out window) && TaskbarGeometry.IsShown(window, shownBounds);
        }

        /// <summary>
        /// After the overlay of a revealed taskbar closed without invoking a hint (e.g. Escape), gives the
        /// foreground back to the window that had it, so the taskbar hides again as usual
        /// </summary>
        /// <param name="session">The session; see <see cref="HintSession.OverlayOwner"/> and <see cref="HintSession.ForegroundToRestore"/></param>
        /// <param name="hintInvoked">True if the overlay invoked a hint</param>
        public void RestoreForeground(HintSession session, bool hintInvoked)
        {
            RestoreForeground(session.OverlayOwner, session.ForegroundToRestore, hintInvoked);
        }

        /// <summary>
        /// Gives the foreground back to <paramref name="previous"/> if the taskbar (or nothing) has it. Skipped if
        /// a hint was invoked (it decides what comes to the foreground), if nothing was revealed, or if something
        /// else already took the foreground.
        /// </summary>
        /// <param name="taskbar">The revealed taskbar, else IntPtr.Zero</param>
        /// <param name="previous">The foreground window before the reveal, else IntPtr.Zero</param>
        /// <param name="hintInvoked">True if a hint was invoked</param>
        public void RestoreForeground(IntPtr taskbar, IntPtr previous, bool hintInvoked)
        {
            if (hintInvoked || taskbar == IntPtr.Zero || previous == IntPtr.Zero)
            {
                return;
            }

            // Closing the owned overlay hands the foreground to its owner, the taskbar; during a switch there may be none
            var foreground = _shell.GetForegroundWindow();
            if (foreground != taskbar && foreground != IntPtr.Zero)
            {
                return;
            }
            _shell.Activate(previous);
        }
    }
}
