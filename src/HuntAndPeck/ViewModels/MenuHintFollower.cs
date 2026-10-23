using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.ViewModels
{
    /// <summary>
    /// Follows a hint's action into the menu or dropdown it opened (e.g. Ctrl+letter right-click): shows that
    /// menu's hints, and again for each submenu, with no hotkey. The menu overlay never takes the foreground, as
    /// the menu would close.
    /// </summary>
    internal sealed class MenuHintFollower
    {
        /// <summary>Menus followed after one hotkey press at most; guards against an app opening popups forever</summary>
        public const int MaxDepth = 5;

        /// <summary>Longest wait for the action (which may open the menu) to finish</summary>
        public static readonly TimeSpan InvocationWait = TimeSpan.FromSeconds(2);

        private readonly MenuPopupWatcher _popups;
        private readonly IHintProviderService _hintProvider;
        private readonly Func<HintSession, OverlayViewModel> _createOverlay;
        private readonly Action<OverlayViewModel, IReadOnlyList<IntPtr>> _showMenuOverlay;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly Func<CancellationToken, bool> _tryBeginSession;
        private readonly Action _endSession;

        /// <param name="createOverlay">Creates the overlay view model for a session</param>
        /// <param name="showMenuOverlay">Shows the menu overlay over the menu windows until it closes</param>
        /// <param name="delay">Waits; Task.Delay outside tests</param>
        /// <param name="tryBeginSession">Claims the session for a menu overlay; false if another one is active or the following was cancelled</param>
        /// <param name="endSession">Releases the session after the menu overlay closed</param>
        public MenuHintFollower(
            MenuPopupWatcher popups,
            IHintProviderService hintProvider,
            Func<HintSession, OverlayViewModel> createOverlay,
            Action<OverlayViewModel, IReadOnlyList<IntPtr>> showMenuOverlay,
            Func<TimeSpan, Task> delay,
            Func<CancellationToken, bool> tryBeginSession,
            Action endSession)
        {
            _popups = popups;
            _hintProvider = hintProvider;
            _createOverlay = createOverlay;
            _showMenuOverlay = showMenuOverlay;
            _delay = delay;
            _tryBeginSession = tryBeginSession;
            _endSession = endSession;
        }

        /// <summary>Taken before an overlay is shown, so a menu its hint opens can be told apart</summary>
        public HashSet<IntPtr> Snapshot()
        {
            return _popups.Snapshot();
        }

        /// <param name="session">The session whose overlay just closed</param>
        /// <param name="overlay">Its view model</param>
        /// <param name="before">The <see cref="Snapshot"/> taken before that overlay was shown</param>
        /// <param name="following">Cancelled when the user moves on (presses the hotkey)</param>
        public async Task FollowAsync(HintSession session, OverlayViewModel overlay, HashSet<IntPtr> before, CancellationToken following)
        {
            for (var depth = 0; depth < MaxDepth && overlay.HintInvoked && !following.IsCancellationRequested; depth++)
            {
                await Task.WhenAny(overlay.PendingInvocation, _delay(InvocationWait));

                var menus = await _popups.WaitForNewAsync(session.OwningWindow, before);
                if (menus.Count == 0 || following.IsCancellationRequested)
                {
                    return;
                }

                var menuSession = await _hintProvider.EnumPopupHintsAsync(menus);
                if (!HintSession.IsShowable(menuSession) || !_tryBeginSession(following))
                {
                    return;
                }

                Trace.TraceInformation("Menu: {0} hints in {1} popup windows", menuSession.Hints.Count, menus.Count);
                try
                {
                    before = _popups.Snapshot();
                    session = menuSession;
                    overlay = _createOverlay(menuSession);
                    _showMenuOverlay(overlay, menus);
                }
                finally
                {
                    _endSession();
                }
            }
        }
    }
}
