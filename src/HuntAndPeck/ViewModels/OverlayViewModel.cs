using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.ViewModels
{
    internal class OverlayViewModel : NotifyPropertyChanged, IOverlayBounds
    {
        private Rect _bounds;
        private ObservableCollection<HintViewModel> _hints = new ObservableCollection<HintViewModel>();
        private readonly Func<Hint, Task> _invokeHint;
        private bool _invoked;

        /// <summary>Longest the overlay stays open waiting for a hint invocation to finish</summary>
        public static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);

        /// <param name="session">The hints to show</param>
        /// <param name="hintLabelService">Assigns labels to hints</param>
        /// <param name="invokeHint">Invokes the selected hint asynchronously</param>
        public OverlayViewModel(
            HintSession session,
            IHintLabelService hintLabelService,
            Func<Hint, Task> invokeHint)
        {
            _invokeHint = invokeHint;
            _bounds = session.OwningWindowBounds;

            var labels = hintLabelService.GetHintStrings(session.Hints.Count());
            for (int i = 0; i < labels.Count; ++i)
            {
                var hint = session.Hints[i];
                _hints.Add(new HintViewModel(hint)
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
        /// Invokes the hint (on the UIA worker thread), then closes the overlay once the invocation finished
        /// or <see cref="CloseDelay"/> passed, whichever is first. As before, the target normally acts while
        /// the overlay is still up; a hung target can't keep the overlay open.
        /// </summary>
        private async void InvokeAndClose(Hint hint)
        {
            if (_invoked)
            {
                // Further typing after the match must not invoke again
                return;
            }
            _invoked = true;

            var invocation = _invokeHint(hint);
            PendingInvocation = invocation;
            await Task.WhenAny(invocation, Task.Delay(CloseDelay));
            CloseOverlay?.Invoke();
        }
    }
}
