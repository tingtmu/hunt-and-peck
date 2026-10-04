using System;
using System.Collections.Generic;
using HuntAndPeck.Services;
using Xunit;
using System.Linq;

namespace HuntAndPeck.Tests.Services
{
    public class HintLabelServiceTest
    {
        [Fact]
        public void GetHintStrings_UniqueStrings()
        {
            // Arrange
            const int hintCount = 256;
            var hintService = new HintLabelService();

            // Act
            var hints = hintService.GetHintStrings(hintCount);

            // Assert
            Assert.Equal(hintCount, hints.Distinct().Count());
        }

        [Theory]
        [InlineData("SADFJKLEWCMPGH")]
        [InlineData("JK")]
        [InlineData("ASDF")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
        public void GetHintStrings_CustomAlphabet_UniquePrefixFreeAndOnlyAlphabetLetters(string alphabet)
        {
            var service = new HintLabelService(alphabet);

            foreach (var count in new[] { 1, 2, 3, 7, 8, 9, 16, 26, 27, 64, 196, 200, 676, 1000 })
            {
                var hints = service.GetHintStrings(count);

                Assert.Equal(count, hints.Count);
                Assert.Equal(count, hints.Distinct().Count());
                Assert.All(hints, h => Assert.All(h, c => Assert.Contains(c, alphabet)));
                AssertPrefixFree(hints);
            }
        }

        [Fact]
        public void GetHintStrings_TwoLetterAlphabet_UsesShortestLabels()
        {
            var service = new HintLabelService("jk");

            Assert.Equal(new[] { "J", "K" }, service.GetHintStrings(2));
            // 8 = 2^3: exactly three letters each (floating point log(8)/log(2) would give four)
            var eight = service.GetHintStrings(8);
            Assert.All(eight, h => Assert.Equal(3, h.Length));
        }

        [Fact]
        public void GetHintStrings_DefaultAlphabet_Unchanged()
        {
            var defaultService = new HintLabelService();
            var explicitService = new HintLabelService("SADFJKLEWCMPGH");

            Assert.Equal(explicitService.GetHintStrings(50), defaultService.GetHintStrings(50));
            Assert.Equal(new[] { "S", "A", "D" }, defaultService.GetHintStrings(3));
        }

        [Theory]
        [InlineData("A")]
        [InlineData("AA")]
        [InlineData("A1")]
        [InlineData("")]
        public void Constructor_InvalidAlphabet_Throws(string alphabet)
        {
            Assert.Throws<ArgumentException>(() => new HintLabelService(alphabet));
        }

        [Fact]
        public void ConfiguredService_UsesCurrentAlphabet()
        {
            var alphabet = "JK";
            var service = new ConfiguredHintLabelService(() => alphabet);

            Assert.Equal(new[] { "J", "K" }, service.GetHintStrings(2));
            alphabet = "QW";
            Assert.Equal(new[] { "Q", "W" }, service.GetHintStrings(2));
        }

        /// <summary>
        /// A label that is a prefix of another can never be selected on its own
        /// </summary>
        private static void AssertPrefixFree(IList<string> hints)
        {
            foreach (var a in hints)
            {
                Assert.DoesNotContain(hints, b => b != a && b.StartsWith(a, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// The default labels are unchanged from before the alphabet became configurable. The integer digit
        /// count only differs from the old floating point ceil(log(n) / log(14)) where that division lands just
        /// above a whole number; for counts 1-195 there is no such count, so no count is excluded. The first
        /// exact power above 14, 196 = 14^2, is outside the range.
        /// </summary>
        [Fact]
        public void GetHintStrings_DefaultAlphabet_MatchesOldImplementation()
        {
            var service = new HintLabelService();
            var differing = new List<int>();

            for (var count = 1; count <= 195; ++count)
            {
                if (!service.GetHintStrings(count).SequenceEqual(OldHintStrings(count)))
                {
                    differing.Add(count);
                }
            }

            Assert.Empty(differing);
        }

        /// <summary>
        /// Copy of the original HintLabelService algorithm (fixed alphabet, floating point digit count)
        /// </summary>
        private static IList<string> OldHintStrings(int hintCount)
        {
            var hintStrings = new List<string>();
            var chars = "SADFJKLEWCMPGH".ToCharArray();
            var digitsNeeded = (int)Math.Ceiling(Math.Log(hintCount) / Math.Log(chars.Length));
            var wholeHintCount = (int)Math.Pow(chars.Length, digitsNeeded);
            var shortHintCount = (wholeHintCount - hintCount) / chars.Length;
            var longHintCount = hintCount - shortHintCount;
            var longHintPrefixCount = wholeHintCount / chars.Length - shortHintCount;
            for (int i = 0, j = 0; i < longHintCount; ++i, ++j)
            {
                hintStrings.Add(new string(OldNumberToHintString(j, chars, digitsNeeded).Reverse().ToArray()));
                if (longHintPrefixCount > 0 && (i + 1) % longHintPrefixCount == 0)
                {
                    j += shortHintCount;
                }
            }
            if (digitsNeeded > 1)
            {
                for (var i = 0; i < shortHintCount; ++i)
                {
                    hintStrings.Add(new string(OldNumberToHintString(i + longHintPrefixCount, chars, digitsNeeded - 1).Reverse().ToArray()));
                }
            }
            return hintStrings;
        }

        private static string OldNumberToHintString(int number, char[] chars, int digits)
        {
            var result = new System.Text.StringBuilder();
            do
            {
                var remainder = number % chars.Length;
                result.Insert(0, chars[remainder]);
                number = (number - remainder) / chars.Length;
            } while (number > 0);
            while (result.Length < digits)
            {
                result.Insert(0, chars[0]);
            }
            return result.ToString();
        }
    }
}
