using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Names for keys: readable display names (";") and stable storage names ("OemSemicolon")
    /// </summary>
    internal static class KeyNames
    {
        private static readonly Dictionary<Keys, string> DisplayNames = new Dictionary<Keys, string>
        {
            { Keys.OemSemicolon, ";" },
            { Keys.Oemplus, "=" },
            { Keys.Oemcomma, "," },
            { Keys.OemMinus, "-" },
            { Keys.OemPeriod, "." },
            { Keys.OemQuestion, "/" },
            { Keys.Oemtilde, "`" },
            { Keys.OemOpenBrackets, "[" },
            { Keys.OemPipe, "\\" },
            { Keys.OemCloseBrackets, "]" },
            { Keys.OemQuotes, "'" },
            { Keys.Space, "Space" },
            { Keys.Return, "Enter" },
            { Keys.Escape, "Esc" },
        };

        /// <summary>
        /// Several Keys members share a value (e.g. Oem1 and OemSemicolon), for which Enum.ToString may return
        /// either name; these fix the name that is stored
        /// </summary>
        private static readonly Dictionary<Keys, string> StorageNames = new Dictionary<Keys, string>
        {
            { Keys.OemSemicolon, "OemSemicolon" },
            { Keys.OemQuestion, "OemQuestion" },
            { Keys.Oemtilde, "Oemtilde" },
            { Keys.OemOpenBrackets, "OemOpenBrackets" },
            { Keys.OemPipe, "OemPipe" },
            { Keys.OemCloseBrackets, "OemCloseBrackets" },
            { Keys.OemQuotes, "OemQuotes" },
            { Keys.OemBackslash, "OemBackslash" },
            { Keys.Return, "Enter" },
            { Keys.PageUp, "PageUp" },
            { Keys.PageDown, "PageDown" },
            { Keys.CapsLock, "CapsLock" },
            { Keys.PrintScreen, "PrintScreen" }, // = Snapshot
            { Keys.KanaMode, "KanaMode" }, // = HangulMode = HanguelMode
            { Keys.KanjiMode, "KanjiMode" },
            { Keys.IMEAccept, "IMEAccept" },
        };

        private static readonly Regex IdentifierPattern = new Regex("^[A-Za-z][A-Za-z0-9]*$");

        public static string DisplayName(Keys key)
        {
            string name;
            if (DisplayNames.TryGetValue(key, out name))
            {
                return name;
            }
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return ((char)('0' + (key - Keys.D0))).ToString();
            }
            return StorageName(key);
        }

        public static string StorageName(Keys key)
        {
            string name;
            return StorageNames.TryGetValue(key, out name) ? name : key.ToString();
        }

        /// <summary>
        /// Resolves a key name: a display name (";", "7", "Esc"), a System.Windows.Forms.Keys name or a WPF Key
        /// name, case-insensitively. Numeric values and flag combinations are rejected.
        /// </summary>
        /// <returns>False if the name is unknown</returns>
        public static bool TryParse(string name, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var display = DisplayNames.FirstOrDefault(x => string.Equals(x.Value, name, StringComparison.OrdinalIgnoreCase));
            if (display.Value != null)
            {
                key = display.Key;
                return true;
            }
            if (name.Length == 1 && name[0] >= '0' && name[0] <= '9')
            {
                key = Keys.D0 + (name[0] - '0');
                return true;
            }
            if (!IdentifierPattern.IsMatch(name))
            {
                return false;
            }
            return TryParseFormsKey(name, out key) || TryParseWpfKey(name, out key);
        }

        private static bool TryParseFormsKey(string name, out Keys key)
        {
            return Enum.TryParse(name, true, out key) && Enum.IsDefined(typeof(Keys), key);
        }

        private static bool TryParseWpfKey(string name, out Keys key)
        {
            key = Keys.None;
            Key wpfKey;
            if (!Enum.TryParse(name, true, out wpfKey) || !Enum.IsDefined(typeof(Key), wpfKey))
            {
                return false;
            }
            key = (Keys)KeyInterop.VirtualKeyFromKey(wpfKey);
            return key != Keys.None;
        }
    }
}
