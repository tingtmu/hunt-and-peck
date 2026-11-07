using System.Linq;
using HuntAndPeck.Configuration;
using Xunit;

namespace HuntAndPeck.Tests.Configuration
{
    public class HintAlphabetPresetsTest
    {
        [Fact]
        public void All_EveryPresetIsAValidAlphabetInUpperCase()
        {
            foreach (var preset in HintAlphabetPresets.All)
            {
                string alphabet;
                string error;
                Assert.True(HintAlphabet.TryNormalize(preset.Letters, out alphabet, out error), preset.Name + ": " + error);
                Assert.Equal(preset.Letters, alphabet);
            }
        }

        [Fact]
        public void All_NamesAndLettersAreUnique()
        {
            Assert.Equal(HintAlphabetPresets.All.Count, HintAlphabetPresets.All.Select(x => x.Name).Distinct().Count());
            Assert.Equal(HintAlphabetPresets.All.Count, HintAlphabetPresets.All.Select(x => x.Letters).Distinct().Count());
            Assert.DoesNotContain(HintAlphabetPresets.All, x => x.Name == HintAlphabetPresets.Custom.Name);
        }

        [Fact]
        public void All_ListsTheDefaultFirst()
        {
            Assert.Equal("QWERTY home + top row", HintAlphabetPresets.All[0].Name);
            Assert.Equal(HintAlphabet.Default, HintAlphabetPresets.All[0].Letters);
        }

        [Theory]
        [InlineData("ASDFGHJKLQWERUIO", "QWERTY home + top row")]
        [InlineData("ASDFGHJKL", "QWERTY home row only")]
        [InlineData("SADFJKLEWCMPGH", "Vimium classic")]
        [InlineData("ARSTDHNEIOWFPLUY", "Colemak")]
        [InlineData("AOEUIDHTNSPYFGCRL", "Dvorak")]
        [InlineData("asdfghjklqweruio", "QWERTY home + top row")]
        [InlineData(" aoeuidhtnspyfgcrl ", "Dvorak")]
        public void Find_KnownLetters_ReturnsPresetIgnoringCaseAndWhitespace(string letters, string name)
        {
            Assert.Equal(name, HintAlphabetPresets.Find(letters).Name);
        }

        [Theory]
        [InlineData("JK")]
        [InlineData("ASDFGHJKLQWERUI")]
        [InlineData("SDAFJKLEWCMPGH")]
        [InlineData("")]
        [InlineData(null)]
        public void Find_UnknownLetters_ReturnsCustom(string letters)
        {
            Assert.Same(HintAlphabetPresets.Custom, HintAlphabetPresets.Find(letters));
        }
    }
}
