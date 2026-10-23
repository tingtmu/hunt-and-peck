using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Tests.Integration
{
    /// <summary>
    /// What the integration tests ask of the fixture window. Every UIA call runs on the app's UIA worker thread.
    /// Bounds are physical screen pixels relative to the window's top left, the same space as the hints' bounds.
    /// </summary>
    internal static class FixtureUia
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        /// <summary>The window's hints, created by the code path the app uses for a window</summary>
        public static Task<List<Hint>> ScanHintsAsync(UiaFixtureApp app)
        {
            var handle = app.Handle;
            return app.Executor.RunAsync(() => ScanHints(handle), Timeout);
        }

        /// <summary>The elements the app's scanner finds in the window</summary>
        public static Task<ElementScan> TryScanAsync(UiaFixtureApp app)
        {
            var handle = app.Handle;
            return app.Executor.RunAsync(() => UiAutomationElementScanner.TryScan(handle), Timeout);
        }

        /// <summary>The bounds of every descendant whose property has the value (e.g. its Name or ControlType)</summary>
        public static Task<List<Rect>> FindBoundsAsync(UiaFixtureApp app, int propertyId, object value)
        {
            var handle = app.Handle;
            return app.Executor.RunAsync(() => FindBounds(handle, propertyId, value), Timeout);
        }

        /// <summary>Whether a descendant whose property has the value supports the pattern</summary>
        public static Task<bool> SupportsPatternAsync(UiaFixtureApp app, int propertyId, object value, int patternAvailableId)
        {
            var handle = app.Handle;
            return app.Executor.RunAsync(() => SupportsPattern(handle, propertyId, value, patternAvailableId), Timeout);
        }

        public static Task<bool> InvokeAsync(UiaFixtureApp app, Hint hint)
        {
            return app.Executor.RunAsync(() => { hint.Invoke(); return true; }, Timeout);
        }

        /// <summary>Polls until a descendant named <paramref name="name"/> exists; false on timeout</summary>
        public static async Task<bool> WaitForNameAsync(UiaFixtureApp app, string name)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if ((await FindBoundsAsync(app, UIA_PropertyIds.UIA_NamePropertyId, name).ConfigureAwait(false)).Count > 0)
                    {
                        return true;
                    }
                }
                catch (COMException)
                {
                    // UIA can fail a call transiently while the window is being built or repainted; poll again
                }
                await Task.Delay(50).ConfigureAwait(false);
            }
            return false;
        }

        private static List<Hint> ScanHints(IntPtr handle)
        {
            var windowBounds = WindowBounds(handle);
            var target = new WindowScanTarget(handle, windowBounds, windowBounds, UiAutomationHintFactory.CreateHint, false);
            return WindowHintScanner.Scan(target);
        }

        private static List<Rect> FindBounds(IntPtr handle, int propertyId, object value)
        {
            var found = Find(handle, propertyId, value);
            var result = new List<Rect>();
            for (var i = 0; i < found.Length; ++i)
            {
                result.Add(Relative(handle, found.GetElement(i)));
            }
            return result;
        }

        private static bool SupportsPattern(IntPtr handle, int propertyId, object value, int patternAvailableId)
        {
            var found = Find(handle, propertyId, value);
            return found.Length > 0 && true.Equals(found.GetElement(0).GetCurrentPropertyValue(patternAvailableId));
        }

        private static IUIAutomationElementArray Find(IntPtr handle, int propertyId, object value)
        {
            var automation = UiaAutomationFactory.ForCurrentThread();
            var condition = automation.CreatePropertyCondition(propertyId, value);
            return automation.ElementFromHandle(handle).FindAll(TreeScope.TreeScope_Descendants, condition);
        }

        private static Rect WindowBounds(IntPtr handle)
        {
            var raw = new RECT();
            if (!User32.GetWindowRect(handle, ref raw))
            {
                throw new InvalidOperationException("GetWindowRect failed");
            }
            return raw;
        }

        private static Rect Relative(IntPtr handle, IUIAutomationElement element)
        {
            var window = WindowBounds(handle);
            var r = element.CurrentBoundingRectangle;
            return new Rect(new Point(r.left - window.Left, r.top - window.Top), new Point(r.right - window.Left, r.bottom - window.Top));
        }
    }
}

