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
using UIAutomationClient;

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

        /// <summary>
        /// A scan is stopped at the first per-element UIA timeout: the element didn't answer within the
        /// transaction timeout, so the target is hung and every further element would cost another timeout.
        /// </summary>
        private const int MaxElementTimeoutsPerScan = 1;

        /// <summary>Only the first few skipped elements of a scan are logged individually</summary>
        private const int MaxLoggedSkipsPerScan = 3;

        private readonly UiaExecutor _executor;

        /// <param name="notifyUser">Optional user notification (tray balloon) for persistent UIA trouble</param>
        public UiAutomationHintProviderService(Action<string> notifyUser = null)
        {
            _executor = new UiaExecutor("HuntAndPeck UIA worker", notifyUser);
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

        /// <summary>
        /// Enumerates all the hints from the given window. Runs on the UIA worker thread.
        /// </summary>
        /// <param name="hWnd">The window to get hints from</param>
        /// <param name="hintFactory">The factory to use to create each hint in the session</param>
        /// <returns>A hint session, else null if the window could not be enumerated</returns>
        private static HintSession EnumWindowHints(IntPtr hWnd, HintFactoryMethod hintFactory)
        {
            var stopwatch = Stopwatch.StartNew();

            // Window bounds, in physical pixels (the process is per-monitor DPI aware)
            var rawWindowBounds = new RECT();
            if (!User32.GetWindowRect(hWnd, ref rawWindowBounds))
            {
                Trace.TraceWarning("GetWindowRect failed for window {0}, error {1}", hWnd, Marshal.GetLastWin32Error());
                return null;
            }
            Rect windowBounds = rawWindowBounds;

            var scan = UiAutomationElementScanner.TryScan(hWnd);
            if (scan == null)
            {
                return null;
            }

            var findMs = stopwatch.ElapsedMilliseconds;
            var hints = CreateHints(hWnd, windowBounds, scan, hintFactory);

            Trace.TraceInformation(
                "Window {0}: {1} elements, {2} hints in {3} ms (find {4} ms, {5} properties)",
                hWnd, scan.Elements.Count, hints.Count, stopwatch.ElapsedMilliseconds, findMs, scan.Source);
            return new HintSession
            {
                Hints = hints,
                OwningWindow = hWnd,
                OwningWindowBounds = windowBounds,
            };
        }

        /// <summary>
        /// Creates a hint for each element, skipping (and counting) elements that vanish or fail mid-scan
        /// </summary>
        private static List<Hint> CreateHints(IntPtr hWnd, Rect windowBounds, ElementScan scan, HintFactoryMethod hintFactory)
        {
            var result = new List<Hint>();
            var skipped = 0;
            var timeouts = 0;

            foreach (var element in scan.Elements)
            {
                try
                {
                    var hint = CreateHint(hWnd, windowBounds, element, scan.Source, hintFactory);
                    if (hint != null)
                    {
                        result.Add(hint);
                    }
                }
                catch (Exception ex) when (UiaErrors.IsTargetFailure(ex))
                {
                    skipped++;
                    if (skipped <= MaxLoggedSkipsPerScan)
                    {
                        Trace.TraceInformation("Window {0}: skipped element: {1}", hWnd, UiaErrors.Describe(ex));
                    }
                    if (UiaErrors.IsTimeout(ex) && ++timeouts >= MaxElementTimeoutsPerScan)
                    {
                        Trace.TraceWarning("Window {0}: scan stopped after {1} UIA timeouts (target not responding)", hWnd, timeouts);
                        break;
                    }
                }
            }

            if (skipped > 0)
            {
                Trace.TraceInformation("Window {0}: {1} of {2} elements skipped (vanished or not responding)", hWnd, skipped, scan.Elements.Count);
            }
            return result;
        }

        /// <summary>
        /// Creates the hint for one element if it is visible within the window
        /// </summary>
        private static Hint CreateHint(
            IntPtr hWnd,
            Rect windowBounds,
            IUIAutomationElement element,
            UiaPropertySource source,
            HintFactoryMethod hintFactory)
        {
            // Physical screen pixels, same unit as the window bounds
            var bounds = UiAutomationElementCache.ReadBounds(element, source);
            Rect windowCoords;
            if (!HintBounds.TryToWindowCoordinates(bounds.left, bounds.top, bounds.right, bounds.bottom, windowBounds, out windowCoords))
            {
                return null;
            }
            return hintFactory(hWnd, windowCoords, element, source);
        }
    }
}