using HuntAndPeck.Configuration;
using Xunit;

namespace HuntAndPeck.Tests.Configuration
{
    public class HintAlphabetTest
    {
        [Theory]
        [InlineData("SADFJKLEWCMPGH", "SADFJKLEWCMPGH")]
        [InlineData("asdf", "ASDF")]
        [InlineData("  jk ", "JK")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
        public void TryNormalize_Valid(string text, string expected)
        {
            string alphabet;
            string error;

            Assert.True(HintAlphabet.TryNormalize(text, out alphabet, out error), error);
            Assert.Equal(expected, alphabet);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("ASDA")]
        [InlineData("aA")]
        [InlineData("AS DF")]
        [InlineData("ASD1")]
        [InlineData("ASD;")]
        [InlineData("ASDé")]
        [InlineData("ASDı")]
        public void TryNormalize_Invalid(string text)
        {
            string alphabet;
            string error;

            Assert.False(HintAlphabet.TryNormalize(text, out alphabet, out error));
            Assert.Null(alphabet);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TryNormalize_TooLong_IsInvalid()
        {
            // 27 letters can't be distinct, but length is checked and reported too
            string alphabet;
            string error;

            Assert.False(HintAlphabet.TryNormalize("ABCDEFGHIJKLMNOPQRSTUVWXYZA", out alphabet, out error));
        }
    }
}
