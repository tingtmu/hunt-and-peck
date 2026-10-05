using System;
using System.Runtime.InteropServices;

namespace HuntAndPeck.Services.Uia
{
    /// <summary>
    /// Classifies exceptions raised by UI Automation COM calls against another process
    /// </summary>
    internal static class UiaErrors
    {
        /// <summary>UIA_E_ELEMENTNOTAVAILABLE: the element no longer exists</summary>
        public const int ElementNotAvailable = unchecked((int)0x80040201);

        /// <summary>
        /// UIA_E_TIMEOUT (same value as COR_E_TIMEOUT). The CLR does not map it to <see cref="TimeoutException"/>
        /// for COM calls; it surfaces as a <see cref="COMException"/> with this HResult (covered by a unit test).
        /// </summary>
        public const int Timeout = unchecked((int)0x80131505);

        /// <summary>
        /// True for failures caused by the target element or app (element vanished, app hung or closed,
        /// provider misbehaving) rather than by a bug in this app. Such an element is simply skipped.
        /// </summary>
        /// <remarks>
        /// UIA_E_ELEMENTNOTAVAILABLE, UIA_E_TIMEOUT, RPC_E_* and most other HRESULTs surface as
        /// <see cref="COMException"/>; HRESULTs the CLR knows (e.g. E_NOINTERFACE, COR_E_INVALIDOPERATION)
        /// map to their managed exceptions. <see cref="TimeoutException"/> is included for managed timeouts.
        /// <see cref="InvalidCastException"/> is a provider that returns a pattern object not implementing
        /// the pattern interface.
        /// </remarks>
        public static bool IsTargetFailure(Exception ex)
        {
            return ex is COMException
                || ex is InvalidOperationException
                || ex is TimeoutException
                || ex is InvalidCastException;
        }

        /// <summary>
        /// True if a failed UI Automation action may be retried as a mouse click on the element: the provider
        /// refused or failed the action (e.g. Chrome's extension buttons return E_FAIL for Expand), but the
        /// element still exists and the app answers. Includes E_NOTIMPL (<see cref="NotImplementedException"/>),
        /// an action the provider does not implement. Not for a vanished element (the click would hit whatever
        /// is there now) or a hung app.
        /// </summary>
        public static bool IsClickFallbackFailure(Exception ex)
        {
            return (IsTargetFailure(ex) || ex is NotImplementedException)
                && !IsTimeout(ex)
                && ex.HResult != ElementNotAvailable;
        }

        /// <summary>
        /// True if the target app did not answer in time (UIA transaction/connection timeout)
        /// </summary>
        public static bool IsTimeout(Exception ex)
        {
            return ex is TimeoutException || ex.HResult == Timeout;
        }

        /// <summary>
        /// Short description for logging, e.g. "COMException 0x80040201: ..."
        /// </summary>
        public static string Describe(Exception ex)
        {
            return string.Format("{0} 0x{1:X8}: {2}", ex.GetType().Name, ex.HResult, ex.Message);
        }
    }
}
