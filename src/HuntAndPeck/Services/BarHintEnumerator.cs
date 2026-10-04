using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Scans the windows of a bars mode session one after another within an overall deadline, and combines
    /// their hints into one session whose overlay covers them all
    /// </summary>
    /// <remarks>The scan itself is injected (the UIA worker outside tests)</remarks>
    internal sealed class BarHintEnumerator
    {
        /// <summary>Longest a whole bars mode enumeration may take</summary>
        public static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(7);

        /// <summary>
        /// Wait before scanning a bar again that gave no hints the first time it was seen. Chromium based content
        /// (e.g. a WebView2 bar) may build its accessibility tree only once a UI Automation client asks for it.
        /// </summary>
        public static readonly TimeSpan EmptyRetryDelay = TimeSpan.FromMilliseconds(250);

        /// <summary>No retry with less time than this left before the deadline</summary>
        public static readonly TimeSpan MinRetryBudget = TimeSpan.FromSeconds(1);

        private readonly Func<WindowScanTarget, TimeSpan, Task<List<Hint>>> _scan;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly Func<TimeSpan> _now;
        private readonly TimeSpan _windowTimeout;
        private readonly HashSet<IntPtr> _seenWindows = new HashSet<IntPtr>();
        private readonly object _seenLock = new object();

        /// <param name="scan">Scans one window within the timeout; null if it could not be enumerated (logged)</param>
        /// <param name="delay">Waits; Task.Delay outside tests</param>
        /// <param name="now">Monotonic clock</param>
        /// <param name="windowTimeout">Longest scan of one window</param>
        public BarHintEnumerator(
            Func<WindowScanTarget, TimeSpan, Task<List<Hint>>> scan,
            Func<TimeSpan, Task> delay,
            Func<TimeSpan> now,
            TimeSpan windowTimeout)
        {
            _scan = scan;
            _delay = delay;
            _now = now;
            _windowTimeout = windowTimeout;
        }

        /// <param name="windows">Each window and its bounds (physical pixels), the session's owner first</param>
        /// <param name="monitor">The monitor (physical pixels); windows are clipped to it</param>
        /// <returns>The session, else null if no window is on the monitor</returns>
        public async Task<HintSession> EnumAsync(IReadOnlyList<KeyValuePair<IntPtr, Rect>> windows, Rect monitor)
        {
            var deadline = _now() + OverallTimeout;
            var targets = ClipToMonitor(windows, monitor);
            if (targets.Count == 0)
            {
                return null;
            }

            var overlayBounds = HintBounds.UnionOf(targets.Select(x => x.Value));
            var hints = new List<Hint>();
            for (var i = 0; i < targets.Count; i++)
            {
                var remaining = deadline - _now();
                if (remaining <= TimeSpan.Zero)
                {
                    Trace.TraceWarning("Bars: {0} ms deadline passed; leaving out {1} windows", OverallTimeout.TotalMilliseconds, targets.Count - i);
                    break;
                }

                var target = new WindowScanTarget(targets[i].Key, targets[i].Value, overlayBounds, UiAutomationHintFactory.CreateBarHint, true);
                var windowHints = await ScanWithRetryAsync(target, deadline).ConfigureAwait(false);
                if (windowHints != null)
                {
                    hints.AddRange(windowHints);
                }
            }

            Trace.TraceInformation("Bars: {0} hints from {1} windows, overlay {2}", hints.Count, targets.Count, overlayBounds);
            return new HintSession
            {
                Hints = hints,
                OwningWindow = targets[0].Key,
                OwningWindowBounds = overlayBounds,
            };
        }

        private async Task<List<Hint>> ScanWithRetryAsync(WindowScanTarget target, TimeSpan deadline)
        {
            var hints = await _scan(target, Budget(deadline)).ConfigureAwait(false);
            if (!MarkSeen(target.Handle) || hints == null || hints.Count > 0)
            {
                return hints;
            }

            if (deadline - _now() < MinRetryBudget)
            {
                Trace.TraceInformation("Bars: window {0} gave no hints; too close to the deadline to scan it again", target.Handle);
                return hints;
            }

            Trace.TraceInformation("Bars: window {0} gave no hints the first time; scanning it again in {1} ms", target.Handle, EmptyRetryDelay.TotalMilliseconds);
            await _delay(EmptyRetryDelay).ConfigureAwait(false);
            return await _scan(target, Budget(deadline)).ConfigureAwait(false);
        }

        /// <returns>True if the window had not been seen before in this app run</returns>
        private bool MarkSeen(IntPtr hWnd)
        {
            lock (_seenLock)
            {
                return _seenWindows.Add(hWnd);
            }
        }

        private TimeSpan Budget(TimeSpan deadline)
        {
            var remaining = deadline - _now();
            return remaining < _windowTimeout ? remaining : _windowTimeout;
        }

        private static List<KeyValuePair<IntPtr, Rect>> ClipToMonitor(IReadOnlyList<KeyValuePair<IntPtr, Rect>> windows, Rect monitor)
        {
            var result = new List<KeyValuePair<IntPtr, Rect>>();
            foreach (var window in windows)
            {
                var onMonitor = HintBounds.ClipToMonitor(window.Value, monitor);
                if (onMonitor.IsEmpty)
                {
                    Trace.TraceInformation("Bars: window {0} at {1} is not on the monitor; leaving it out", window.Key, window.Value);
                    continue;
                }
                result.Add(new KeyValuePair<IntPtr, Rect>(window.Key, onMonitor));
            }
            return result;
        }
    }
}
