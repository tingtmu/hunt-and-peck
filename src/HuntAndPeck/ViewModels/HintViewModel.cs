using HuntAndPeck.Models;

namespace HuntAndPeck.ViewModels
{
    public class HintViewModel : NotifyPropertyChanged
    {
        private string _label;
        private bool _active;

        /// <param name="hint">The hint</param>
        /// <param name="fontSize">Label font size</param>
        public HintViewModel(Hint hint, double fontSize)
        {
            Hint = hint;
            FontSize = fontSize;
        }

        public Hint Hint { get; set; }

        /// <summary>True while the label matches the typed letters; inactive hints are hidden</summary>
        public bool Active
        {
            get { return _active; }
            set
            {
                // Each key press sets every hint; only changed ones need a re-render
                if (_active == value)
                {
                    return;
                }
                _active = value;
                NotifyOfPropertyChange();
            }
        }

        public string Label
        {
            get { return _label; }
            set { _label = value; NotifyOfPropertyChange(); }
        }

        /// <summary>Label font size</summary>
        public double FontSize { get; }
    }
}
