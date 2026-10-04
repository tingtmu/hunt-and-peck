using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UIAutomationClient;

namespace HuntAndPeck.Services.Uia
{
    /// <summary>
    /// Provides the IUIAutomation instance for the calling thread (the UIA worker thread)
    /// </summary>
    internal static class UiaAutomationFactory
    {
        /// <summary>Time allowed to connect to a target's UIA provider</summary>
        public const uint ConnectionTimeoutMs = 2000;

        /// <summary>Time allowed for a single cross-process UIA call</summary>
        public const uint TransactionTimeoutMs = 3000;

        [ThreadStatic]
        private static IUIAutomation _threadAutomation;

        /// <summary>
        /// Gets (creating on first use) the automation object owned by the calling thread. Must only be
        /// called on a UIA worker thread, so every UIA object lives in the worker's MTA.
        /// </summary>
        public static IUIAutomation ForCurrentThread()
        {
            if (_threadAutomation == null)
            {
                _threadAutomation = Create();
            }
            return _threadAutomation;
        }

        private static IUIAutomation Create()
        {
            IUIAutomation automation;
            try
            {
                // CUIAutomation8 (Windows 8+) implements IUIAutomation2+, which has the timeout properties
                automation = (IUIAutomation)new CUIAutomation8();
            }
            catch (COMException ex)
            {
                Trace.TraceWarning("CUIAutomation8 unavailable, falling back to CUIAutomation: {0}", UiaErrors.Describe(ex));
                automation = new CUIAutomation();
            }

            var automation2 = automation as IUIAutomation2;
            if (automation2 != null)
            {
                automation2.ConnectionTimeout = ConnectionTimeoutMs;
                automation2.TransactionTimeout = TransactionTimeoutMs;
            }
            else
            {
                Trace.TraceWarning("IUIAutomation2 unavailable; UIA connection/transaction timeouts not set");
            }

            return automation;
        }
    }
}
