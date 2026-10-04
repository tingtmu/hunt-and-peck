using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Checks that a window of another process can own the overlay without putting it at risk
    /// </summary>
    internal static class OverlayOwnerCheck
    {
        /// <summary>Longest wait for the owner to answer a WM_NULL</summary>
        public const uint ResponseTimeoutMs = 100;

        /// <summary>
        /// True if the window exists and its thread answers messages; an owner that is gone or hung could leave
        /// the owned overlay unresponsive or destroy it
        /// </summary>
        public static bool IsUsable(IntPtr owner)
        {
            if (!User32.IsWindow(owner))
            {
                Trace.TraceWarning("Overlay: owner window {0} no longer exists; showing the overlay unowned", owner);
                return false;
            }

            UIntPtr result;
            var answered = User32.SendMessageTimeout(owner, User32.WM_NULL, UIntPtr.Zero, IntPtr.Zero, User32.SMTO_ABORTIFHUNG, ResponseTimeoutMs, out result);
            if (answered == IntPtr.Zero)
            {
                Trace.TraceWarning("Overlay: owner window {0} did not answer within {1} ms (error {2}); showing the overlay unowned", owner, ResponseTimeoutMs, Marshal.GetLastWin32Error());
                return false;
            }
            return true;
        }
    }
}
