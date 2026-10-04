using System;
using UIAutomationClient;

namespace HuntAndPeck.Services.Uia
{
    /// <summary>
    /// Fetches live pattern objects when a hint is invoked, so enumeration needs no per-element pattern calls
    /// </summary>
    internal static class UiaPatterns
    {
        /// <summary>
        /// Gets the element's current pattern object. Must run on the UIA worker thread.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The element no longer supports the pattern (a target failure, see <see cref="UiaErrors.IsTargetFailure"/>)
        /// </exception>
        public static T GetCurrent<T>(IUIAutomationElement element, int patternId) where T : class
        {
            var pattern = element.GetCurrentPattern(patternId);
            if (pattern == null)
            {
                throw new InvalidOperationException(string.Format("Element no longer supports pattern {0}", patternId));
            }
            return (T)pattern;
        }
    }
}
