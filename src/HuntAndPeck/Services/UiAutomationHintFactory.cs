using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Creates hints from UI Automation elements. Runs on the UIA worker thread.
    /// </summary>
    /// <remarks>
    /// Exceptions from a vanishing or misbehaving element propagate to the caller, which skips and counts
    /// the element (see <see cref="UiaErrors.IsTargetFailure"/>).
    /// </remarks>
    internal static class UiAutomationHintFactory
    {
        /// <summary>
        /// Creates an actionable hint for the element from its properties (see
        /// <see cref="UiAutomationElementCache"/>); no cross-process call when they are cached. The hint
        /// fetches the live pattern object when it is invoked. Elements whose pattern action is known to do
        /// nothing (<see cref="UiAutomationHintKindSelector.PrefersClick"/>) get a mouse click hint instead.
        /// </summary>
        /// <returns>The created hint, else null if the element supports no usable pattern</returns>
        public static Hint CreateHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement, UiaPropertySource source)
        {
            var capabilities = UiAutomationElementCache.ReadCapabilities(automationElement, source);
            var kind = UiAutomationHintKindSelector.Select(capabilities);
            if (kind != UiAutomationHintKind.None && PrefersClick(automationElement, source))
            {
                return new UiAutomationClickHint(owningWindow, automationElement, hintBounds);
            }
            return Create(kind, owningWindow, hintBounds, automationElement);
        }

        /// <remarks>Reads the control type only for Qt elements: a live read costs a cross-process call</remarks>
        private static bool PrefersClick(IUIAutomationElement automationElement, UiaPropertySource source)
        {
            var frameworkId = UiAutomationElementCache.ReadFrameworkId(automationElement, source);
            return UiAutomationHintKindSelector.IsQt(frameworkId)
                && UiAutomationHintKindSelector.PrefersClick(frameworkId, UiAutomationElementCache.ReadControlType(automationElement, source));
        }

        /// <summary>
        /// As <see cref="CreateHint"/>, plus a LegacyIAccessible default action hint for elements with no action
        /// pattern; for bars mode only (taskbar app buttons), so normal windows keep their hints. On a taskbar
        /// such elements get a click hint instead: the Windows 11 taskbar app buttons ignore the default action.
        /// </summary>
        public static Hint CreateBarHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement, UiaPropertySource source)
        {
            var capabilities = UiAutomationElementCache.ReadCapabilities(automationElement, source, true);
            var kind = UiAutomationHintKindSelector.Select(capabilities, true);
            if (kind != UiAutomationHintKind.LegacyDefaultAction)
            {
                return Create(kind, owningWindow, hintBounds, automationElement);
            }
            if (!UiAutomationHintKindSelector.IsLegacyActionControlType(UiAutomationElementCache.ReadControlType(automationElement, source)))
            {
                return null;
            }
            return Taskbar.IsTaskbar(owningWindow)
                ? new UiAutomationClickHint(owningWindow, automationElement, hintBounds)
                : Create(kind, owningWindow, hintBounds, automationElement);
        }

        private static Hint Create(UiAutomationHintKind kind, IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement)
        {
            switch (kind)
            {
                case UiAutomationHintKind.Invoke:
                    return new UiAutomationInvokeHint(owningWindow, automationElement, hintBounds);
                case UiAutomationHintKind.Toggle:
                    return new UiAutomationToggleHint(owningWindow, automationElement, hintBounds);
                case UiAutomationHintKind.Select:
                    return new UiAutomationSelectHint(owningWindow, automationElement, hintBounds);
                case UiAutomationHintKind.ExpandCollapse:
                    return new UiAutomationExpandCollapseHint(owningWindow, automationElement, hintBounds);
                case UiAutomationHintKind.Focus:
                    return new UiAutomationFocusHint(owningWindow, automationElement, hintBounds);
                case UiAutomationHintKind.LegacyDefaultAction:
                    return new UiAutomationLegacyDefaultActionHint(owningWindow, automationElement, hintBounds);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates a debug hint listing every pattern the element supports.
        /// Note that the performance of this is *very* bad -- hence debug only.
        /// </summary>
        /// <param name="source">Unused: the patterns are always probed live</param>
        /// <returns>A debug hint, else null if the element supports no pattern</returns>
        public static DebugHint CreateDebugHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement, UiaPropertySource source)
        {
            var programmaticNames = new List<string>();

            foreach (var pn in UiAutomationPatternIds.PatternNames)
            {
                try
                {
                    if (automationElement.GetCurrentPattern(pn.Key) != null)
                    {
                        programmaticNames.Add(pn.Value);
                    }
                }
                catch (Exception ex) when (UiaErrors.IsTargetFailure(ex))
                {
                    // E.g. a pattern id unknown to this OS version, or a provider failing for one pattern;
                    // keep probing the remaining patterns
                    Trace.TraceInformation("Debug hint: pattern {0} probe failed: {1}", pn.Value, UiaErrors.Describe(ex));
                }
            }

            return programmaticNames.Count > 0
                ? new DebugHint(owningWindow, hintBounds, programmaticNames)
                : null;
        }
    }
}
