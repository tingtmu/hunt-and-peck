using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// What to scan and how to place its hints
    /// </summary>
    internal sealed class WindowScanTarget
    {
        public WindowScanTarget(IntPtr handle, Rect windowBounds, Rect overlayBounds, HintFactoryMethod hintFactory, bool barsMode)
        {
            Handle = handle;
            WindowBounds = windowBounds;
            OverlayBounds = overlayBounds;
            HintFactory = hintFactory;
            BarsMode = barsMode;
        }

        public IntPtr Handle { get; private set; }

        /// <summary>The window's bounds, physical screen pixels; elements outside it get no hint</summary>
        public Rect WindowBounds { get; private set; }

        /// <summary>The overlay's bounds, physical screen pixels; hint bounds are relative to it</summary>
        public Rect OverlayBounds { get; private set; }

        public HintFactoryMethod HintFactory { get; private set; }

        /// <summary>
        /// Bars mode: cache the LegacyIAccessible properties, skip elements covering (nearly) the whole window
        /// (see <see cref="HintBounds.CoversWindow"/>) and drop LegacyIAccessible hints inside other hints
        /// </summary>
        public bool BarsMode { get; private set; }
    }

    /// <summary>
    /// Creates the hints of one window. Runs on the UIA worker thread.
    /// </summary>
    internal static class WindowHintScanner
    {
        /// <summary>
        /// A scan is stopped at the first per-element UIA timeout: the element didn't answer within the
        /// transaction timeout, so the target is hung and every further element would cost another timeout.
        /// </summary>
        private const int MaxElementTimeoutsPerScan = 1;

        /// <summary>Only the first few skipped elements of a scan are logged individually</summary>
        private const int MaxLoggedSkipsPerScan = 3;

        /// <returns>The hints, else null if the window could not be enumerated (logged)</returns>
        public static List<Hint> Scan(WindowScanTarget target)
        {
            var stopwatch = Stopwatch.StartNew();
            var scan = UiAutomationElementScanner.TryScan(target.Handle, target.BarsMode);
            if (scan == null)
            {
                return null;
            }

            var findMs = stopwatch.ElapsedMilliseconds;
            var hints = CreateHints(target, scan);
            if (target.BarsMode)
            {
                hints = HintDedup.DropContained(hints, x => x is UiAutomationLegacyDefaultActionHint || x is UiAutomationClickHint);
            }

            Trace.TraceInformation(
                "Window {0}: {1} elements, {2} hints in {3} ms (find {4} ms, {5} properties)",
                target.Handle, scan.Elements.Count, hints.Count, stopwatch.ElapsedMilliseconds, findMs, scan.Source);
            return hints;
        }

        /// <summary>
        /// Creates a hint for each element, skipping (and counting) elements that vanish or fail mid-scan
        /// </summary>
        private static List<Hint> CreateHints(WindowScanTarget target, ElementScan scan)
        {
            var result = new List<Hint>();
            var skipped = 0;
            var timeouts = 0;

            foreach (var element in scan.Elements)
            {
                try
                {
                    var hint = CreateHint(target, element, scan.Source);
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
                        Trace.TraceInformation("Window {0}: skipped element: {1}", target.Handle, UiaErrors.Describe(ex));
                    }
                    if (UiaErrors.IsTimeout(ex) && ++timeouts >= MaxElementTimeoutsPerScan)
                    {
                        Trace.TraceWarning("Window {0}: scan stopped after {1} UIA timeouts (target not responding)", target.Handle, timeouts);
                        break;
                    }
                }
            }

            if (skipped > 0)
            {
                Trace.TraceInformation("Window {0}: {1} of {2} elements skipped (vanished or not responding)", target.Handle, skipped, scan.Elements.Count);
            }
            return result;
        }

        /// <summary>
        /// Creates the hint for one element if it is visible within the window
        /// </summary>
        private static Hint CreateHint(WindowScanTarget target, IUIAutomationElement element, UiaPropertySource source)
        {
            // Physical screen pixels, same unit as the window bounds
            var bounds = UiAutomationElementCache.ReadBounds(element, source);
            Rect overlayCoords;
            if (!HintBounds.TryToOverlayCoordinates(bounds.left, bounds.top, bounds.right, bounds.bottom, target.WindowBounds, target.OverlayBounds, out overlayCoords))
            {
                return null;
            }

            if (target.BarsMode)
            {
                var screenRect = new Rect(new Point(bounds.left, bounds.top), new Point(bounds.right, bounds.bottom));
                if (HintBounds.CoversWindow(screenRect, target.WindowBounds))
                {
                    return null;
                }
            }
            return target.HintFactory(target.Handle, overlayCoords, element, source);
        }
    }
}
