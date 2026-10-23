using System;
using System.Windows;
using HuntAndPeck.NativeMethods;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// A hint acting on a UI Automation element; by default it can be clicked instead (see <see cref="Hint.CreateClickHint"/>)
    /// </summary>
    internal abstract class UiAutomationHint : Hint
    {
        protected UiAutomationHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            AutomationElement = automationElement;
        }

        protected IUIAutomationElement AutomationElement { get; }

        public override Hint CreateClickHint(MouseAction action) => new UiAutomationClickHint(OwningWindow, AutomationElement, BoundingRectangle, action);
    }
}
