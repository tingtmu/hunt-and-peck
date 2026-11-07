namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Validated, immutable copy of the user settings
    /// </summary>
    internal sealed class UserSettingsSnapshot
    {
        public static readonly UserSettingsSnapshot Defaults = new UserSettingsSnapshot(
            FontSizeSetting.Default,
            Configuration.HintAlphabet.Default,
            HotKeyParser.ParseOrDefault(HotKeyParser.DefaultMainHotKey, HotKeyParser.DefaultMainHotKey, out _),
            HotKeyParser.ParseOrDefault(HotKeyParser.DefaultTaskbarHotKey, HotKeyParser.DefaultTaskbarHotKey, out _));

        public UserSettingsSnapshot(
            double fontSize, string hintAlphabet, HotKeyCombination mainHotKey, HotKeyCombination taskbarHotKey,
            HintStyle hintStyle = HintStyleSetting.Default, bool checkForUpdates = true)
        {
            FontSize = fontSize;
            HintStyle = hintStyle;
            HintAlphabet = hintAlphabet;
            MainHotKey = mainHotKey;
            TaskbarHotKey = taskbarHotKey;
            CheckForUpdates = checkForUpdates;
        }

        /// <summary>Hint label font size</summary>
        public double FontSize { get; }

        /// <summary>Hint label colours</summary>
        public HintStyle HintStyle { get; }

        /// <summary>Hint label letters, upper case</summary>
        public string HintAlphabet { get; }

        /// <summary>Hotkey that shows hints for the foreground window</summary>
        public HotKeyCombination MainHotKey { get; }

        /// <summary>Hotkey that shows hints for the taskbar</summary>
        public HotKeyCombination TaskbarHotKey { get; }

        /// <summary>Whether HuntAndPeck looks for a newer release at start-up (at most once a day)</summary>
        public bool CheckForUpdates { get; }

        /// <summary>A copy with the update check setting changed</summary>
        public UserSettingsSnapshot WithCheckForUpdates(bool checkForUpdates)
        {
            return new UserSettingsSnapshot(FontSize, HintAlphabet, MainHotKey, TaskbarHotKey, HintStyle, checkForUpdates);
        }
    }
}
