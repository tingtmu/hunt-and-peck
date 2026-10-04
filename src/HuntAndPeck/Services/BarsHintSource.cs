using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Bars mode: hints for the primary taskbar (revealed first if it auto-hides) and every edge-docked bar
    /// window (<see cref="BarClassifier"/>) on the monitor under the mouse cursor, in one overlay covering them
    /// </summary>
    internal sealed class BarsHintSource
    {
        /// <summary>Most windows in one session (the taskbar included), bounding the enumeration time</summary>
        public const int MaxWindows = 8;

        private readonly IHintProviderService _hintProvider;
        private readonly TaskbarRevealer _revealer;
        private readonly Func<BarsScope> _findScope;
        private readonly Func<BarsScope, IReadOnlyList<IntPtr>> _findBars;

        public BarsHintSource(IHintProviderService hintProvider)
            : this(hintProvider, new TaskbarRevealer(), BarsScope.ForCursor, BarWindowFinder.FindBars)
        {
        }

        public BarsHintSource(
            IHintProviderService hintProvider,
            TaskbarRevealer revealer,
            Func<BarsScope> findScope,
            Func<BarsScope, IReadOnlyList<IntPtr>> findBars)
        {
            _hintProvider = hintProvider;
            _revealer = revealer;
            _findScope = findScope;
            _findBars = findBars;
        }

        /// <summary>
        /// Enumerates the hints of the taskbar and the bars; for a revealed auto-hide taskbar the session's
        /// overlay must be owned by the taskbar (<see cref="HintSession.OverlayOwner"/>) so it stays shown
        /// </summary>
        /// <remarks>
        /// Await on the UI thread (the reveal activates windows from it). If there will be no overlay (no
        /// session, no hints, or enumeration threw), the foreground is given back right away, as after an
        /// overlay closed without invoking a hint.
        /// </remarks>
        /// <returns>The session, else null if there is nothing to show (logged)</returns>
        public async Task<HintSession> EnumHintsAsync()
        {
            var scope = _findScope();
            if (scope == null)
            {
                return null;
            }

            var taskbar = scope.Taskbar;
            var reveal = taskbar == IntPtr.Zero ? TaskbarReveal.NotNeeded : await _revealer.RevealAsync(taskbar);

            var windows = CollectWindows(taskbar, _findBars(scope));
            if (windows.Count == 0)
            {
                Trace.TraceInformation("Bars: no taskbar and no bar windows on the monitor under the cursor; no overlay shown");
                return null;
            }

            HintSession session;
            try
            {
                session = await _hintProvider.EnumBarHintsAsync(windows, scope.MonitorBounds);
            }
            catch
            {
                RestoreIfRevealed(taskbar, reveal);
                throw;
            }

            if (session == null || session.Hints == null || session.Hints.Count == 0)
            {
                Trace.TraceInformation("Bars: no hints in {0} windows; no overlay shown", windows.Count);
                RestoreIfRevealed(taskbar, reveal);
                return null;
            }
            return reveal.Revealed ? session.WithOverlayOwner(taskbar, reveal.PreviousForeground) : session;
        }

        /// <summary>
        /// Call once the session's overlay has closed
        /// </summary>
        /// <param name="session">The session the overlay showed</param>
        /// <param name="hintInvoked">True if the overlay invoked a hint</param>
        public void OnOverlayClosed(HintSession session, bool hintInvoked)
        {
            _revealer.RestoreForeground(session, hintInvoked);
        }

        /// <summary>The taskbar (if any) first, then the bars, without duplicates, at most <see cref="MaxWindows"/></summary>
        internal static List<IntPtr> CollectWindows(IntPtr taskbar, IEnumerable<IntPtr> bars)
        {
            var windows = new List<IntPtr>();
            if (taskbar != IntPtr.Zero)
            {
                windows.Add(taskbar);
            }
            windows.AddRange(bars.Where(x => x != IntPtr.Zero && !windows.Contains(x)).Distinct());
            if (windows.Count <= MaxWindows)
            {
                return windows;
            }

            Trace.TraceWarning("Bars: {0} windows found; using the first {1}", windows.Count, MaxWindows);
            return windows.Take(MaxWindows).ToList();
        }

        private void RestoreIfRevealed(IntPtr taskbar, TaskbarReveal reveal)
        {
            if (reveal.Revealed)
            {
                _revealer.RestoreForeground(taskbar, reveal.PreviousForeground, false);
            }
        }
    }
}
