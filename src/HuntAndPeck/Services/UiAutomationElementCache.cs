using System.Collections.Generic;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// The UI Automation cache request used to enumerate hints, and readers for the properties it caches.
    /// Fetching these properties in bulk with FindAllBuildCache avoids several cross-process calls per element.
    /// </summary>
    /// <remarks>
    /// Every Cached* property or GetCachedPropertyValue read anywhere on enumerated elements must be added to
    /// <see cref="CreateRequest"/>. Reading an uncached property throws E_INVALIDARG, surfacing as
    /// <see cref="System.ArgumentException"/> (verified). That is not a target failure
    /// (<see cref="UiaErrors.IsTargetFailure"/>), so it is not skipped per element: it fails the whole
    /// enumeration, on every window, loudly.
    /// </remarks>
    internal static class UiAutomationElementCache
    {
        /// <summary>Boolean property id -> the capability flag it sets when true</summary>
        private static readonly KeyValuePair<int, UiAutomationCapabilities>[] s_flagProperties =
        {
            Flag(UIA_PropertyIds.UIA_IsInvokePatternAvailablePropertyId, UiAutomationCapabilities.InvokeAvailable),
            Flag(UIA_PropertyIds.UIA_IsTogglePatternAvailablePropertyId, UiAutomationCapabilities.ToggleAvailable),
            Flag(UIA_PropertyIds.UIA_IsSelectionItemPatternAvailablePropertyId, UiAutomationCapabilities.SelectionItemAvailable),
            Flag(UIA_PropertyIds.UIA_IsExpandCollapsePatternAvailablePropertyId, UiAutomationCapabilities.ExpandCollapseAvailable),
            Flag(UIA_PropertyIds.UIA_IsValuePatternAvailablePropertyId, UiAutomationCapabilities.ValueAvailable),
            Flag(UIA_PropertyIds.UIA_IsRangeValuePatternAvailablePropertyId, UiAutomationCapabilities.RangeValueAvailable),
        };

        /// <summary>
        /// Creates the cache request. Elements stay in Full mode so hints can make live calls when invoked.
        /// </summary>
        /// <param name="automation">The automation object of the calling (worker) thread</param>
        /// <param name="includeLegacy">
        /// Also cache what the LegacyIAccessible fallback needs (<see cref="ReadCapabilities"/> with includeLegacy);
        /// bars mode only, so the normal request stays small
        /// </param>
        public static IUIAutomationCacheRequest CreateRequest(IUIAutomation automation, bool includeLegacy = false)
        {
            var request = automation.CreateCacheRequest();
            request.TreeScope = TreeScope.TreeScope_Element;
            // Cache every found element; the find condition already does the filtering
            request.TreeFilter = automation.CreateTrueCondition();
            request.AutomationElementMode = AutomationElementMode.AutomationElementMode_Full;

            request.AddProperty(UIA_PropertyIds.UIA_BoundingRectanglePropertyId);
            foreach (var flagProperty in s_flagProperties)
            {
                request.AddProperty(flagProperty.Key);
            }
            request.AddProperty(UIA_PropertyIds.UIA_ValueIsReadOnlyPropertyId);
            request.AddProperty(UIA_PropertyIds.UIA_RangeValueIsReadOnlyPropertyId);
            request.AddProperty(UIA_PropertyIds.UIA_ControlTypePropertyId);
            request.AddProperty(UIA_PropertyIds.UIA_FrameworkIdPropertyId);
            if (includeLegacy)
            {
                request.AddProperty(UIA_PropertyIds.UIA_IsLegacyIAccessiblePatternAvailablePropertyId);
                request.AddProperty(UIA_PropertyIds.UIA_LegacyIAccessibleDefaultActionPropertyId);
            }
            return request;
        }

        /// <summary>
        /// Reads the element's control type id (UIA_*ControlTypeId)
        /// </summary>
        public static int ReadControlType(IUIAutomationElement element, UiaPropertySource source)
        {
            return source == UiaPropertySource.Cached ? element.CachedControlType : element.CurrentControlType;
        }

        /// <summary>
        /// Reads the element's UI framework, e.g. "Qt", "Chrome", "WPF"; null or empty if the provider gives none
        /// </summary>
        public static string ReadFrameworkId(IUIAutomationElement element, UiaPropertySource source)
        {
            return source == UiaPropertySource.Cached ? element.CachedFrameworkId : element.CurrentFrameworkId;
        }

        /// <summary>
        /// Reads the element's bounding rectangle (physical screen pixels)
        /// </summary>
        public static tagRECT ReadBounds(IUIAutomationElement element, UiaPropertySource source)
        {
            return source == UiaPropertySource.Cached ? element.CachedBoundingRectangle : element.CurrentBoundingRectangle;
        }

        /// <summary>
        /// Reads the element's capabilities; no cross-process call when <paramref name="source"/> is cached
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="source">Where to read the properties</param>
        /// <param name="includeLegacy">
        /// Also read <see cref="UiAutomationCapabilities.LegacyDefaultActionAvailable"/>; off where it would not be
        /// used, as reading it live costs extra cross-process calls
        /// </param>
        public static UiAutomationCapabilities ReadCapabilities(IUIAutomationElement element, UiaPropertySource source, bool includeLegacy = false)
        {
            var capabilities = UiAutomationCapabilities.None;
            foreach (var flagProperty in s_flagProperties)
            {
                if (ReadBool(element, source, flagProperty.Key, false))
                {
                    capabilities |= flagProperty.Value;
                }
            }

            // An unknown read-only state counts as read-only, so no focus hint (as when reading it live failed)
            if ((capabilities & UiAutomationCapabilities.ValueAvailable) != 0
                && ReadBool(element, source, UIA_PropertyIds.UIA_ValueIsReadOnlyPropertyId, true))
            {
                capabilities |= UiAutomationCapabilities.ValueReadOnly;
            }
            if ((capabilities & UiAutomationCapabilities.RangeValueAvailable) != 0
                && ReadBool(element, source, UIA_PropertyIds.UIA_RangeValueIsReadOnlyPropertyId, true))
            {
                capabilities |= UiAutomationCapabilities.RangeValueReadOnly;
            }
            if (includeLegacy && HasLegacyDefaultAction(element, source))
            {
                capabilities |= UiAutomationCapabilities.LegacyDefaultActionAvailable;
            }
            return capabilities;
        }

        private static bool HasLegacyDefaultAction(IUIAutomationElement element, UiaPropertySource source)
        {
            if (!ReadBool(element, source, UIA_PropertyIds.UIA_IsLegacyIAccessiblePatternAvailablePropertyId, false))
            {
                return false;
            }
            var defaultAction = source == UiaPropertySource.Cached
                ? element.GetCachedPropertyValue(UIA_PropertyIds.UIA_LegacyIAccessibleDefaultActionPropertyId)
                : element.GetCurrentPropertyValue(UIA_PropertyIds.UIA_LegacyIAccessibleDefaultActionPropertyId);
            return !string.IsNullOrWhiteSpace(defaultAction as string);
        }

        /// <returns>The boolean, else <paramref name="fallback"/> if the provider gave none (not-supported sentinel)</returns>
        private static bool ReadBool(IUIAutomationElement element, UiaPropertySource source, int propertyId, bool fallback)
        {
            var value = source == UiaPropertySource.Cached
                ? element.GetCachedPropertyValue(propertyId)
                : element.GetCurrentPropertyValue(propertyId);
            return value is bool ? (bool)value : fallback;
        }

        private static KeyValuePair<int, UiAutomationCapabilities> Flag(int propertyId, UiAutomationCapabilities flag)
        {
            return new KeyValuePair<int, UiAutomationCapabilities>(propertyId, flag);
        }
    }
}
