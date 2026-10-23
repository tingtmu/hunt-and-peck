using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// The elements found in a window, and where their properties must be read from
    /// </summary>
    internal sealed class ElementScan
    {
        public ElementScan(List<IUIAutomationElement> elements, UiaPropertySource source)
        {
            Elements = elements;
            Source = source;
        }

        public List<IUIAutomationElement> Elements { get; private set; }

        public UiaPropertySource Source { get; private set; }
    }

    /// <summary>
    /// Finds the hintable automation elements of a window. Runs on the UIA worker thread.
    /// </summary>
    internal static class UiAutomationElementScanner
    {
        /// <param name="hWnd">The window</param>
        /// <param name="includeLegacy">Also cache the LegacyIAccessible properties (bars mode)</param>
        /// <param name="allElements">Also elements no hint can act on (debug overlay)</param>
        /// <returns>The elements found, else null if the window could not be enumerated</returns>
        public static ElementScan TryScan(IntPtr hWnd, bool includeLegacy = false, bool allElements = false)
        {
            try
            {
                return ScanWithFallback(hWnd, includeLegacy, allElements);
            }
            catch (Exception ex) when (UiaErrors.IsTargetFailure(ex) || ex is ArgumentException)
            {
                // The window may have been closed between finding it and enumerating it, or be hung
                Trace.TraceWarning("UI Automation enumeration failed for window {0}: {1}", hWnd, UiaErrors.Describe(ex));
                return null;
            }
        }

        /// <remarks>
        /// Trade-off: a provider may fail the bulk cached find (e.g. while building one element's cache)
        /// even though a plain find works. Retrying once uncached keeps such windows usable, at the cost of
        /// reading properties live per element. A timeout is not retried: the target is hung, and a second
        /// attempt would only burn the rest of the enumeration timeout.
        /// </remarks>
        private static ElementScan ScanWithFallback(IntPtr hWnd, bool includeLegacy, bool allElements)
        {
            try
            {
                return Scan(hWnd, UiaPropertySource.Cached, includeLegacy, allElements);
            }
            catch (Exception ex) when (UiaErrors.IsTargetFailure(ex) && !UiaErrors.IsTimeout(ex))
            {
                Trace.TraceWarning("Cached UI Automation enumeration failed for window {0}, retrying uncached: {1}", hWnd, UiaErrors.Describe(ex));
            }
            return Scan(hWnd, UiaPropertySource.Current, includeLegacy, allElements);
        }

        private static ElementScan Scan(IntPtr hWnd, UiaPropertySource source, bool includeLegacy, bool allElements)
        {
            var automation = UiaAutomationFactory.ForCurrentThread();
            var automationElement = automation.ElementFromHandle(hWnd);
            var condition = CreateCondition(automation, includeLegacy, allElements);

            // Cached: one bulk call fetches every element with the properties that decide its hint
            var elementArray = source == UiaPropertySource.Cached
                ? automationElement.FindAllBuildCache(TreeScope.TreeScope_Descendants, condition, UiAutomationElementCache.CreateRequest(automation, includeLegacy))
                : automationElement.FindAll(TreeScope.TreeScope_Descendants, condition);

            var elements = new List<IUIAutomationElement>();
            if (elementArray != null)
            {
                for (var i = 0; i < elementArray.Length; ++i)
                {
                    elements.Add(elementArray.GetElement(i));
                }
            }
            return new ElementScan(elements, source);
        }

        /// <summary>
        /// Enabled, on-screen elements of the control view; unless <paramref name="allElements"/>, only those
        /// supporting a pattern a hint can use, so the target doesn't send elements that would get no hint
        /// (halves the elements found in typical windows)
        /// </summary>
        private static IUIAutomationCondition CreateCondition(IUIAutomation automation, bool includeLegacy, bool allElements)
        {
            var conditionEnabled = automation.CreatePropertyCondition(UIA_PropertyIds.UIA_IsEnabledPropertyId, true);
            var enabledControlCondition = automation.CreateAndCondition(automation.ControlViewCondition, conditionEnabled);

            var conditionOnScreen = automation.CreatePropertyCondition(UIA_PropertyIds.UIA_IsOffscreenPropertyId, false);
            var visibleCondition = automation.CreateAndCondition(enabledControlCondition, conditionOnScreen);
            if (allElements)
            {
                return visibleCondition;
            }

            var actionConditions = UiAutomationElementCache.ActionPatternAvailabilityProperties(includeLegacy)
                .Select(x => automation.CreatePropertyCondition(x, true))
                .ToArray();
            return automation.CreateAndCondition(visibleCondition, automation.CreateOrConditionFromArray(actionConditions));
        }
    }
}
