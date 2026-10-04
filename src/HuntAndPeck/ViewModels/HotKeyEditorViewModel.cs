using HuntAndPeck.Configuration;

namespace HuntAndPeck.ViewModels
{
    /// <summary>
    /// One hotkey in the options window: records a pressed combination, validating it, and resets to the default
    /// </summary>
    internal sealed class HotKeyEditorViewModel : NotifyPropertyChanged
    {
        private readonly HotKeyCombination _default;
        private HotKeyCombination _combination;
        private string _error;

        public HotKeyEditorViewModel(HotKeyCombination current, HotKeyCombination defaultCombination)
        {
            _combination = current;
            _default = defaultCombination;
            CaptureCommand = new DelegateCommand(p => Capture(p as HotKeyCombination));
            ResetCommand = new DelegateCommand(() => Capture(_default));
        }

        /// <summary>The chosen, valid combination</summary>
        public HotKeyCombination Combination
        {
            get { return _combination; }
            private set
            {
                _combination = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(DisplayText));
            }
        }

        /// <summary>Readable combination, e.g. "Alt+;"</summary>
        public string DisplayText => _combination.ToString();

        /// <summary>Why the last pressed combination or saving failed, else null</summary>
        public string Error
        {
            get { return _error; }
            set { _error = value; NotifyOfPropertyChange(); }
        }

        /// <summary>Takes a pressed <see cref="HotKeyCombination"/></summary>
        public DelegateCommand CaptureCommand { get; }

        public DelegateCommand ResetCommand { get; }

        /// <summary>
        /// Uses the combination if it is valid, else keeps the current one and shows why
        /// </summary>
        public void Capture(HotKeyCombination combination)
        {
            if (combination == null)
            {
                return;
            }

            string error;
            if (!HotKeyParser.Validate(combination, out error))
            {
                Error = error;
                return;
            }
            Error = null;
            Combination = combination;
        }
    }
}
