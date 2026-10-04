using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;
using HuntAndPeck.Services.Uia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            return EnumForegroundAsync(UiAutomationHintFactory.CreateHint);
        }

        public Task<HintSession> EnumHintsAsync(IntPtr hWnd)
        {
            return EnumAsync(hWnd, UiAutomationHintFactory.CreateHint);
        }

        public Task<HintSession> EnumDebugHintsAsync()
        {
            return EnumForegroundAsync(UiAutomationHintFactory.CreateDebugHint);
        }

        public Task<HintSession> EnumDebugHintsAsync(IntPtr hWnd)
        {
            return EnumAsync(hWnd, UiAutomationHintFactory.CreateDebugHint);
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
        /// <remarks>
        /// Target failures (element gone, app hung/closed) and timeouts are logged and do not fault the task
        /// </remarks>
        public async Task InvokeHintAsync(Hint hint)
        {
            try
            {
                await _executor.RunAsync(() => InvokeOnWorker(hint), InvocationTimeout).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsShutdown(ex))
            {
                // Checked first: ObjectDisposedException is also an InvalidOperationException
                Trace.TraceInformation("Invoking {0} skipped, UI Automation is shutting down: {1}", hint.GetType().Name, ex.Message);
            }
            catch (Exception ex) when (UiaErrors.IsTargetFailure(ex))
            {
                Trace.TraceWarning("Invoking {0} in window {1} failed: {2}", hint.GetType().Name, hint.OwningWindow, UiaErrors.Describe(ex));
            }
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

        private Task<HintSession> EnumForegroundAsync(HintFactoryMethod hintFactory)
        {
            var foregroundWindow = User32.GetForegroundWindow();
            return EnumAsync(foregroundWindow, hintFactory);
        }

        private async Task<HintSession> EnumAsync(IntPtr hWnd, HintFactoryMethod hintFactory)
        {
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return await _executor.RunAsync(() => EnumWindowHints(hWnd, hintFactory), EnumerationTimeout).ConfigureAwait(false);
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
        /// Enumerates all the hints from the given window. Runs on the UIA worker thread.
        /// </summary>
        /// <param name="hWnd">The window to get hints from</param>
        /// <param name="hintFactory">The factory to use to create each hint in the session</param>
        /// <returns>A hint session, else null if the window could not be enumerated</returns>
        private static HintSession EnumWindowHints(IntPtr hWnd, HintFactoryMethod hintFactory)
        {
            Rect windowBounds;
            if (!TryGetWindowBounds(hWnd, out windowBounds))
            {
                return null;
            }

            var hints = WindowHintScanner.Scan(new WindowScanTarget(hWnd, windowBounds, windowBounds, hintFactory, false));
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
