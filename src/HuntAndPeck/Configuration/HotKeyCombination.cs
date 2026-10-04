using System;
using System.Collections.Generic;
using System.Windows.Forms;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// An immutable key combination (modifiers plus one key), not validated; see <see cref="HotKeyParser.Validate"/>
    /// </summary>
    internal sealed class HotKeyCombination : IEquatable<HotKeyCombination>
    {
        public HotKeyCombination(KeyModifier modifiers, Keys key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public KeyModifier Modifiers { get; }
        public Keys Key { get; }

        /// <summary>
        /// Creates a new, unregistered hotkey for this combination
        /// </summary>
        public HotKey ToHotKey()
        {
            return new HotKey { Modifier = Modifiers, Keys = Key };
        }

        /// <summary>
        /// Whether the hotkey has this combination
        /// </summary>
        public bool Matches(HotKey hotKey)
        {
            return hotKey != null && hotKey.Modifier == Modifiers && hotKey.Keys == Key;
        }

        /// <summary>
        /// Form stored in the settings, e.g. "Alt+OemSemicolon"
        /// </summary>
        public string ToStorageString()
        {
            return Join(KeyNames.StorageName(Key));
        }

        /// <summary>
        /// Human readable text, e.g. "Alt+;"
        /// </summary>
        public override string ToString()
        {
            return Join(KeyNames.DisplayName(Key));
        }

        private string Join(string keyName)
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(KeyModifier.Control))
            {
                parts.Add("Ctrl");
            }
            if (Modifiers.HasFlag(KeyModifier.Alt))
            {
                parts.Add("Alt");
            }
            if (Modifiers.HasFlag(KeyModifier.Shift))
            {
                parts.Add("Shift");
            }
            if (Modifiers.HasFlag(KeyModifier.Windows))
            {
                parts.Add("Win");
            }
            parts.Add(keyName);
            return string.Join("+", parts);
        }

        public bool Equals(HotKeyCombination other)
        {
            return other != null && other.Modifiers == Modifiers && other.Key == Key;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HotKeyCombination);
        }

        public override int GetHashCode()
        {
            return ((int)Modifiers * 397) ^ (int)Key;
        }
    }
}
