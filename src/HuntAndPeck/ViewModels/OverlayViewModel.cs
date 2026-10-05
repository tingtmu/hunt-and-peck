using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Configuration;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.ViewModels
{
    internal class OverlayViewModel : NotifyPropertyChanged, IOverlayBounds
    {
        private Rect _bounds;
        private ObservableCollection<HintViewModel> _hints = new ObservableCollection<HintViewModel>();
        private readonly Func<Hint, Task<bool>> _invokeHint;
        private bool _invoked;

        /// <summary>Longest the overlay stays open waiting for a hint invocation to finish</summary>
        public static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);

        /// <summary>A failed action is retried as a click only if it failed this soon after the hint was chosen</summary>
        public static readonly TimeSpan ClickFallbackDeadline = TimeSpan.FromSeconds(1);

        /// <param name="session">The hints to show</param>
        /// <param name="hintLabelService">Assigns labels to hints</param>
        /// <param name="invokeHint">
        /// Invokes the selected hint asynchronously; never faults, and results in false if a mouse click may
        /// fix a failed action (see <see cref="IHintProviderService.InvokeHintAsync"/>)
        /// </param>
        /// <param name="fontSize">Label font size</param>
        public OverlayViewModel(
            HintSession session,
            IHintLabelService hintLabelService,
            Func<Hint, Task<bool>> invokeHint,
            double fontSize = FontSizeSetting.Default)
        {
            _invokeHint = invokeHint;
            _bounds = session.OwningWindowBounds;
            OverlayOwner = session.OverlayOwner;

            var labels = hintLabelService.GetHintStrings(session.Hints.Count());
            for (int i = 0; i < labels.Count; ++i)
            {
                var hint = session.Hints[i];
                _hints.Add(new HintViewModel(hint, fontSize)
                {
                    Label = labels[i],
                    Active = false
                });
            }
        }

        /// <summary>
        /// Bounds in physical screen pixels
        /// </summary>
        public Rect Bounds
        {
            get
            {
                return _bounds;
            }
            set
            {
                _bounds = value;
                NotifyOfPropertyChange();
            }
        }

        public ObservableCollection<HintViewModel> Hints
        {
            get
            {
                return _hints;
            }
            set
            {
                _hints = value;
                NotifyOfPropertyChange();
            }
        }

        public Action CloseOverlay { get; set; }

        /// <summary>
        /// The most recent hint invocation (completed if none). Headless mode waits for it before exiting.
        /// </summary>
        public Task PendingInvocation { get; private set; } = Task.CompletedTask;

        /// <summary>True once a hint was selected and invoked</summary>
        public bool HintInvoked => _invoked;

        /// <summary>
        /// Window (of another process) that must own the overlay window, else IntPtr.Zero; see
        /// <see cref="HintSession.OverlayOwner"/>
        /// </summary>
        public IntPtr OverlayOwner { get; }

        public string MatchString
        {
            set
            {
                foreach (var x in Hints)
                {
                    x.Active = false;
                }

                var matching = Hints.Where(x => x.Label.StartsWith(value, StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach (var x in matching)
                {
                    x.Active = true;
                }

                if (matching.Count() == 1)
                {
                    InvokeAndClose(matching.First().Hint);
                }
            }
        }

        /// <summary>
        /// True while Shift is held for the typed letter: the matched hint is clicked with the mouse instead of
        /// using its UI Automation action (for elements whose action silently does nothing). Set by the view.
        /// </summary>
        public bool ForceClick { get; set; }

        /// <summary>
        /// Invokes the hint (see <see cref="InvokeThenCloseAsync"/>), then, if its action failed in a way a mouse
        /// click may fix (<see cref="Hint.ClicksOnFailure"/>) within <see cref="ClickFallbackDeadline"/>, clicks
        /// the element instead, with the overlay closed. <see cref="ForceClick"/> clicks straight away.
        /// </summary>
        private async void InvokeAndClose(Hint hint)
        {
            if (_invoked)
            {
                // Further typing after the match must not invoke again
                return;
            }
            _invoked = true;

            if (ForceClick)
            {
                hint = hint.CreateClickHint() ?? hint;
            }

            // Published before closing, covering any fallback click: headless mode reads PendingInvocation
            // from the window's Closed event and waits for it
            var pending = new TaskCompletionSource<bool>();
            PendingInvocation = pending.Task;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                if (await InvokeThenCloseAsync(hint) || !hint.ClicksOnFailure)
                {
                    return;
                }
                var click = hint.CreateClickHint();
                if (click == null)
                {
                    return;
                }
                if (stopwatch.Elapsed > ClickFallbackDeadline)
                {
                    // The user may have moved on (another window, another overlay); a click now could hit anything
                    Trace.TraceInformation("Invoking {0} failed after {1} ms; too late to click the element instead", hint.GetType().Name, stopwatch.ElapsedMilliseconds);
                    return;
                }
                Trace.TraceInformation("Invoking {0} failed; clicking the element instead", hint.GetType().Name);
                await _invokeHint(click);
            }
            finally
            {
                pending.SetResult(true);
            }
        }

        /// <summary>
        /// Invokes the hint (on the UIA worker thread) and closes the overlay once the invocation finished or
        /// <see cref="CloseDelay"/> passed, whichever is first. As before, the target normally acts while the
        /// overlay is still up; a hung target can't keep the overlay open. A hint that must not run under the
        /// overlay (<see cref="Hint.InvokeAfterOverlayCloses"/>) is invoked right after the overlay closed instead.
        /// </summary>
        /// <returns>The invocation's result, once it finished; the overlay is closed by then</returns>
        private async Task<bool> InvokeThenCloseAsync(Hint hint)
        {
            if (hint.InvokeAfterOverlayCloses)
            {
                CloseOverlay?.Invoke();
                return await _invokeHint(hint);
            }

            var invocation = _invokeHint(hint);
            await Task.WhenAny(invocation, Task.Delay(CloseDelay));
            CloseOverlay?.Invoke();
            return await invocation;
        }
    }
}
