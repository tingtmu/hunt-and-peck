using System;
using System.Windows;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based focus hint
    /// </summary>
    internal class UiAutomationFocusHint : UiAutomationHint
    {
        public UiAutomationFocusHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, automationElement, boundingRectangle)
        {
        }

        public override void Invoke()
        {
            AutomationElement.SetFocus();
        }
    }
}
