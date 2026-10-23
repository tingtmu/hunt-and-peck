using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Configuration;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
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
        /// <param name="hintStyle">Label colours</param>
        public OverlayViewModel(
            HintSession session,
            IHintLabelService hintLabelService,
            Func<Hint, Task<bool>> invokeHint,
            double fontSize = FontSizeSetting.Default,
            HintStyle hintStyle = HintStyleSetting.Default)
        {
            _invokeHint = invokeHint;
            HintStyle = hintStyle;
            _bounds = session.OwningWindowBounds;
            OverlayOwner = session.OverlayOwner;

            // Labelled in reading order, so labels follow the screen rather than the UI Automation tree
            var hints = HintOrdering.ByReadingOrder(session.Hints);
            var labels = hintLabelService.GetHintStrings(hints.Count);
            for (int i = 0; i < labels.Count; ++i)
            {
                _hints.Add(new HintViewModel(hints[i], fontSize)
                {
                    Label = labels[i],
                    Active = true
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

        /// <summary>Label colours; applied by the view when it gets this view model</summary>
        public HintStyle HintStyle { get; }

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

        /// <summary>
        /// The typed letters: only hints whose label starts with them stay shown (<see cref="HintViewModel.Active"/>),
        /// and a single match is invoked
        /// </summary>
        public string MatchString
        {
            set
            {
                var matching = new List<HintViewModel>();
                foreach (var x in Hints)
                {
                    x.Active = IsMatch(x, value);
                    if (x.Active)
                    {
                        matching.Add(x);
                    }
                }

                if (matching.Count == 1)
                {
                    InvokeAndClose(matching[0].Hint);
                }
            }
        }

        /// <summary>
        /// True if some hint's label starts with <paramref name="prefix"/>; the view rejects a letter that would
        /// leave no hint, so a typo needs no Backspace
        /// </summary>
        public bool HasMatch(string prefix)
        {
            return Hints.Any(x => IsMatch(x, prefix));
        }

        private static bool IsMatch(HintViewModel hint, string prefix)
        {
            return hint.Label.StartsWith(prefix ?? "", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The mouse action chosen by the modifiers held for the typed letter, else null: the matched hint is
        /// clicked that way instead of using its UI Automation action. Shift: left click (for elements whose action
        /// silently does nothing), Ctrl: right-click, Ctrl+Shift: double-click, Alt: hover. Set by the view.
        /// </summary>
        public MouseAction? ForcedAction { get; set; }

        /// <summary>
        /// The <see cref="ForcedAction"/> for the modifiers held with a typed letter. Ctrl wins over Alt, as
        /// Ctrl+Alt is AltGr on some keyboard layouts.
        /// </summary>
        public static MouseAction? ForcedActionFor(bool shift, bool ctrl, bool alt = false)
        {
            if (ctrl)
            {
                return shift ? MouseAction.DoubleClick : MouseAction.RightClick;
            }
            if (alt)
            {
                return MouseAction.Hover;
            }
            return shift ? MouseAction.LeftClick : (MouseAction?)null;
        }

        /// <summary>
        /// True once the chosen hint is being hovered (<see cref="MouseAction.Hover"/>); unlike <see cref="ForcedAction"/>,
        /// unchanged by keys pressed after the choice
        /// </summary>
        public bool Hovering { get; private set; }

        /// <summary>True once a forced <see cref="MouseAction.Hover"/> moved the cursor onto the chosen element</summary>
        public bool Hovered { get; private set; }

        /// <summary>
        /// Invokes the hint (see <see cref="InvokeThenCloseAsync"/>), then, if its action failed in a way a mouse
        /// click may fix (<see cref="Hint.ClicksOnFailure"/>) within <see cref="ClickFallbackDeadline"/>, clicks
        /// the element instead, with the overlay closed. <see cref="ForcedAction"/> clicks straight away.
        /// </summary>
        private async void InvokeAndClose(Hint hint)
        {
            if (_invoked)
            {
                // Further typing after the match must not invoke again
                return;
            }
            _invoked = true;

            if (ForcedAction.HasValue)
            {
                var forced = hint.CreateClickHint(ForcedAction.Value);
                Hovering = forced != null && ForcedAction == MouseAction.Hover;
                hint = forced ?? hint;
            }

            // Published before closing, covering any fallback click: headless mode reads PendingInvocation
            // from the window's Closed event and waits for it
            var pending = new TaskCompletionSource<bool>();
            PendingInvocation = pending.Task;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var succeeded = await InvokeThenCloseAsync(hint);
                Hovered = succeeded && Hovering;
                if (succeeded || !hint.ClicksOnFailure)
                {
                    return;
                }
                var click = hint.CreateClickHint(MouseAction.LeftClick);
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
