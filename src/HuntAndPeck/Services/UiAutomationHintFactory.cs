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
        /// Creates an actionable hint for the element
        /// </summary>
        /// <returns>The created hint, else null if the element supports no usable pattern</returns>
        public static Hint CreateHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement)
        {
            var invokePattern = (IUIAutomationInvokePattern)automationElement.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId);
            if (invokePattern != null)
            {
                return new UiAutomationInvokeHint(owningWindow, invokePattern, hintBounds);
            }

            var togglePattern = (IUIAutomationTogglePattern)automationElement.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId);
            if (togglePattern != null)
            {
                return new UiAutomationToggleHint(owningWindow, togglePattern, hintBounds);
            }

            var selectPattern = (IUIAutomationSelectionItemPattern)automationElement.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId);
            if (selectPattern != null)
            {
                return new UiAutomationSelectHint(owningWindow, selectPattern, hintBounds);
            }

            var expandCollapsePattern = (IUIAutomationExpandCollapsePattern)automationElement.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId);
            if (expandCollapsePattern != null)
            {
                return new UiAutomationExpandCollapseHint(owningWindow, expandCollapsePattern, hintBounds);
            }

            return CreateFocusHint(owningWindow, hintBounds, automationElement);
        }

        /// <summary>
        /// Creates a debug hint listing every pattern the element supports.
        /// Note that the performance of this is *very* bad -- hence debug only.
        /// </summary>
        /// <returns>A debug hint, else null if the element supports no pattern</returns>
        public static DebugHint CreateDebugHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement)
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

        private static Hint CreateFocusHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement)
        {
            var valuePattern = (IUIAutomationValuePattern)automationElement.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId);
            if (valuePattern != null && valuePattern.CurrentIsReadOnly == 0)
            {
                return new UiAutomationFocusHint(owningWindow, automationElement, hintBounds);
            }

            var rangeValuePattern = (IUIAutomationRangeValuePattern)automationElement.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId);
            if (rangeValuePattern != null && rangeValuePattern.CurrentIsReadOnly == 0)
            {
                return new UiAutomationFocusHint(owningWindow, automationElement, hintBounds);
            }

            return null;
        }
    }
}
