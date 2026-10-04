using System;
using System.Collections.Generic;
using System.Windows.Forms;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Parses and validates hotkey text such as "Alt+OemSemicolon", "Ctrl+Shift+F5" or "Alt+;"
    /// </summary>
    internal static class HotKeyParser
    {
        public const string DefaultMainHotKey = "Alt+OemSemicolon";
        public const string DefaultTaskbarHotKey = "Ctrl+OemSemicolon";

        private const KeyModifier NonShiftModifiers = KeyModifier.Alt | KeyModifier.Control | KeyModifier.Windows;

        private static readonly Dictionary<string, KeyModifier> ModifierNames =
            new Dictionary<string, KeyModifier>(StringComparer.OrdinalIgnoreCase)
            {
                { "Alt", KeyModifier.Alt },
                { "Ctrl", KeyModifier.Control },
                { "Control", KeyModifier.Control },
                { "Shift", KeyModifier.Shift },
                { "Win", KeyModifier.Windows },
                { "Windows", KeyModifier.Windows },
            };

        private static readonly HashSet<Keys> UnusableKeys = new HashSet<Keys>
        {
            Keys.None,
            Keys.LButton, Keys.RButton, Keys.MButton, Keys.XButton1, Keys.XButton2,
            Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
            Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
            Keys.Menu, Keys.LMenu, Keys.RMenu,
            Keys.LWin, Keys.RWin,
            // Pseudo keys that are not real key presses
            Keys.ProcessKey, Keys.Packet, Keys.NoName,
        };

        /// <summary>Highest real virtual key code (VK_OEM_CLEAR); 0xFF and above are not keys</summary>
        private const int MaxKeyCode = 0xFE;

        /// <summary>
        /// Parses and validates hotkey text: modifiers (Alt, Ctrl, Shift, Win) and a key, joined by "+"
        /// </summary>
        /// <param name="text">The text to parse</param>
        /// <param name="combination">The parsed combination, else null</param>
        /// <param name="error">Why the text is invalid, else null</param>
        public static bool TryParse(string text, out HotKeyCombination combination, out string error)
        {
            combination = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "No hotkey given.";
                return false;
            }

            var parts = text.Split('+');
            KeyModifier modifiers;
            if (!TryParseModifiers(parts, out modifiers, out error))
            {
                return false;
            }

            var keyName = parts[parts.Length - 1].Trim();
            Keys key;
            if (!KeyNames.TryParse(keyName, out key))
            {
                error = string.Format("Unknown key \"{0}\".", keyName);
                return false;
            }

            var candidate = new HotKeyCombination(modifiers, key);
            if (!Validate(candidate, out error))
            {
                return false;
            }
            combination = candidate;
            return true;
        }

        /// <summary>
        /// Checks that the combination can be used as a global hotkey: a real, non-modifier key, and Ctrl, Alt
        /// or Win unless the key is F1-F24 (Shift alone would take over typing), and not F12 alone (reserved for
        /// debuggers)
        /// </summary>
        public static bool Validate(HotKeyCombination combination, out string error)
        {
            error = null;
            var key = combination.Key;
            if (!IsRealKey(key))
            {
                error = "Choose a key other than a modifier key.";
            }
            else if (key == Keys.F12 && combination.Modifiers == 0)
            {
                error = "F12 on its own is reserved for debuggers; add Ctrl, Alt, Shift or Win.";
            }
            else if ((combination.Modifiers & ~(NonShiftModifiers | KeyModifier.Shift)) != 0)
            {
                error = "Only Alt, Ctrl, Shift and Win can be used as modifiers.";
            }
            else if ((combination.Modifiers & NonShiftModifiers) == 0 && !IsFunctionKey(key))
            {
                error = string.Format("{0}: add Ctrl, Alt or Win (only F1-F24 work without them).", combination);
            }
            return error == null;
        }

        /// <summary>
        /// Parses stored hotkey text, falling back to the default for missing or invalid text
        /// </summary>
        /// <param name="text">The stored text</param>
        /// <param name="defaultText">The default, which must be valid</param>
        /// <param name="error">Why the stored text was invalid, else null</param>
        public static HotKeyCombination ParseOrDefault(string text, string defaultText, out string error)
        {
            HotKeyCombination combination;
            if (TryParse(text, out combination, out error))
            {
                return combination;
            }

            string defaultError;
            if (!TryParse(defaultText, out combination, out defaultError))
            {
                throw new ArgumentException("Invalid default hotkey: " + defaultError, nameof(defaultText));
            }
            return combination;
        }

        private static bool TryParseModifiers(string[] parts, out KeyModifier modifiers, out string error)
        {
            modifiers = 0;
            error = null;
            for (var i = 0; i < parts.Length - 1; ++i)
            {
                var name = parts[i].Trim();
                KeyModifier modifier;
                if (!ModifierNames.TryGetValue(name, out modifier))
                {
                    error = string.Format("Unknown modifier \"{0}\" (use Alt, Ctrl, Shift or Win).", name);
                    return false;
                }
                if (modifiers.HasFlag(modifier))
                {
                    error = string.Format("Modifier \"{0}\" is given twice.", name);
                    return false;
                }
                modifiers |= modifier;
            }
            return true;
        }

        /// <summary>
        /// A defined virtual key code 0x01-0xFE without modifier flags (rejects KeyCode, Modifiers etc.) that is
        /// not a modifier key, mouse button or pseudo key
        /// </summary>
        private static bool IsRealKey(Keys key)
        {
            var code = (int)key;
            return code >= 1 && code <= MaxKeyCode && !UnusableKeys.Contains(key) && Enum.IsDefined(typeof(Keys), key);
        }

        private static bool IsFunctionKey(Keys key)
        {
            return key >= Keys.F1 && key <= Keys.F24;
        }
    }
}
