using System;
using System.Collections.Generic;
using System.Diagnostics;
using HuntAndPeck.Properties;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// User settings backed by <see cref="Settings"/> (user.config)
    /// </summary>
    /// <remarks>Use on the UI thread</remarks>
    internal sealed class UserSettings : IUserSettings
    {
        /// <summary>Invalid values already logged, so each is logged once rather than on every load</summary>
        private readonly HashSet<string> _reportedInvalid = new HashSet<string>();

        public UserSettingsSnapshot Load()
        {
            var settings = Settings.Default;
            return Parse(settings.FontSize, settings.HintAlphabet, settings.MainHotKey, settings.TaskbarHotKey, settings.HintStyle, ReportInvalid);
        }

        public void Save(UserSettingsSnapshot values)
        {
            var settings = Settings.Default;
            settings.FontSize = FontSizeSetting.Format(values.FontSize);
            settings.HintStyle = HintStyleSetting.Format(values.HintStyle);
            settings.HintAlphabet = values.HintAlphabet;
            settings.MainHotKey = values.MainHotKey.ToStorageString();
            settings.TaskbarHotKey = values.TaskbarHotKey.ToStorageString();
            settings.Save();
            Trace.TraceInformation(
                "Settings: saved font size {0}, hint style {1}, alphabet {2}, hotkeys {3} / {4}",
                settings.FontSize, settings.HintStyle, settings.HintAlphabet, settings.MainHotKey, settings.TaskbarHotKey);
        }

        /// <summary>
        /// Validates stored values, replacing each invalid one by its default
        /// </summary>
        /// <param name="reportInvalid">Called with the setting name, stored value and error of each invalid value</param>
        public static UserSettingsSnapshot Parse(
            string fontSize, string hintAlphabet, string mainHotKey, string taskbarHotKey, string hintStyle,
            Action<string, string, string> reportInvalid)
        {
            var defaults = UserSettingsSnapshot.Defaults;
            string error;

            double size;
            if (!FontSizeSetting.TryParse(fontSize, out size, out error))
            {
                reportInvalid(nameof(Settings.FontSize), fontSize, error);
            }

            HintStyle style;
            if (!HintStyleSetting.TryParse(hintStyle, out style, out error))
            {
                reportInvalid(nameof(Settings.HintStyle), hintStyle, error);
            }

            string alphabet;
            if (!HintAlphabet.TryNormalize(hintAlphabet, out alphabet, out error))
            {
                reportInvalid(nameof(Settings.HintAlphabet), hintAlphabet, error);
                alphabet = defaults.HintAlphabet;
            }

            var main = ParseHotKey(nameof(Settings.MainHotKey), mainHotKey, HotKeyParser.DefaultMainHotKey, reportInvalid);
            var taskbar = ParseHotKey(nameof(Settings.TaskbarHotKey), taskbarHotKey, HotKeyParser.DefaultTaskbarHotKey, reportInvalid);
            if (main.Equals(taskbar))
            {
                reportInvalid(nameof(Settings.TaskbarHotKey), taskbarHotKey, "Same as the main hotkey.");
                main = defaults.MainHotKey;
                taskbar = defaults.TaskbarHotKey;
            }

            return new UserSettingsSnapshot(size, alphabet, main, taskbar, style);
        }

        private static HotKeyCombination ParseHotKey(
            string name, string value, string defaultValue, Action<string, string, string> reportInvalid)
        {
            string error;
            var combination = HotKeyParser.ParseOrDefault(value, defaultValue, out error);
            if (error != null)
            {
                reportInvalid(name, value, error);
            }
            return combination;
        }

        private void ReportInvalid(string name, string value, string error)
        {
            if (_reportedInvalid.Add(name + "\n" + value))
            {
                Trace.TraceWarning("Settings: invalid {0} \"{1}\" ({2}); using the default", name, value, error);
            }
        }
    }
}
