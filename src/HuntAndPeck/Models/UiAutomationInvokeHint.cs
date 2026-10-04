using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based invoke hint
    /// </summary>
    internal class UiAutomationInvokeHint : Hint
    {
        private readonly IUIAutomationElement _automationElement;

        public UiAutomationInvokeHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            _automationElement = automationElement;
        }

        public override void Invoke()
        {
            UiaPatterns.GetCurrent<IUIAutomationInvokePattern>(_automationElement, UIA_PatternIds.UIA_InvokePatternId).Invoke();
        }
    }
}
