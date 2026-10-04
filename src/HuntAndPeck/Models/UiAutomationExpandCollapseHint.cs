using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based expandcollapse hint
    /// </summary>
    internal class UiAutomationExpandCollapseHint : Hint
    {
        private readonly IUIAutomationElement _automationElement;

        public UiAutomationExpandCollapseHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            _automationElement = automationElement;
        }

        public override void Invoke()
        {
            var expandCollapsePattern = UiaPatterns.GetCurrent<IUIAutomationExpandCollapsePattern>(
                _automationElement, UIA_PatternIds.UIA_ExpandCollapsePatternId);
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
