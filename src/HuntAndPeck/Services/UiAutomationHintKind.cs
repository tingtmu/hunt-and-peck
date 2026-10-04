namespace HuntAndPeck.Services
{
    /// <summary>
    /// The kind of actionable hint created for a UI Automation element
    /// </summary>
    public enum UiAutomationHintKind
    {
        /// <summary>The element supports no usable pattern; no hint</summary>
        None,
        Invoke,
        Toggle,
        Select,
        ExpandCollapse,
        Focus,

        /// <summary>LegacyIAccessible DoDefaultAction; only offered where allowed (bars mode)</summary>
        LegacyDefaultAction,
    }
}
