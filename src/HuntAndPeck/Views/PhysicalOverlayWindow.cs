using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HuntAndPeck.Extensions;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.ViewModels;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Foreground window that exactly covers a target window whose bounds are given in physical pixels
    /// (the DataContext must implement <see cref="IOverlayBounds"/>).
    /// </summary>
    /// <remarks>
    /// The window is placed with SetWindowPos in physical pixels, so its position does not depend on the DPI
    /// of the monitor WPF initially created it on. The content is laid out in physical pixels too: it is
    /// scaled by 1 / device scale, so one content unit is one device pixel at any DPI. The scale is
    /// re-applied when the window's DPI changes (e.g. it lands on a monitor with a different scale).
    /// </remarks>
    public class PhysicalOverlayWindow : ForegroundWindow
    {
        private bool _placementPending;
        private bool _missingBoundsLogged;

        public PhysicalOverlayWindow()
        {
            // Placed both here and in OnSourceInitialized: SourceInitialized moves the hidden HWND onto the
            // target's monitor before it is shown (so it gets that monitor's DPI early), and Loaded re-applies
            // after WPF's initial show/size logic, which may otherwise adjust the window.
            Loaded += (sender, e) => ApplyPlacement(CurrentDeviceScale());
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyPlacement(CurrentDeviceScale());
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            ScaleContent(newDpi.DpiScaleX);
            QueuePlacement();
        }

        /// <summary>
        /// Restores the exact physical bounds once a DPI change has been fully processed (WPF may resize the
        /// window to the system suggested rectangle after OnDpiChanged). Placing synchronously inside the DPI
        /// change could ping-pong between monitors, so at most one deferred placement is queued at a time.
        /// </summary>
        private void QueuePlacement()
        {
            if (_placementPending)
            {
                return;
            }

            _placementPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _placementPending = false;
                ApplyPlacement(CurrentDeviceScale());
            }));
        }

        private IntPtr Handle => new WindowInteropHelper(this).Handle;

        private double CurrentDeviceScale()
        {
            return DpiHelper.GetDeviceScale(Handle, this);
        }

        private void ApplyPlacement(double scale)
        {
            var overlay = DataContext as IOverlayBounds;
            if (overlay == null || overlay.Bounds.IsEmpty)
            {
                if (!_missingBoundsLogged)
                {
                    _missingBoundsLogged = true;
                    Trace.TraceWarning("Overlay has no target bounds; leaving it unplaced");
                }
                return;
            }

            ScaleContent(scale);
            PlaceWindow(OverlayPlacement.AvoidCoveringMonitor(overlay.Bounds), scale);
        }

        private static bool IsAlreadyAt(IntPtr hWnd, RECT target)
        {
            var current = new RECT();
            return User32.GetWindowRect(hWnd, ref current) &&
                   current.left == target.left && current.top == target.top &&
                   current.right == target.right && current.bottom == target.bottom;
        }

        private void ScaleContent(double scale)
        {
            var content = Content as FrameworkElement;
            if (content == null)
            {
                return;
            }

            var factor = DpiHelper.PhysicalToDipFactor(scale);
            content.LayoutTransform = new ScaleTransform(factor, factor);
        }

        private void PlaceWindow(Rect physicalBounds, double scale)
        {
            var hWnd = Handle;
            if (hWnd != IntPtr.Zero)
            {
                RECT r = physicalBounds;
                if (IsAlreadyAt(hWnd, r))
                {
                    return;
                }

                const uint flags = Constants.SWP_NOZORDER | Constants.SWP_NOACTIVATE;
                if (User32.SetWindowPos(hWnd, IntPtr.Zero, r.left, r.top, r.right - r.left, r.bottom - r.top, flags))
                {
                    return;
                }

                Trace.TraceWarning("SetWindowPos failed with error {0}; positioning overlay in DIPs", Marshal.GetLastWin32Error());
            }

            // Fallback: WPF positions in DIPs of the window's current monitor
            var dip = DpiHelper.PhysicalToDip(physicalBounds, scale);
            Left = dip.Left;
            Top = dip.Top;
            Width = dip.Width;
            Height = dip.Height;
        }
    }
}
