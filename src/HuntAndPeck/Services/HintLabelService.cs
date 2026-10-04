using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HuntAndPeck.Configuration;
using HuntAndPeck.Extensions;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    internal class HintLabelService : IHintLabelService
    {
        private readonly char[] _hintCharacters;

        /// <summary>
        /// Uses the default alphabet, <see cref="HintAlphabet.Default"/>
        /// </summary>
        public HintLabelService()
            : this(HintAlphabet.Default)
        {
        }

        /// <param name="alphabet">The label letters; must pass <see cref="HintAlphabet.TryNormalize"/></param>
        /// <exception cref="ArgumentException">The alphabet is invalid</exception>
        public HintLabelService(string alphabet)
        {
            string normalized;
            string error;
            if (!HintAlphabet.TryNormalize(alphabet, out normalized, out error))
            {
                throw new ArgumentException(error, nameof(alphabet));
            }
            _hintCharacters = normalized.ToCharArray();
        }

        /// <summary>
        /// Gets available hint strings
        /// </summary>
        /// <remarks>Adapted from vimium to give a consistent experience, see https://github.com/philc/vimium/blob/master/content_scripts/link_hints.js </remarks>
        /// <param name="hintCount">The number of hints</param>
        /// <returns>A list of hint strings</returns>
        public IList<string> GetHintStrings(int hintCount)
        {
            var hintStrings = new List<string>();
            if (hintCount <= 0)
            {
                return hintStrings;
            }

            var hintCharacters = _hintCharacters;
            var digitsNeeded = DigitsNeeded(hintCount, hintCharacters.Length);

            var wholeHintCount = (int)Math.Pow(hintCharacters.Length, digitsNeeded);
            var shortHintCount = (wholeHintCount - hintCount) / hintCharacters.Length;
            var longHintCount = hintCount - shortHintCount;

            var longHintPrefixCount = wholeHintCount / hintCharacters.Length - shortHintCount;
            for (int i = 0, j = 0; i < longHintCount; ++i, ++j)
            {
                hintStrings.Add(new string(NumberToHintString(j, hintCharacters, digitsNeeded).Reverse().ToArray()));
                if (longHintPrefixCount > 0 && (i + 1) % longHintPrefixCount == 0)
                {
                    j += shortHintCount;
                }
            }

            if (digitsNeeded > 1)
            {
                for (var i = 0; i < shortHintCount; ++i)
                {
                    hintStrings.Add(new string(NumberToHintString(i + longHintPrefixCount, hintCharacters, digitsNeeded - 1).Reverse().ToArray()));
                }
            }

            return hintStrings.ToList();
        }

        /// <summary>
        /// The smallest number of digits d with base^d >= count, i.e. ceil(log(count) / log(base)) computed with
        /// integers: the floating point division can land just above a whole number (e.g. log(8) / log(2)),
        /// giving one digit too many and labels that are prefixes of others
        /// </summary>
        private static int DigitsNeeded(int count, int numberBase)
        {
            var digits = 0;
            for (long capacity = 1; capacity < count; capacity *= numberBase)
            {
                ++digits;
            }
            return digits;
        }

        /// <summary>
        /// Converts a number like "8" into a hint string like "JK". This is used to sequentially generate all of the
        /// hint text. The hint string will be "padded with zeroes" to ensure its length is >= numHintDigits.
        /// </summary>
        /// <remarks>Adapted from vimium to give a consistent experience, see https://github.com/philc/vimium/blob/master/content_scripts/link_hints.js</remarks>
        /// <param name="number">The number</param>
        /// <param name="characterSet">The set of characters</param>
        /// <param name="noHintDigits">The number of hint digits</param>
        /// <returns>A hint string</returns>
        private string NumberToHintString(int number, char[] characterSet, int noHintDigits = 0)
        {
            var divisor = characterSet.Length;
            var hintString = new StringBuilder();

            do
            {
                var remainder = number % divisor;
                hintString.Insert(0, characterSet[remainder]);
                number -= remainder;
                number /= (int)Math.Floor((double)divisor);
            } while (number > 0);

            // Pad the hint string we're returning so that it matches numHintDigits.
            // Note: the loop body changes hintString.length, so the original length must be cached!
            var length = hintString.Length;
            for (var i = 0; i < (noHintDigits - length); ++i)
            {
                hintString.Insert(0, characterSet[0]);
            }

            return hintString.ToString();
        }
    }
}
