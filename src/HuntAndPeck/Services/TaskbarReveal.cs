using System;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Outcome of <see cref="TaskbarRevealer.RevealAsync"/>
    /// </summary>
    internal sealed class TaskbarReveal
    {
        /// <summary>The taskbar is always visible: nothing was done</summary>
        public static readonly TaskbarReveal NotNeeded = new TaskbarReveal(false, IntPtr.Zero);

        public TaskbarReveal(bool revealed, IntPtr previousForeground)
        {
            Revealed = revealed;
            PreviousForeground = previousForeground;
        }

        /// <summary>
        /// True if the auto-hide taskbar was brought into view (and made the foreground window) for the session,
        /// so the overlay must keep it in view and the previous foreground window may need restoring afterwards
        /// </summary>
        public bool Revealed { get; private set; }

        /// <summary>The foreground window before the reveal, else IntPtr.Zero</summary>
        public IntPtr PreviousForeground { get; private set; }
    }
}
