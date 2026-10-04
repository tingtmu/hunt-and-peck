using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Switches the foreground window to a window of another process
    /// </summary>
    internal static class ForegroundActivation
    {
        /// <summary>
        /// Calls SetForegroundWindow, and if Windows refuses (this process lacks the foreground right, e.g. it is
        /// no longer the foreground process), retries with the current foreground thread's input attached.
        /// </summary>
        /// <remarks>
        /// Call on a thread with a message queue (the UI thread). The result is deliberately not returned:
        /// SetForegroundWindow can report failure under AttachThreadInput and still switch, so callers check the
        /// effect instead.
        /// </remarks>
        public static void Activate(IntPtr hWnd)
        {
            if (!User32.IsWindow(hWnd))
            {
                Trace.TraceInformation("Foreground: window {0} no longer exists, not activating it", hWnd);
                return;
            }

            if (User32.SetForegroundWindow(hWnd))
            {
                return;
            }

            var foregroundThread = User32.GetWindowThreadProcessId(User32.GetForegroundWindow(), IntPtr.Zero);
            var ownThread = Kernel32.GetCurrentThreadId();
            if (foregroundThread == 0 || foregroundThread == ownThread)
            {
                Trace.TraceWarning("Foreground: SetForegroundWindow({0}) refused, no other foreground thread to attach to", hWnd);
                return;
            }

            if (!User32.AttachThreadInput(ownThread, foregroundThread, true))
            {
                Trace.TraceWarning("Foreground: AttachThreadInput to thread {0} failed, error {1}; window {2} not activated", foregroundThread, Marshal.GetLastWin32Error(), hWnd);
                return;
            }

            try
            {
                User32.SetForegroundWindow(hWnd);
            }
            finally
            {
                // Always detach, or the two threads' input stays linked (shared focus/key state)
                if (!User32.AttachThreadInput(ownThread, foregroundThread, false))
                {
                    Trace.TraceWarning("Foreground: detaching thread input from thread {0} failed, error {1}", foregroundThread, Marshal.GetLastWin32Error());
                }
            }
        }
    }
}
