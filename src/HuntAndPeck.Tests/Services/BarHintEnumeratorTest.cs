using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class BarHintEnumeratorTest
    {
        private static readonly IntPtr Taskbar = new IntPtr(1);
        private static readonly IntPtr Bar = new IntPtr(2);
        private static readonly Rect Monitor = new Rect(0, 0, 1920, 1080);
        private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task Windows_AreCombined_OverlayIsUnionClippedToMonitor()
        {
            var scanner = new FakeScanner();
            var enumerator = scanner.CreateEnumerator();

            var session = await enumerator.EnumAsync(Windows(Pair(Taskbar, 0, 1078, 1920, 72), Pair(Bar, 0, 0, 1920, 60)), Monitor);

            Assert.Equal(new Rect(0, 0, 1920, 1080), session.OwningWindowBounds);
            Assert.Equal(Taskbar, session.OwningWindow);
            Assert.Equal(2, session.Hints.Count);
            Assert.Equal(new Rect(0, 1078, 1920, 2), scanner.Scans[0].WindowBounds);
            Assert.All(scanner.Scans, x => Assert.Equal(session.OwningWindowBounds, x.OverlayBounds));
            Assert.All(scanner.Scans, x => Assert.True(x.BarsMode));
        }

        [Fact]
        public async Task WindowsOffTheMonitor_AreLeftOut()
        {
            var scanner = new FakeScanner();

            var session = await scanner.CreateEnumerator().EnumAsync(Windows(Pair(Taskbar, 1920, 0, 2560, 48), Pair(Bar, 0, 0, 1920, 60)), Monitor);

            Assert.Equal(Bar, session.OwningWindow);
            Assert.Equal(new[] { Bar }, scanner.Scans.Select(x => x.Handle));
        }

        [Fact]
        public async Task NoWindowOnMonitor_ReturnsNull()
        {
            var scanner = new FakeScanner();

            Assert.Null(await scanner.CreateEnumerator().EnumAsync(Windows(Pair(Bar, 1920, 0, 2560, 48)), Monitor));
            Assert.Empty(scanner.Scans);
        }

        [Fact]
        public async Task FailingWindow_IsLeftOut_OthersKept()
        {
            var scanner = new FakeScanner { Results = { [Taskbar] = null } };

            var session = await scanner.CreateEnumerator().EnumAsync(Windows(Pair(Taskbar, 0, 1008, 1920, 72), Pair(Bar, 0, 0, 1920, 60)), Monitor);

            Assert.Single(session.Hints);
            Assert.Equal(Bar, session.Hints[0].OwningWindow);
        }

        [Fact]
        public async Task EmptyWindow_IsRetriedOnce_OnlyTheFirstTimeSeen()
        {
            var scanner = new FakeScanner { EmptyScansLeft = { [Bar] = 1 } };
            var enumerator = scanner.CreateEnumerator();

            var first = await enumerator.EnumAsync(Windows(Pair(Bar, 0, 0, 1920, 60)), Monitor);
            Assert.Single(first.Hints);
            Assert.Equal(2, scanner.Scans.Count);
            Assert.Equal(new[] { BarHintEnumerator.EmptyRetryDelay }, scanner.Clock.Delays);

            scanner.EmptyScansLeft[Bar] = 1;
            var second = await enumerator.EnumAsync(Windows(Pair(Bar, 0, 0, 1920, 60)), Monitor);
            Assert.Empty(second.Hints);
            Assert.Equal(3, scanner.Scans.Count);
        }

        [Fact]
        public async Task SlowScans_StopAtOverallDeadline_AndShrinkTheTimeout()
        {
            var scanner = new FakeScanner { ScanDuration = TimeSpan.FromSeconds(4) };
            var windows = Windows(Pair(Taskbar, 0, 1008, 1920, 72), Pair(Bar, 0, 0, 1920, 60), Pair(new IntPtr(3), 0, 60, 1920, 40));

            var session = await scanner.CreateEnumerator().EnumAsync(windows, Monitor);

            // 7 s deadline: the first scan may take 5 s, the second only the 3 s left, the third is skipped
            Assert.Equal(new[] { WindowTimeout, TimeSpan.FromSeconds(3) }, scanner.Timeouts);
            Assert.Equal(2, session.Hints.Count);
        }

        [Fact]
        public async Task NoRetry_WhenLessThanBudgetLeft()
        {
            var scanner = new FakeScanner { ScanDuration = TimeSpan.FromSeconds(4), EmptyScansLeft = { [Bar] = 1 } };

            // The taskbar takes 4 s, the bar the 3 s left: no time for the bar's retry
            var session = await scanner.CreateEnumerator().EnumAsync(Windows(Pair(Taskbar, 0, 1008, 1920, 72), Pair(Bar, 0, 0, 1920, 60)), Monitor);

            Assert.Equal(Taskbar, Assert.Single(session.Hints).OwningWindow);
            Assert.Equal(2, scanner.Scans.Count);
        }

        private static List<KeyValuePair<IntPtr, Rect>> Windows(params KeyValuePair<IntPtr, Rect>[] windows) => windows.ToList();

        private static KeyValuePair<IntPtr, Rect> Pair(IntPtr hWnd, double x, double y, double width, double height)
        {
            return new KeyValuePair<IntPtr, Rect>(hWnd, new Rect(x, y, width, height));
        }

        private sealed class FakeScanner
        {
            public FakeClock Clock { get; } = new FakeClock();

            public List<WindowScanTarget> Scans { get; } = new List<WindowScanTarget>();

            public List<TimeSpan> Timeouts { get; } = new List<TimeSpan>();

            /// <summary>Per window: null to fail; missing: one hint</summary>
            public Dictionary<IntPtr, List<Hint>> Results { get; } = new Dictionary<IntPtr, List<Hint>>();

            /// <summary>Per window: how many scans return no hints first</summary>
            public Dictionary<IntPtr, int> EmptyScansLeft { get; } = new Dictionary<IntPtr, int>();

            public TimeSpan ScanDuration { get; set; }

            public BarHintEnumerator CreateEnumerator()
            {
                return new BarHintEnumerator(Scan, Clock.Delay, () => Clock.Now, WindowTimeout);
            }

            private Task<List<Hint>> Scan(WindowScanTarget target, TimeSpan timeout)
            {
                Scans.Add(target);
                Timeouts.Add(timeout);
                Clock.Advance(ScanDuration < timeout ? ScanDuration : timeout);

                int empty;
                if (EmptyScansLeft.TryGetValue(target.Handle, out empty) && empty > 0)
                {
                    EmptyScansLeft[target.Handle] = empty - 1;
                    return Task.FromResult(new List<Hint>());
                }

                List<Hint> result;
                if (Results.TryGetValue(target.Handle, out result))
                {
                    return Task.FromResult(result);
                }
                return Task.FromResult(new List<Hint> { new TestHint(target.Handle) });
            }
        }

        private sealed class TestHint : Hint
        {
            public TestHint(IntPtr window)
                : base(window, new Rect(0, 0, 10, 10))
            {
            }

            public override void Invoke()
            {
            }
        }
    }
}
