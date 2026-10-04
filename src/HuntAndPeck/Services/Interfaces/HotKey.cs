using System.Collections.Generic;
using System.Windows.Forms;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services.Interfaces
{
    internal class HotKey
    {
        private static readonly Dictionary<Keys, string> KeyDisplayNames = new Dictionary<Keys, string>
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

        public KeyModifier Modifier { get; set; }
        public Keys Keys { get; set; }

        /// <summary>
        /// Id of the hot key registration
        /// </summary>
        public int RegistrationId { get; set; }

        /// <summary>
        /// Whether the last registration attempt succeeded
        /// </summary>
        public bool IsRegistered { get; set; }

        /// <summary>
        /// Human readable text, e.g. "Alt+;" or "Ctrl+Shift+F5"
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>();
            if (Modifier.HasFlag(KeyModifier.Control))
            {
                parts.Add("Ctrl");
            }
            if (Modifier.HasFlag(KeyModifier.Alt))
            {
                parts.Add("Alt");
            }
            if (Modifier.HasFlag(KeyModifier.Shift))
            {
                parts.Add("Shift");
            }
            if (Modifier.HasFlag(KeyModifier.Windows))
            {
                parts.Add("Win");
            }
            parts.Add(KeyDisplayName(Keys));
            return string.Join("+", parts);
        }

        private static string KeyDisplayName(Keys key)
        {
            string name;
            if (KeyDisplayNames.TryGetValue(key, out name))
            {
                return name;
            }
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return ((char)('0' + (key - Keys.D0))).ToString();
            }
            return key.ToString();
        }
    }
}
