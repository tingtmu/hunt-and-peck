using System;
using System.Windows;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a Windows UI Automation based toggle hint
    /// </summary>
    internal class UiAutomationToggleHint : Hint
    {
        private readonly IUIAutomationElement _automationElement;

        public UiAutomationToggleHint(IntPtr owningWindow, IUIAutomationElement automationElement, Rect boundingRectangle)
            : base(owningWindow, boundingRectangle)
        {
            _automationElement = automationElement;
        }

        public override Hint CreateClickHint() => new UiAutomationClickHint(OwningWindow, _automationElement, BoundingRectangle);

        public override void Invoke()
        {
            UiaPatterns.GetCurrent<IUIAutomationTogglePattern>(_automationElement, UIA_PatternIds.UIA_TogglePatternId).Toggle();
        }
    }
}
