using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;
using HuntAndPeck.Services.Uia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// UI Automation based hint provider. All UIA calls run on one background MTA worker thread
    /// (<see cref="UiaExecutor"/>) with an overall timeout, so a hung or closing target app can't block the UI.
    /// </summary>
    internal sealed class UiAutomationHintProviderService : IHintProviderService, IDebugHintProviderService, IDisposable
    {
        /// <summary>Overall time allowed to enumerate a window's hints</summary>
        public static readonly TimeSpan EnumerationTimeout = TimeSpan.FromSeconds(5);

        /// <summary>Overall time allowed to invoke a hint</summary>
        public static readonly TimeSpan InvocationTimeout = TimeSpan.FromSeconds(5);

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private readonly UiaExecutor _executor;
        private readonly BarHintEnumerator _barEnumerator;

        /// <param name="notifyUser">Optional user notification (tray balloon) for persistent UIA trouble</param>
        public UiAutomationHintProviderService(Action<string> notifyUser = null)
        {
            _executor = new UiaExecutor("HuntAndPeck UIA worker", notifyUser);
            _barEnumerator = new BarHintEnumerator(ScanBarAsync, Task.Delay, () => Clock.Elapsed, EnumerationTimeout);
        }

        public Task<HintSession> EnumHintsAsync()
        {
            return EnumForegroundAsync(UiAutomationHintFactory.CreateHint, false);
        }

        public Task<HintSession> EnumHintsAsync(IntPtr hWnd)
        {
            return EnumAsync(hWnd, UiAutomationHintFactory.CreateHint, false);
        }

        /// <remarks>Shows every element, also those no hint can act on</remarks>
        public Task<HintSession> EnumDebugHintsAsync()
        {
            return EnumForegroundAsync(UiAutomationHintFactory.CreateDebugHint, true);
        }

        public Task<HintSession> EnumDebugHintsAsync(IntPtr hWnd)
        {
            return EnumAsync(hWnd, UiAutomationHintFactory.CreateDebugHint, true);
        }

        /// <remarks>
        /// Each window is scanned on its own (<see cref="BarHintEnumerator"/>), within <see cref="EnumerationTimeout"/>
        /// and an overall deadline; a window that fails, hangs or vanishes is logged and left out, the others
        /// still get hints
        /// </remarks>
        public Task<HintSession> EnumBarHintsAsync(IReadOnlyList<IntPtr> windows, Rect monitor)
        {
            return _barEnumerator.EnumAsync(GetWindowBounds(windows), monitor);
        }
        public async Task<HintSession> EnumPopupHintsAsync(IReadOnlyList<IntPtr> windows)
        {
            try
            {
                return await _executor.RunAsync(() => EnumPopupWindowHints(windows), EnumerationTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                Trace.TraceWarning("Popup hint enumeration abandoned: {0}", ex.Message);
                return null;
            }
            catch (Exception ex) when (IsShutdown(ex))
            {
                Trace.TraceInformation("Popup hint enumeration cancelled, UI Automation is shutting down: {0}", ex.Message);
                return null;
            }
        }

        /// <remarks>
        /// Target failures (element gone, app hung/closed) and timeouts are logged and do not fault the task
        /// </remarks>
        public async Task<bool> InvokeHintAsync(Hint hint)
        {
            try
            {
                return await _executor.RunAsync(() => InvokeOnWorker(hint), InvocationTimeout).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsShutdown(ex))
            {
                // Checked first: ObjectDisposedException is also an InvalidOperationException
                Trace.TraceInformation("Invoking {0} skipped, UI Automation is shutting down: {1}", hint.GetType().Name, ex.Message);
                return true;
            }
            catch (Exception ex) when (UiaErrors.IsTargetFailure(ex) || IsActionRefused(ex))
            {
                Trace.TraceWarning("Invoking {0} in window {1} failed: {2}", hint.GetType().Name, hint.OwningWindow, UiaErrors.Describe(ex));
                return !UiaErrors.IsClickFallbackFailure(ex);
            }
        }

        /// <summary>
        /// True for HRESULTs the CLR maps to exceptions that are not target failures but that a provider may
        /// return for an action: E_NOTIMPL (action not implemented), E_ACCESSDENIED (e.g. an elevated target)
        /// </summary>
        private static bool IsActionRefused(Exception ex)
        {
            return ex is NotImplementedException || ex is UnauthorizedAccessException;
        }

        /// <summary>
        /// True for the exceptions <see cref="UiaExecutor"/> raises once it is disposed (app exit)
        /// </summary>
        private static bool IsShutdown(Exception ex)
        {
            return ex is OperationCanceledException || ex is ObjectDisposedException;
        }

        public void Dispose()
        {
            _executor.Dispose();
        }

        private static bool InvokeOnWorker(Hint hint)
        {
            hint.Invoke();
            return true;
        }

        private Task<HintSession> EnumForegroundAsync(HintFactoryMethod hintFactory, bool allElements)
        {
            var foregroundWindow = User32.GetForegroundWindow();
            return EnumAsync(foregroundWindow, hintFactory, allElements);
        }

        private async Task<HintSession> EnumAsync(IntPtr hWnd, HintFactoryMethod hintFactory, bool allElements)
        {
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return await _executor.RunAsync(() => EnumWindowHints(hWnd, hintFactory, allElements), EnumerationTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                Trace.TraceWarning("Hint enumeration for window {0} abandoned, no overlay shown: {1}", hWnd, ex.Message);
                return null;
            }
            catch (Exception ex) when (IsShutdown(ex))
            {
                Trace.TraceInformation("Hint enumeration for window {0} cancelled, UI Automation is shutting down: {1}", hWnd, ex.Message);
                return null;
            }
        }

        /// <returns>The window's hints, else null if it could not be enumerated (logged)</returns>
        private async Task<List<Hint>> ScanBarAsync(WindowScanTarget target, TimeSpan timeout)
        {
            try
            {
                return await _executor.RunAsync(() => WindowHintScanner.Scan(target), timeout).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                Trace.TraceWarning("Bars: hint enumeration for window {0} abandoned, leaving it out: {1}", target.Handle, ex.Message);
                return null;
            }
            catch (Exception ex) when (IsShutdown(ex))
            {
                Trace.TraceInformation("Bars: hint enumeration for window {0} cancelled, UI Automation is shutting down: {1}", target.Handle, ex.Message);
                return null;
            }
        }

        /// <summary>The bounds (physical pixels) of each window that still has bounds</summary>
        private static List<KeyValuePair<IntPtr, Rect>> GetWindowBounds(IReadOnlyList<IntPtr> windows)
        {
            var result = new List<KeyValuePair<IntPtr, Rect>>();
            foreach (var hWnd in windows)
            {
                Rect bounds;
                if (TryGetWindowBounds(hWnd, out bounds))
                {
                    result.Add(new KeyValuePair<IntPtr, Rect>(hWnd, bounds));
                }
            }
            return result;
        }
        private static bool TryGetWindowBounds(IntPtr hWnd, out Rect bounds)
        {
            // Physical pixels: the process is per-monitor DPI aware
            var raw = new RECT();
            if (!User32.GetWindowRect(hWnd, ref raw))
            {
                Trace.TraceWarning("GetWindowRect failed for window {0}, error {1}", hWnd, Marshal.GetLastWin32Error());
                bounds = Rect.Empty;
                return false;
            }
            bounds = raw;
            return true;
        }

        /// <summary>
        /// Enumerates the hints of popup windows for one overlay over all of them. Runs on the UIA worker thread.
        /// </summary>
        /// <returns>A hint session, else null if none of the windows has bounds any more</returns>
        private static HintSession EnumPopupWindowHints(IReadOnlyList<IntPtr> windows)
        {
            var targets = GetWindowBounds(windows);
            if (targets.Count == 0)
            {
                return null;
            }

            var overlayBounds = HintBounds.UnionOf(targets.Select(x => x.Value));
            var hints = new List<Hint>();
            foreach (var target in targets)
            {
                var windowHints = WindowHintScanner.Scan(new WindowScanTarget(target.Key, target.Value, overlayBounds, UiAutomationHintFactory.CreateHint, false));
                if (windowHints != null)
                {
                    hints.AddRange(windowHints);
                }
            }
            return new HintSession
            {
                Hints = hints,
                OwningWindow = targets[0].Key,
                OwningWindowBounds = overlayBounds,
            };
        }

        /// <summary>
        /// Enumerates all the hints from the given window. Runs on the UIA worker thread.
        /// </summary>
        /// <param name="hWnd">The window to get hints from</param>
        /// <param name="hintFactory">The factory to use to create each hint in the session</param>
        /// <param name="allElements">Also scan elements no hint can act on (debug overlay)</param>
        /// <returns>A hint session, else null if the window could not be enumerated</returns>
        private static HintSession EnumWindowHints(IntPtr hWnd, HintFactoryMethod hintFactory, bool allElements)
        {
            Rect windowBounds;
            if (!TryGetWindowBounds(hWnd, out windowBounds))
            {
                return null;
            }

            var hints = WindowHintScanner.Scan(new WindowScanTarget(hWnd, windowBounds, windowBounds, hintFactory, false, allElements));
            if (hints == null)
            {
                return null;
            }
            return new HintSession
            {
                Hints = hints,
                OwningWindow = hWnd,
                OwningWindowBounds = windowBounds,
            };
        }
    }
}
