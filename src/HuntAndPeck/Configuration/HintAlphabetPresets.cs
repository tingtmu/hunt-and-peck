using System;
using System.Collections.Generic;
using System.Linq;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// A named hint alphabet offered in Options
    /// </summary>
    internal sealed class HintAlphabetPreset
    {
        public HintAlphabetPreset(string name, string letters)
        {
            Name = name;
            Letters = letters;
        }

        /// <summary>The name shown in Options</summary>
        public string Name { get; }

        /// <summary>The letters in upper case, or null for <see cref="HintAlphabetPresets.Custom"/></summary>
        public string Letters { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// The hint alphabet presets for common keyboard layouts. Each lists the home row left to right,
    /// then the top-row keys next to it, so the most comfortable keys make the shortest hints.
    /// </summary>
    internal static class HintAlphabetPresets
    {
        /// <summary>Stands for any letters that are not one of <see cref="All"/>; it has no letters of its own</summary>
        public static readonly HintAlphabetPreset Custom = new HintAlphabetPreset("Custom", null);

        /// <summary>The presets, the default (<see cref="HintAlphabet.Default"/>) first</summary>
        public static readonly IReadOnlyList<HintAlphabetPreset> All = new[]
        {
            new HintAlphabetPreset("QWERTY home + top row", HintAlphabet.Default),
            new HintAlphabetPreset("QWERTY home row only", "ASDFGHJKL"),
            new HintAlphabetPreset("Vimium classic", "SADFJKLEWCMPGH"),
            new HintAlphabetPreset("Colemak", "ARSTDHNEIOWFPLUY"),
            new HintAlphabetPreset("Dvorak", "AOEUIDHTNSPYFGCRL")
        };

        /// <summary>
        /// Finds the preset with exactly these letters, in this order. Case and surrounding whitespace are ignored.
        /// </summary>
        /// <param name="letters">The alphabet to look up; may be null or invalid</param>
        /// <returns>The matching preset, else <see cref="Custom"/></returns>
        public static HintAlphabetPreset Find(string letters)
        {
            var trimmed = (letters ?? string.Empty).Trim();
            return All.FirstOrDefault(x => string.Equals(x.Letters, trimmed, StringComparison.OrdinalIgnoreCase)) ?? Custom;
        }
    }
}
