using System;
using System.Windows;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// What <see cref="BarClassifier"/> needs to know about a top-level window
    /// </summary>
    internal sealed class BarCandidate
    {
        public IntPtr Handle { get; set; }

        public string ClassName { get; set; }

        /// <summary>Window bounds, physical screen pixels</summary>
        public Rect Bounds { get; set; }

        /// <summary>Bounds of the window's monitor, physical screen pixels</summary>
        public Rect Monitor { get; set; }

        /// <summary>The monitor's scale factor (effective DPI / 96)</summary>
        public double MonitorScale { get; set; } = 1.0;

        /// <summary>Extended window styles (WS_EX_*)</summary>
        public uint ExStyle { get; set; }

        public bool Visible { get; set; }

        /// <summary>Cloaked by DWM: e.g. on another virtual desktop or another tiling window manager workspace</summary>
        public bool Cloaked { get; set; }

        public bool Minimized { get; set; }

        /// <summary>The window belongs to this process (e.g. the overlay)</summary>
        public bool OwnProcess { get; set; }
    }
}
