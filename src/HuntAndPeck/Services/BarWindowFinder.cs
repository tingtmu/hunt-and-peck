using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Finds the edge-docked bar windows (see <see cref="BarClassifier"/>) on one monitor
    /// </summary>
    internal static class BarWindowFinder
    {
        /// <param name="scope">The monitor to look on</param>
        /// <returns>The bar windows on the monitor that are not fully covered, in z-order (topmost first)</returns>
        public static List<IntPtr> FindBars(BarsScope scope)
        {
            uint ownProcess;
            using (var process = Process.GetCurrentProcess())
            {
                ownProcess = (uint)process.Id;
            }

            var bars = new List<IntPtr>();
            WindowEnumeration.EnumWindowsProc callback = (hWnd, lParam) =>
            {
                try
                {
                    if (IsVisibleBar(hWnd, scope, ownProcess))
                    {
                        bars.Add(hWnd);
                    }
                }
                catch (Exception ex)
                {
                    // Never let an exception unwind through the native callback; skip the window
                    Trace.TraceWarning("Bars: inspecting window {0} failed, skipping it: {1}", hWnd, ex);
                }
                return true;
            };

            if (!WindowEnumeration.EnumWindows(callback, IntPtr.Zero))
            {
                Trace.TraceWarning("Bars: EnumWindows failed, error {0}; using the {1} bars found so far", Marshal.GetLastWin32Error(), bars.Count);
            }
            GC.KeepAlive(callback);
            return bars;
        }

        private static bool IsVisibleBar(IntPtr hWnd, BarsScope scope, uint ownProcess)
        {
            var candidate = Describe(hWnd, scope, ownProcess);
            if (candidate == null)
            {
                return false;
            }

            var edge = BarClassifier.Classify(candidate);
            if (edge == BarEdge.None)
            {
                return false;
            }
            if (!IsUncovered(hWnd, Rect.Intersect(candidate.Bounds, candidate.Monitor)))
            {
                Trace.TraceInformation("Bars: window {0} ({1}) at {2} is covered by other windows; leaving it out", hWnd, candidate.ClassName, candidate.Bounds);
                return false;
            }

            Trace.TraceInformation("Bars: window {0} ({1}) at {2} is a bar docked to the {3} edge", hWnd, candidate.ClassName, candidate.Bounds, edge);
            return true;
        }

        /// <summary>
        /// True if the bar itself is the window at one or more of its <see cref="BarClassifier.SamplePoints"/>
        /// (e.g. not covered by a full screen window)
        /// </summary>
        private static bool IsUncovered(IntPtr bar, Rect onMonitor)
        {
            return BarClassifier.SamplePoints(onMonitor).Any(point =>
            {
                var hit = WindowEnumeration.WindowFromPoint(point);
                return hit != IntPtr.Zero && WindowEnumeration.GetAncestor(hit, WindowEnumeration.GA_ROOT) == bar;
            });
        }

        /// <returns>The window's properties, else null if it is invisible, on another monitor or its bounds are unknown</returns>
        private static BarCandidate Describe(IntPtr hWnd, BarsScope scope, uint ownProcess)
        {
            // Cheap checks first: most top-level windows are invisible
            if (!WindowEnumeration.IsWindowVisible(hWnd))
            {
                return null;
            }
            if (User32.MonitorFromWindow(hWnd, User32.MONITOR_DEFAULTTONEAREST) != scope.Monitor)
            {
                return null;
            }

            var rect = new RECT();
            if (!User32.GetWindowRect(hWnd, ref rect))
            {
                return null;
            }

            uint processId;
            WindowEnumeration.GetWindowThreadProcessId(hWnd, out processId);
            return new BarCandidate
            {
                Handle = hWnd,
                ClassName = WindowEnumeration.GetClassName(hWnd),
                Bounds = rect,
                Monitor = scope.MonitorBounds,
                MonitorScale = scope.MonitorScale,
                ExStyle = WindowEnumeration.GetExStyle(hWnd),
                Visible = true,
                Cloaked = IsCloaked(hWnd),
                Minimized = WindowEnumeration.IsIconic(hWnd),
                OwnProcess = processId == ownProcess,
            };
        }

        /// <remarks>A window whose cloaked state can't be read counts as not cloaked, as before DWM cloaking existed</remarks>
        private static bool IsCloaked(IntPtr hWnd)
        {
            int cloaked;
            return Dwmapi.DwmGetWindowAttribute(hWnd, Dwmapi.DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0;
        }
    }
}
