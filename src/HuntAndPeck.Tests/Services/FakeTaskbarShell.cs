using System;
using System.Collections.Generic;
using System.Windows;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Tests.Services
{
    /// <summary>
    /// Taskbar whose position is set by the test: <see cref="Shown"/> places it at <see cref="ShownBounds"/>,
    /// else auto-hidden below it
    /// </summary>
    internal sealed class FakeTaskbarShell : ITaskbarShell
    {
        public static readonly Rect ShownBounds = new Rect(0, 1008, 1920, 72);
        public static readonly Rect HiddenBounds = new Rect(0, 1078, 1920, 72);

        public bool AutoHide { get; set; }

        public bool Hung { get; set; }

        public bool Shown { get; set; }

        public bool ShownBoundsKnown { get; set; } = true;

        public IntPtr Foreground { get; set; }

        public List<IntPtr> Activated { get; } = new List<IntPtr>();

        public int ShownBoundsQueries { get; private set; }

        public bool IsAutoHide() => AutoHide;

        public bool IsHung(IntPtr hWnd) => Hung;

        public bool TryGetShownBounds(IntPtr taskbar, out Rect bounds)
        {
            ShownBoundsQueries++;
            bounds = ShownBoundsKnown ? ShownBounds : Rect.Empty;
            return ShownBoundsKnown;
        }

        public bool TryGetWindowBounds(IntPtr hWnd, out Rect bounds)
        {
            bounds = Shown ? ShownBounds : HiddenBounds;
            return true;
        }

        public IntPtr GetForegroundWindow() => Foreground;

        public void Activate(IntPtr hWnd) => Activated.Add(hWnd);
    }
}
