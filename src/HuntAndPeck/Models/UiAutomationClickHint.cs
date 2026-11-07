using System;
using System.Windows;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Clicks the centre of the element with a synthesized mouse click; for elements whose UI Automation action does
    /// nothing or fails: Windows 11 taskbar app buttons (their LegacyIAccessible default action reports success but does
    /// nothing), Qt item view cells, a failed pattern action's fallback, or a forced click (Shift: left, Ctrl: right,
    /// Ctrl+Shift: double). A <see cref="MouseAction.Hover"/> only moves the cursor onto the element (Alt): onto
    /// its hint label if the label size is known (<see cref="ActionPoint.LabelCentre"/>), else its centre.
    /// </summary>
    internal class UiAutomationClickHint : UiAutomationHint
    {
        /// <param name="labelSize">About the size of the element's hint label (physical pixels), else empty</param>
        public UiAutomationClickHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle, MouseAction action = MouseAction.LeftClick, Size labelSize = default(Size))
            : base(owningWindow, automationElement, boundingRectangle)
        {
            Action = action;
            LabelSize = labelSize;
        }

        public MouseAction Action { get; }

        /// <summary>About the size of the element's hint label, where a hover lands; empty if unknown (the centre)</summary>
        public Size LabelSize { get; }

        /// <summary>The overlay covers the button, so it must be gone before the click</summary>
        public override bool InvokeAfterOverlayCloses => true;

        /// <summary>Null if this hint already clicks that way</summary>
        public override Hint CreateClickHint(MouseAction action) => action == Action ? null : base.CreateClickHint(action);

        /// <summary>Null if this hint already clicks that way</summary>
        public override Hint CreateClickHint(MouseAction action, Size labelSize) => action == Action ? null : base.CreateClickHint(action, labelSize);

        /// <exception cref="InvalidOperationException">
        /// The element has no visible bounds in its window, or another window covers the click point
        /// </exception>
        public override void Invoke()
        {
            // Live bounds in physical pixels (the process is per-monitor DPI aware), as the button may have moved.
            // Clipped to the window, so a partly scrolled-out element is clicked on its visible part.
            var bounds = AutomationElement.CurrentBoundingRectangle;
            var window = new RECT();
            var left = bounds.left;
            var top = bounds.top;
            var right = bounds.right;
            var bottom = bounds.bottom;
            if (User32.GetWindowRect(OwningWindow, ref window))
            {
                left = Math.Max(left, window.left);
                top = Math.Max(top, window.top);
                right = Math.Min(right, window.right);
                bottom = Math.Min(bottom, window.bottom);
            }
            if (right <= left || bottom <= top)
            {
                throw new InvalidOperationException("Element has no bounds to click");
            }

            var target = Action == MouseAction.Hover
                ? ActionPoint.LabelCentre(left, top, right, bottom, LabelSize)
                : ActionPoint.Centre(left, top, right, bottom);
            var point = new POINT((int)target.X, (int)target.Y);
            var hit = WindowEnumeration.WindowFromPoint(point);
            var root = WindowEnumeration.GetAncestor(OwningWindow, WindowEnumeration.GA_ROOT);
            if (hit == IntPtr.Zero || WindowEnumeration.GetAncestor(hit, WindowEnumeration.GA_ROOT) != root)
            {
                // E.g. the user switched windows meanwhile, or a topmost window covers the element
                throw new InvalidOperationException(string.Format("Window {0} covers the click point {1},{2}; not clicking", hit, point.X, point.Y));
            }
            if (Action == MouseAction.Hover)
            {
                MouseInput.MoveTo(point.X, point.Y);
                return;
            }
            MouseInput.Click(point.X, point.Y, Action);
        }
    }
}
