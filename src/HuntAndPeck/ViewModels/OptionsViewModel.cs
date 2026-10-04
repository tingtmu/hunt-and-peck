using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HuntAndPeck.Configuration;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.ViewModels
{
    /// <summary>
    /// The options window. Edits are only validated, applied and persisted on <see cref="SaveCommand"/>;
    /// closing the window otherwise discards them.
    /// </summary>
    internal sealed class OptionsViewModel : NotifyPropertyChanged
    {
        private readonly IUserSettings _settings;
        private readonly UserSettingsSnapshot _original;
        private readonly Func<HotKeyCombination, HotKeyCombination, IReadOnlyList<HotKeyFailure>> _applyHotKeys;
        private readonly Action<bool> _suspendHotKeys;
        private string _fontSizeText;
        private string _fontSizeError;
        private string _hintAlphabetText;
        private string _hintAlphabetError;
        private string _saveError;

        /// <summary>True while a hotkey box has keyboard focus (global hotkeys suspended)</summary>
        private bool _capturing;

        /// <param name="settings">The persisted settings</param>
        /// <param name="applyHotKeys">Registers new main and taskbar hotkeys all-or-nothing, returning failures</param>
        /// <param name="suspendHotKeys">Unregisters (true) or re-registers (false) the global hotkeys</param>
        public OptionsViewModel(
            IUserSettings settings,
            Func<HotKeyCombination, HotKeyCombination, IReadOnlyList<HotKeyFailure>> applyHotKeys,
            Action<bool> suspendHotKeys)
        {
            _settings = settings;
            _applyHotKeys = applyHotKeys;
            _suspendHotKeys = suspendHotKeys;
            _original = settings.Load();

            _fontSizeText = FontSizeSetting.Format(_original.FontSize);
            _hintAlphabetText = _original.HintAlphabet;
            MainHotKey = new HotKeyEditorViewModel(_original.MainHotKey, UserSettingsSnapshot.Defaults.MainHotKey);
            TaskbarHotKey = new HotKeyEditorViewModel(_original.TaskbarHotKey, UserSettingsSnapshot.Defaults.TaskbarHotKey);
            SaveCommand = new DelegateCommand(() => Save());
        }

        public string DisplayName => "HuntAndPeck Options";

        public HotKeyEditorViewModel MainHotKey { get; }
        public HotKeyEditorViewModel TaskbarHotKey { get; }

        /// <summary>Suggested font sizes; any number in range can be typed</summary>
        public IReadOnlyList<string> FontSizeChoices { get; } =
            Enumerable.Range(8, 17).Select(x => FontSizeSetting.Format(x)).ToList();

        public string FontSizeText
        {
            get { return _fontSizeText; }
            set { _fontSizeText = value; NotifyOfPropertyChange(); }
        }

        public string FontSizeError
        {
            get { return _fontSizeError; }
            private set { _fontSizeError = value; NotifyOfPropertyChange(); }
        }

        public string FontSizeHelp =>
            string.Format(CultureInfo.InvariantCulture, "{0} to {1}. Default: {2}",
                FontSizeSetting.Min, FontSizeSetting.Max, FontSizeSetting.Default);

        public string HintAlphabetText
        {
            get { return _hintAlphabetText; }
            set { _hintAlphabetText = value; NotifyOfPropertyChange(); }
        }

        public string HintAlphabetError
        {
            get { return _hintAlphabetError; }
            private set { _hintAlphabetError = value; NotifyOfPropertyChange(); }
        }

        public string HintAlphabetHelp =>
            string.Format("{0}-{1} different letters A-Z. Default: {2}", HintAlphabet.MinLength, HintAlphabet.MaxLength, HintAlphabet.Default);

        /// <summary>Why saving failed (other than a field error), else null</summary>
        public string SaveError
        {
            get { return _saveError; }
            private set { _saveError = value; NotifyOfPropertyChange(); }
        }

        public DelegateCommand SaveCommand { get; }

        /// <summary>Closes the window after a successful save; set by the view</summary>
        public Action Close { get; set; }

        /// <summary>A hotkey box got focus: suspend the global hotkeys so their combinations can be pressed</summary>
        public void BeginHotKeyCapture()
        {
            _capturing = true;
            _suspendHotKeys(true);
        }

        /// <summary>A hotkey box lost focus, or the window closed</summary>
        public void EndHotKeyCapture()
        {
            _capturing = false;
            _suspendHotKeys(false);
        }

        /// <summary>
        /// Validates, registers changed hotkeys and persists the settings, then closes the window; on any
        /// failure shows the error, keeps the window open and persists nothing
        /// </summary>
        /// <returns>True if saved</returns>
        public bool Save()
        {
            var values = Validate();
            if (values == null)
            {
                return false;
            }

            if (!ApplyHotKeys(values) || !Persist(values))
            {
                // Applying resumed the hotkeys; suspend them again while a hotkey box still has focus
                if (_capturing)
                {
                    _suspendHotKeys(true);
                }
                return false;
            }

            Close?.Invoke();
            return true;
        }

        /// <returns>False after showing the error and putting the previous hotkeys back</returns>
        private bool Persist(UserSettingsSnapshot values)
        {
            try
            {
                _settings.Save(values);
                return true;
            }
            catch (ConfigurationErrorsException ex)
            {
                Trace.TraceError("Options: saving the settings failed: {0}", ex);
                var notRestored = _applyHotKeys(_original.MainHotKey, _original.TaskbarHotKey);
                SaveError = "Couldn't save the settings: " + ex.Message + DescribeNotRestored(notRestored);
                return false;
            }
        }

        /// <returns>Text about previously active hotkeys that no longer work, else empty</returns>
        private static string DescribeNotRestored(IEnumerable<HotKeyFailure> failures)
        {
            var messages = failures.Select(x => x.Message).ToList();
            return messages.Count == 0
                ? string.Empty
                : " Previous hotkeys that could not be restored and no longer work: " + string.Join(" ", messages);
        }

        /// <returns>The validated settings, else null after showing the errors</returns>
        private UserSettingsSnapshot Validate()
        {
            string error;
            double fontSize;
            FontSizeError = FontSizeSetting.TryParse(FontSizeText, out fontSize, out error) ? null : error;

            string alphabet;
            HintAlphabetError = HintAlphabet.TryNormalize(HintAlphabetText, out alphabet, out error) ? null : error;

            MainHotKey.Error = null;
            TaskbarHotKey.Error = MainHotKey.Combination.Equals(TaskbarHotKey.Combination)
                ? "Choose a hotkey different from the main hotkey."
                : null;
            SaveError = null;

            if (FontSizeError != null || HintAlphabetError != null || TaskbarHotKey.Error != null)
            {
                return null;
            }
            return new UserSettingsSnapshot(fontSize, alphabet, MainHotKey.Combination, TaskbarHotKey.Combination);
        }

        /// <returns>
        /// False after showing the failures; the previous hotkeys are then kept, except any reported in
        /// <see cref="SaveError"/> as not restored
        /// </returns>
        private bool ApplyHotKeys(UserSettingsSnapshot values)
        {
            var failures = _applyHotKeys(values.MainHotKey, values.TaskbarHotKey);
            foreach (var failure in failures.Where(x => !x.IsRestore))
            {
                var editor = values.MainHotKey.Matches(failure.HotKey) ? MainHotKey : TaskbarHotKey;
                editor.Error = failure.Message + " Choose another one.";
            }

            var notRestored = DescribeNotRestored(failures.Where(x => x.IsRestore));
            if (notRestored.Length > 0)
            {
                SaveError = notRestored.TrimStart();
            }
            return failures.All(x => x.IsRestore);
        }
    }
}
