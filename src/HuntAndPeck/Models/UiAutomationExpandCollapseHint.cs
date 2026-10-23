using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based expandcollapse hint
    /// </summary>
    internal class UiAutomationExpandCollapseHint : UiAutomationHint
    {
        public UiAutomationExpandCollapseHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, automationElement, boundingRectangle)
        {
        }

        public override bool ClicksOnFailure => true;

        public override void Invoke()
        {
            var expandCollapsePattern = UiaPatterns.GetCurrent<IUIAutomationExpandCollapsePattern>(
                AutomationElement, UIA_PatternIds.UIA_ExpandCollapsePatternId);
            switch (expandCollapsePattern.CurrentExpandCollapseState)
            {
                case ExpandCollapseState.ExpandCollapseState_Collapsed:
                    expandCollapsePattern.Expand();
                    break;
                case ExpandCollapseState.ExpandCollapseState_Expanded:
                case ExpandCollapseState.ExpandCollapseState_PartiallyExpanded:
                    expandCollapsePattern.Collapse();
                    break;
            }
        }
    }
}
