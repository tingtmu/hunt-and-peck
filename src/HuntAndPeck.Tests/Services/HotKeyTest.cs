using System.Windows.Forms;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class HotKeyTest
    {
        [Theory]
        [InlineData(KeyModifier.Alt, Keys.OemSemicolon, "Alt+;")]
        [InlineData(KeyModifier.Control, Keys.OemSemicolon, "Ctrl+;")]
        [InlineData(KeyModifier.Alt | KeyModifier.Shift, Keys.OemSemicolon, "Alt+Shift+;")]
        [InlineData(KeyModifier.Shift | KeyModifier.Control | KeyModifier.Windows | KeyModifier.Alt, Keys.F5, "Ctrl+Alt+Shift+Win+F5")]
        [InlineData(KeyModifier.Windows, Keys.A, "Win+A")]
        [InlineData(KeyModifier.Control, Keys.D7, "Ctrl+7")]
        [InlineData(KeyModifier.Control, Keys.OemQuotes, "Ctrl+'")]
        [InlineData(KeyModifier.Alt | KeyModifier.NoRepeat, Keys.Space, "Alt+Space")]
        public void ToString_IsReadable(KeyModifier modifier, Keys key, string expected)
        {
            var hotKey = new HotKey { Modifier = modifier, Keys = key };

            Assert.Equal(expected, hotKey.ToString());
        }
    }
}
