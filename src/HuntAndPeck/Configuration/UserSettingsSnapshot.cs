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

        public UserSettingsSnapshot(double fontSize, string hintAlphabet, HotKeyCombination mainHotKey, HotKeyCombination taskbarHotKey)
        {
            FontSize = fontSize;
            HintAlphabet = hintAlphabet;
            MainHotKey = mainHotKey;
            TaskbarHotKey = taskbarHotKey;
        }

        /// <summary>Hint label font size</summary>
        public double FontSize { get; }

        /// <summary>Hint label letters, upper case</summary>
        public string HintAlphabet { get; }

        /// <summary>Hotkey that shows hints for the foreground window</summary>
        public HotKeyCombination MainHotKey { get; }

        /// <summary>Hotkey that shows hints for the taskbar</summary>
        public HotKeyCombination TaskbarHotKey { get; }
    }
}
