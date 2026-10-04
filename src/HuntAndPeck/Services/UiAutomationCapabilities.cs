using System;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// The cached UI Automation pattern availability flags of an element that decide its hint kind
    /// </summary>
    [Flags]
    public enum UiAutomationCapabilities
    {
        None = 0,
        InvokeAvailable = 1 << 0,
        ToggleAvailable = 1 << 1,
        SelectionItemAvailable = 1 << 2,
        ExpandCollapseAvailable = 1 << 3,
        ValueAvailable = 1 << 4,

        /// <summary>The Value pattern's IsReadOnly property; only meaningful with <see cref="ValueAvailable"/></summary>
        ValueReadOnly = 1 << 5,

        RangeValueAvailable = 1 << 6,

        /// <summary>The RangeValue pattern's IsReadOnly property; only meaningful with <see cref="RangeValueAvailable"/></summary>
        RangeValueReadOnly = 1 << 7,

        /// <summary>
        /// The LegacyIAccessible pattern is available and reports a non-empty DefaultAction (e.g. "Press")
        /// </summary>
        LegacyDefaultActionAvailable = 1 << 8,
    }
}
