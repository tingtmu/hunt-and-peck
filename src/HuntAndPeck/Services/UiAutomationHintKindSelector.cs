namespace HuntAndPeck.Services
{
    /// <summary>
    /// Decides which hint an element gets from its cached capabilities. Pure; no UI Automation calls.
    /// </summary>
    internal static class UiAutomationHintKindSelector
    {
        /// <summary>
        /// Precedence: Invoke, Toggle, SelectionItem, ExpandCollapse, then Focus for a writable Value or
        /// writable RangeValue element; otherwise no hint.
        /// </summary>
        public static UiAutomationHintKind Select(UiAutomationCapabilities capabilities)
        {
            if (Has(capabilities, UiAutomationCapabilities.InvokeAvailable))
            {
                return UiAutomationHintKind.Invoke;
            }
            if (Has(capabilities, UiAutomationCapabilities.ToggleAvailable))
            {
                return UiAutomationHintKind.Toggle;
            }
            if (Has(capabilities, UiAutomationCapabilities.SelectionItemAvailable))
            {
                return UiAutomationHintKind.Select;
            }
            if (Has(capabilities, UiAutomationCapabilities.ExpandCollapseAvailable))
            {
                return UiAutomationHintKind.ExpandCollapse;
            }
            return IsFocusable(capabilities) ? UiAutomationHintKind.Focus : UiAutomationHintKind.None;
        }

        private static bool IsFocusable(UiAutomationCapabilities capabilities)
        {
            var writableValue = Has(capabilities, UiAutomationCapabilities.ValueAvailable)
                && !Has(capabilities, UiAutomationCapabilities.ValueReadOnly);
            var writableRangeValue = Has(capabilities, UiAutomationCapabilities.RangeValueAvailable)
                && !Has(capabilities, UiAutomationCapabilities.RangeValueReadOnly);
            return writableValue || writableRangeValue;
        }

        private static bool Has(UiAutomationCapabilities capabilities, UiAutomationCapabilities flag)
        {
            return (capabilities & flag) == flag;
        }
    }
}
