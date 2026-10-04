using System.Windows.Forms;
using HuntAndPeck.Configuration;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Properties;
using Xunit;

namespace HuntAndPeck.Tests.Configuration
{
    public class HotKeyParserTest
    {
        [Theory]
        [InlineData("Alt+OemSemicolon", KeyModifier.Alt, Keys.OemSemicolon)]
        [InlineData("Ctrl+OemSemicolon", KeyModifier.Control, Keys.OemSemicolon)]
        [InlineData("Alt+;", KeyModifier.Alt, Keys.OemSemicolon)]
        [InlineData("alt+oem1", KeyModifier.Alt, Keys.OemSemicolon)]
        [InlineData(" Control + Shift + F5 ", KeyModifier.Control | KeyModifier.Shift, Keys.F5)]
        [InlineData("Win+A", KeyModifier.Windows, Keys.A)]
        [InlineData("Windows+a", KeyModifier.Windows, Keys.A)]
        [InlineData("Ctrl+7", KeyModifier.Control, Keys.D7)]
        [InlineData("Ctrl+D7", KeyModifier.Control, Keys.D7)]
        [InlineData("Ctrl+Alt+Space", KeyModifier.Control | KeyModifier.Alt, Keys.Space)]
        [InlineData("Ctrl+Enter", KeyModifier.Control, Keys.Return)]
        [InlineData("Ctrl+PageUp", KeyModifier.Control, Keys.PageUp)]
        [InlineData("Ctrl+=", KeyModifier.Control, Keys.Oemplus)]
        [InlineData("F8", (KeyModifier)0, Keys.F8)]
        [InlineData("Shift+F12", KeyModifier.Shift, Keys.F12)]
        [InlineData("Ctrl+NumPad5", KeyModifier.Control, Keys.NumPad5)]
        [InlineData("Ctrl+F12", KeyModifier.Control, Keys.F12)]
        [InlineData("Ctrl+OemClear", KeyModifier.Control, Keys.OemClear)]
        [InlineData("Ctrl+Snapshot", KeyModifier.Control, Keys.PrintScreen)]
        [InlineData("Ctrl+HangulMode", KeyModifier.Control, Keys.KanaMode)]
        // WPF Key spellings
        [InlineData("Alt+OemPlus", KeyModifier.Alt, Keys.Oemplus)]
        [InlineData("Ctrl+Back", KeyModifier.Control, Keys.Back)]
        [InlineData("Ctrl+OemTilde", KeyModifier.Control, Keys.Oemtilde)]
        public void TryParse_Valid(string text, KeyModifier modifiers, Keys key)
        {
            HotKeyCombination combination;
            string error;

            Assert.True(HotKeyParser.TryParse(text, out combination, out error), error);
            Assert.Null(error);
            Assert.Equal(new HotKeyCombination(modifiers, key), combination);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Alt+")]
        [InlineData("Alt+Nope")]
        [InlineData("Hyper+A")]
        [InlineData("Alt+Alt+A")]
        [InlineData("Alt++A")]
        [InlineData("A")]
        [InlineData("Shift+A")]
        [InlineData("OemSemicolon")]
        [InlineData("Alt+Shift")]
        [InlineData("Alt+ShiftKey")]
        [InlineData("Ctrl+LWin")]
        [InlineData("Ctrl+LeftAlt")]
        [InlineData("Ctrl+LButton")]
        [InlineData("Alt+186")]
        [InlineData("Alt+A, B")]
        [InlineData("Alt+None")]
        [InlineData("Alt+Modifiers")]
        [InlineData("Alt+KeyCode")]
        [InlineData("Alt+Packet")]
        [InlineData("Alt+ProcessKey")]
        [InlineData("Alt+NoName")]
        [InlineData("F12")]
        public void TryParse_Invalid(string text)
        {
            HotKeyCombination combination;
            string error;

            Assert.False(HotKeyParser.TryParse(text, out combination, out error));
            Assert.Null(combination);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Theory]
        [InlineData(KeyModifier.Alt, Keys.OemSemicolon, "Alt+OemSemicolon")]
        [InlineData(KeyModifier.Control, Keys.OemSemicolon, "Ctrl+OemSemicolon")]
        [InlineData(KeyModifier.Windows | KeyModifier.Shift | KeyModifier.Alt | KeyModifier.Control, Keys.F5, "Ctrl+Alt+Shift+Win+F5")]
        [InlineData(KeyModifier.Control, Keys.Return, "Ctrl+Enter")]
        [InlineData(KeyModifier.Control, Keys.PageDown, "Ctrl+PageDown")]
        [InlineData(KeyModifier.Control, Keys.OemQuotes, "Ctrl+OemQuotes")]
        [InlineData(KeyModifier.Control, Keys.D0, "Ctrl+D0")]
        [InlineData(KeyModifier.Control, Keys.Snapshot, "Ctrl+PrintScreen")]
        [InlineData(KeyModifier.Control, Keys.HangulMode, "Ctrl+KanaMode")]
        public void ToStorageString_IsStableAndRoundTrips(KeyModifier modifiers, Keys key, string expected)
        {
            var combination = new HotKeyCombination(modifiers, key);

            var text = combination.ToStorageString();

            Assert.Equal(expected, text);
            HotKeyCombination parsed;
            string error;
            Assert.True(HotKeyParser.TryParse(text, out parsed, out error), error);
            Assert.Equal(combination, parsed);
        }

        [Theory]
        [InlineData(KeyModifier.Alt, Keys.OemSemicolon)]
        [InlineData(KeyModifier.Control | KeyModifier.Shift, Keys.OemQuestion)]
        [InlineData(KeyModifier.Windows, Keys.D3)]
        [InlineData(KeyModifier.Alt, Keys.Escape)]
        [InlineData(KeyModifier.Alt, Keys.PageUp)]
        public void DisplayText_RoundTrips(KeyModifier modifiers, Keys key)
        {
            var combination = new HotKeyCombination(modifiers, key);

            HotKeyCombination parsed;
            string error;
            Assert.True(HotKeyParser.TryParse(combination.ToString(), out parsed, out error), error);
            Assert.Equal(combination, parsed);
        }

        [Fact]
        public void Validate_RejectsNoRepeatFlag()
        {
            string error;
            Assert.False(HotKeyParser.Validate(new HotKeyCombination(KeyModifier.Alt | KeyModifier.NoRepeat, Keys.A), out error));
        }

        [Fact]
        public void ParseOrDefault_InvalidText_ReturnsDefaultAndError()
        {
            string error;

            var combination = HotKeyParser.ParseOrDefault("Shift+Q", HotKeyParser.DefaultMainHotKey, out error);

            Assert.Equal(new HotKeyCombination(KeyModifier.Alt, Keys.OemSemicolon), combination);
            Assert.NotNull(error);
        }

        [Fact]
        public void Defaults_MatchPreviouslyHardcodedHotKeys_AndSettingsDefaults()
        {
            Assert.Equal(new HotKeyCombination(KeyModifier.Alt, Keys.OemSemicolon), UserSettingsSnapshot.Defaults.MainHotKey);
            Assert.Equal(new HotKeyCombination(KeyModifier.Control, Keys.OemSemicolon), UserSettingsSnapshot.Defaults.TaskbarHotKey);
            Assert.Equal(HotKeyParser.DefaultMainHotKey, Settings.Default.Properties["MainHotKey"].DefaultValue);
            Assert.Equal(HotKeyParser.DefaultTaskbarHotKey, Settings.Default.Properties["TaskbarHotKey"].DefaultValue);
            Assert.Equal(HintAlphabet.Default, Settings.Default.Properties["HintAlphabet"].DefaultValue);
        }
    }
}
