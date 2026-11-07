using System;
using System.Windows;
using HuntAndPeck.NativeMethods;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// A hint acting on a UI Automation element; by default it can be clicked instead (see <see cref="Hint.CreateClickHint(MouseAction)"/>)
    /// </summary>
    internal abstract class UiAutomationHint : Hint
    {
        protected UiAutomationHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            AutomationElement = automationElement;
        }

        protected IUIAutomationElement AutomationElement { get; }

        public override Hint CreateClickHint(MouseAction action) => CreateClickHint(action, Size.Empty);

        public override Hint CreateClickHint(MouseAction action, Size labelSize) => new UiAutomationClickHint(OwningWindow, AutomationElement, BoundingRectangle, action, labelSize);
    }
}
