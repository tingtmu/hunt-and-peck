using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based toggle hint
    /// </summary>
    internal class UiAutomationToggleHint : UiAutomationHint
    {
        public UiAutomationToggleHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, automationElement, boundingRectangle)
        {
        }

        public override void Invoke()
        {
            UiaPatterns.GetCurrent<IUIAutomationTogglePattern>(AutomationElement, UIA_PatternIds.UIA_TogglePatternId).Toggle();
        }
    }
}
