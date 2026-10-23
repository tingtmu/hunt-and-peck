using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based select hint
    /// </summary>
    internal class UiAutomationSelectHint : UiAutomationHint
    {
        public UiAutomationSelectHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, automationElement, boundingRectangle)
        {
        }

        public override bool ClicksOnFailure => true;

        public override void Invoke()
        {
            UiaPatterns.GetCurrent<IUIAutomationSelectionItemPattern>(AutomationElement, UIA_PatternIds.UIA_SelectionItemPatternId).Select();
        }
    }
}
