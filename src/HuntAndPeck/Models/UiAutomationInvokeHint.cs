using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based invoke hint
    /// </summary>
    internal class UiAutomationInvokeHint : UiAutomationHint
    {
        public UiAutomationInvokeHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, automationElement, boundingRectangle)
        {
        }

        public override bool ClicksOnFailure => true;

        public override void Invoke()
        {
            UiaPatterns.GetCurrent<IUIAutomationInvokePattern>(AutomationElement, UIA_PatternIds.UIA_InvokePatternId).Invoke();
        }
    }
}
