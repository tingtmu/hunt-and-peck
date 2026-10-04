using System.Collections.Generic;
using System.Windows;
using HuntAndPeck.Models;
using System.Linq;

namespace HuntAndPeck.ViewModels
{
    public class DebugOverlayViewModel : NotifyPropertyChanged, IOverlayBounds
    {
        private Rect _bounds;

        public DebugOverlayViewModel(HintSession session)
        {
            Bounds = session.OwningWindowBounds;
            Hints = session.Hints.OfType<DebugHint>().Select(x => new DebugHintViewModel(x)).ToList();
        }

        public List<DebugHintViewModel> Hints { get; set; }

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
    }
}
