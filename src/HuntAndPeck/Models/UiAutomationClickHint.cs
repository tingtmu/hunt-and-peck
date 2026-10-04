using System;
using System.Windows;
using HuntAndPeck.NativeMethods;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Clicks the centre of the element with a synthesized mouse click; for Windows 11 taskbar app buttons, which
    /// support no UI Automation action pattern and whose LegacyIAccessible default action reports success but does
    /// nothing
    /// </summary>
    internal class UiAutomationClickHint : Hint
    {
        private readonly IUIAutomationElement _automationElement;

        public UiAutomationClickHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            _automationElement = automationElement;
        }

        /// <summary>The overlay covers the button, so it must be gone before the click</summary>
        public override bool InvokeAfterOverlayCloses => true;

        public override void Invoke()
        {
            // Live bounds in physical pixels (the process is per-monitor DPI aware), as the button may have moved
            var bounds = _automationElement.CurrentBoundingRectangle;
            if (bounds.right <= bounds.left || bounds.bottom <= bounds.top)
            {
                throw new InvalidOperationException("Element has no bounds to click");
            }
            MouseInput.LeftClick((bounds.left + bounds.right) / 2, (bounds.top + bounds.bottom) / 2);
        }
    }
}
