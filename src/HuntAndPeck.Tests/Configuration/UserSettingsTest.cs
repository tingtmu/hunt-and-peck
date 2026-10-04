using System.Collections.Generic;
using System.Windows.Forms;
using HuntAndPeck.Configuration;
using HuntAndPeck.NativeMethods;
using Xunit;

namespace HuntAndPeck.Tests.Configuration
{
    public class UserSettingsTest
    {
        private readonly List<string> _invalid = new List<string>();

        [Fact]
        public void Parse_ValidValues()
        {
            var settings = Parse("18", "asdf", "Ctrl+Alt+H", "Win+F2");

            Assert.Equal(18, settings.FontSize);
            Assert.Equal("ASDF", settings.HintAlphabet);
            Assert.Equal(new HotKeyCombination(KeyModifier.Control | KeyModifier.Alt, Keys.H), settings.MainHotKey);
            Assert.Equal(new HotKeyCombination(KeyModifier.Windows, Keys.F2), settings.TaskbarHotKey);
            Assert.Empty(_invalid);
        }

        [Fact]
        public void Parse_InvalidValues_FallBackToDefaultsAndAreReported()
        {
            var settings = Parse("huge", "AAB", "Shift+X", "Ctrl+Bogus");

            var defaults = UserSettingsSnapshot.Defaults;
            Assert.Equal(defaults.FontSize, settings.FontSize);
            Assert.Equal(defaults.HintAlphabet, settings.HintAlphabet);
            Assert.Equal(defaults.MainHotKey, settings.MainHotKey);
            Assert.Equal(defaults.TaskbarHotKey, settings.TaskbarHotKey);
            Assert.Equal(new[] { "FontSize", "HintAlphabet", "MainHotKey", "TaskbarHotKey" }, _invalid);
        }

        [Fact]
        public void Parse_MissingValues_FallBackToDefaults()
        {
            var settings = Parse(null, null, null, null);

            Assert.Equal(UserSettingsSnapshot.Defaults.MainHotKey, settings.MainHotKey);
            Assert.Equal(4, _invalid.Count);
        }

        [Fact]
        public void Parse_SameMainAndTaskbarHotKey_FallsBackToDefaultHotKeys()
        {
            var settings = Parse("14", "SADF", "Ctrl+J", "Ctrl+J");

            Assert.Equal(UserSettingsSnapshot.Defaults.MainHotKey, settings.MainHotKey);
            Assert.Equal(UserSettingsSnapshot.Defaults.TaskbarHotKey, settings.TaskbarHotKey);
            Assert.Equal(new[] { "TaskbarHotKey" }, _invalid);
        }

        private UserSettingsSnapshot Parse(string fontSize, string alphabet, string main, string taskbar)
        {
            return UserSettings.Parse(fontSize, alphabet, main, taskbar, (name, value, error) => _invalid.Add(name));
        }
    }
}
