using System;
using System.Collections.Generic;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class MenuPopupWatcherTest
    {
        private const uint App = 10;
        private const uint OtherApp = 20;
        private static readonly IntPtr Owner = new IntPtr(1);

        [Fact]
        public void NewPopupOfTheApp_IsFoundAfterItSettles()
        {
            var windows = new FakeWindows();
            var clock = new FakeClock();
            var watcher = new MenuPopupWatcher(windows, clock.Delay, () => clock.Now);
            var before = watcher.Snapshot();
            var opened = false;
            clock.OnAdvanced = now =>
            {
                if (!opened && now >= TimeSpan.FromMilliseconds(48))
                {
                    opened = true;
                    windows.Add(new TopLevelWindow(new IntPtr(5), App, "#32768", true));
                }
            };

            var menus = watcher.WaitForNewAsync(Owner, before).Result;

            Assert.Equal(new[] { new IntPtr(5) }, menus);
            Assert.Equal(MenuPopupWatcher.SettleDelay, clock.Delays[clock.Delays.Count - 1]);
        }

        [Fact]
        public void DialogsOtherAppsShadowsAndExistingPopups_AreNotMenus()
        {
            var windows = new FakeWindows();
            windows.Add(new TopLevelWindow(new IntPtr(4), App, "#32768", true));
            var clock = new FakeClock();
            var watcher = new MenuPopupWatcher(windows, clock.Delay, () => clock.Now);
            var before = watcher.Snapshot();
            windows.Add(new TopLevelWindow(new IntPtr(5), App, "#32770", false));
            windows.Add(new TopLevelWindow(new IntPtr(6), OtherApp, "#32768", true));
            windows.Add(new TopLevelWindow(new IntPtr(7), App, "SysShadow", true));
            windows.Add(new TopLevelWindow(new IntPtr(8), App, "tooltips_class32", true));

            var menus = watcher.WaitForNewAsync(Owner, before).Result;

            Assert.Empty(menus);
            Assert.True(clock.Now >= MenuPopupWatcher.AppearTimeout);
        }

        [Fact]
        public void AnyVisible_IsFalseOnceTheMenuCloses()
        {
            var windows = new FakeWindows();
            var menu = new TopLevelWindow(new IntPtr(5), App, "#32768", true);
            windows.Add(menu);
            var clock = new FakeClock();
            var watcher = new MenuPopupWatcher(windows, clock.Delay, () => clock.Now);

            Assert.True(watcher.AnyVisible(new[] { menu.Handle }));
            windows.Remove(menu);
            Assert.False(watcher.AnyVisible(new[] { menu.Handle }));
        }

        private sealed class FakeWindows : ITopLevelWindowSource
        {
            private readonly List<TopLevelWindow> _windows = new List<TopLevelWindow>
            {
                new TopLevelWindow(Owner, App, "Notepad", false),
            };

            public void Add(TopLevelWindow window) => _windows.Add(window);

            public void Remove(TopLevelWindow window) => _windows.Remove(window);

            public IReadOnlyList<TopLevelWindow> VisibleWindows() => new List<TopLevelWindow>(_windows);

            public uint ProcessOf(IntPtr hWnd)
            {
                var window = _windows.Find(x => x.Handle == hWnd);
                return window == null ? 0 : window.ProcessId;
            }
        }
    }
}
