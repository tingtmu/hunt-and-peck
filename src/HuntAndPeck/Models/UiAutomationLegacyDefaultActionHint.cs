using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Performs the element's MSAA default action (e.g. "Press") through the LegacyIAccessible pattern; for
    /// elements with no UI Automation action pattern, such as Windows 11 taskbar app buttons
    /// </summary>
    internal class UiAutomationLegacyDefaultActionHint : Hint
    {
        private readonly IUIAutomationElement _automationElement;

        public UiAutomationLegacyDefaultActionHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            _automationElement = automationElement;
        }

        public override void Invoke()
        {
            UiaPatterns.GetCurrent<IUIAutomationLegacyIAccessiblePattern>(_automationElement, UIA_PatternIds.UIA_LegacyIAccessiblePatternId).DoDefaultAction();
        }
    }
}
