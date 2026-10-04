using System;
using System.Threading.Tasks;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Hints for the primary taskbar, revealing it first if it auto-hides
    /// </summary>
    internal sealed class TaskbarHintSource
    {
        private readonly IHintProviderService _hintProvider;
        private readonly TaskbarRevealer _revealer;
        private readonly Func<IntPtr> _findTaskbar;

        public TaskbarHintSource(IHintProviderService hintProvider)
            : this(hintProvider, new TaskbarRevealer(), Taskbar.FindPrimaryTaskbar)
        {
        }

        public TaskbarHintSource(IHintProviderService hintProvider, TaskbarRevealer revealer, Func<IntPtr> findTaskbar)
        {
            _hintProvider = hintProvider;
            _revealer = revealer;
            _findTaskbar = findTaskbar;
        }

        /// <summary>
        /// Enumerates the taskbar's hints; for a revealed auto-hide taskbar the session's overlay must be owned
        /// by the taskbar (<see cref="HintSession.OverlayOwner"/>) so it stays shown
        /// </summary>
        /// <remarks>
        /// Await on the UI thread (the reveal activates windows from it). If there will be no overlay (no
        /// session, or enumeration threw), the foreground is given back right away, as after an overlay closed
        /// without invoking a hint.
        /// </remarks>
        /// <returns>The session, else null if there is no taskbar or enumeration failed (logged)</returns>
        public async Task<HintSession> EnumHintsAsync()
        {
            var taskbar = _findTaskbar();
            if (taskbar == IntPtr.Zero)
            {
                return null;
            }

            var reveal = await _revealer.RevealAsync(taskbar);
            if (!reveal.Revealed)
            {
                return await _hintProvider.EnumHintsAsync(taskbar);
            }

            HintSession session;
            try
            {
                session = await _hintProvider.EnumHintsAsync(taskbar);
            }
            catch
            {
                _revealer.RestoreForeground(taskbar, reveal.PreviousForeground, false);
                throw;
            }

            if (session == null)
            {
                _revealer.RestoreForeground(taskbar, reveal.PreviousForeground, false);
                return null;
            }
            return session.WithOverlayOwner(taskbar, reveal.PreviousForeground);
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
    }
}
