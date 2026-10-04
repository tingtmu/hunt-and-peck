using System.Windows.Forms;
using HuntAndPeck.Configuration;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services.Interfaces
{
    internal class HotKey
    {
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
            return new HotKeyCombination(Modifier, Keys).ToString();
        }
    }
}
