using System;
using System.Collections.Generic;
using System.Windows;

namespace HuntAndPeck.Models
{
    public class HintSession
    {
        /// <summary>
        /// The hints
        /// </summary>
        public IList<Hint> Hints { get; set; }

        /// <summary>
        /// Owning window for the hints
        /// </summary>
        public IntPtr OwningWindow { get; set; }

        /// <summary>
        /// Bounds of the owning window in physical screen pixels
        /// </summary>
        public Rect OwningWindowBounds { get; set; }

        /// <summary>
        /// Window that must own the overlay, else IntPtr.Zero. Set for a revealed auto-hide taskbar: it stays
        /// shown while a window it owns is in the foreground, and would hide under an unowned overlay.
        /// </summary>
        public IntPtr OverlayOwner { get; set; }

        /// <summary>
        /// Window to give the foreground back to if the overlay closes without invoking a hint, else IntPtr.Zero
        /// </summary>
        public IntPtr ForegroundToRestore { get; set; }

        /// <summary>
        /// A copy of this session whose overlay is owned by <paramref name="overlayOwner"/>
        /// </summary>
        public HintSession WithOverlayOwner(IntPtr overlayOwner, IntPtr foregroundToRestore)
        {
            return new HintSession
            {
                Hints = Hints,
                OwningWindow = OwningWindow,
                OwningWindowBounds = OwningWindowBounds,
                OverlayOwner = overlayOwner,
                ForegroundToRestore = foregroundToRestore,
            };
        }
    }
}
