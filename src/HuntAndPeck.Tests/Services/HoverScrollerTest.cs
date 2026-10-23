using System;
using System.Collections.Generic;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class HoverScrollerTest
    {
        private const int Escape = 0x1B;
        private const int PageUp = 0x21;
        private const int PageDown = 0x22;
        private const int Left = 0x25;
        private const int Up = 0x26;
        private const int Right = 0x27;
        private const int Down = 0x28;

        [Theory]
        [InlineData(Up, HoverScroller.WheelDelta, 0)]
        [InlineData(Down, -HoverScroller.WheelDelta, 0)]
        [InlineData(Left, 0, -HoverScroller.WheelDelta)]
        [InlineData(Right, 0, HoverScroller.WheelDelta)]
        [InlineData(PageUp, HoverScroller.PageNotches * HoverScroller.WheelDelta, 0)]
        [InlineData(PageDown, -HoverScroller.PageNotches * HoverScroller.WheelDelta, 0)]
        public void ScrollKey_TurnsTheWheel_AndIsSwallowed(int virtualKey, int vertical, int horizontal)
        {
            var fake = new FakeSession();

            var swallowed = fake.Press(virtualKey);

            Assert.True(swallowed);
            Assert.Equal(Tuple.Create(vertical, horizontal), Assert.Single(fake.Wheels));
            Assert.True(fake.Scroller.Active);
            Assert.Equal(0, fake.HookDisposals);
        }

        [Fact]
        public void ScrollKeys_KeepScrolling_UntilStopped()
        {
            var fake = new FakeSession();

            Assert.True(fake.Press(Down));
            Assert.True(fake.Press(Down));
            Assert.True(fake.Press(Up));

            Assert.Equal(
                new[] { Tuple.Create(-120, 0), Tuple.Create(-120, 0), Tuple.Create(120, 0) },
                fake.Wheels);
            Assert.True(fake.Scroller.Active);
        }

        [Theory]
        [InlineData(0x10)] // Shift
        [InlineData(0x11)] // Ctrl
        [InlineData(0x12)] // Alt
        [InlineData(0xA0)] // Left shift
        [InlineData(0xA1)] // Right shift
        [InlineData(0xA2)] // Left Ctrl
        [InlineData(0xA3)] // Right Ctrl
        [InlineData(0xA4)] // Left Alt
        [InlineData(0xA5)] // Right Alt
        [InlineData(0x5B)] // Left Win
        [InlineData(0x5C)] // Right Win
        public void Modifier_PassesThrough_AndKeepsScrolling(int virtualKey)
        {
            var fake = new FakeSession();

            var swallowed = fake.Press(virtualKey);

            Assert.False(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.True(fake.Scroller.Active);
            Assert.Equal(0, fake.HookDisposals);
        }

        [Fact]
        public void Escape_IsSwallowed_AndStops()
        {
            var fake = new FakeSession();

            var swallowed = fake.Press(Escape);

            Assert.True(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.False(fake.Scroller.Active);
            Assert.Equal(1, fake.HookDisposals);
        }

        [Theory]
        [InlineData(0x41)] // A
        [InlineData(0xBA)] // ;
        public void OtherKey_PassesThrough_AndStops(int virtualKey)
        {
            var fake = new FakeSession();

            var swallowed = fake.Press(virtualKey);

            Assert.False(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.False(fake.Scroller.Active);
            Assert.Equal(1, fake.HookDisposals);
        }

        [Fact]
        public void ForegroundChanged_ArrowPassesThrough_AndStops()
        {
            var fake = new FakeSession();
            fake.Foreground = new IntPtr(2);

            var swallowed = fake.Press(Down);

            Assert.False(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.False(fake.Scroller.Active);
            Assert.Equal(1, fake.HookDisposals);
        }

        [Fact]
        public void MouseMoved_ArrowPassesThrough_AndStops()
        {
            var fake = new FakeSession();
            fake.Cursor = new POINT(100 + HoverScroller.MaxCursorDrift + 1, 200);

            var swallowed = fake.Press(Down);

            Assert.False(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.False(fake.Scroller.Active);
            Assert.Equal(1, fake.HookDisposals);
        }

        [Fact]
        public void CursorJitterWithinDrift_KeepsScrolling()
        {
            var fake = new FakeSession();
            fake.Cursor = new POINT(100 - HoverScroller.MaxCursorDrift, 200 + HoverScroller.MaxCursorDrift);

            Assert.True(fake.Press(Down));
            Assert.True(fake.Scroller.Active);
        }

        [Theory]
        [InlineData(0x10, Down)] // Shift+Down: extend the selection
        [InlineData(0x11, Down)] // Ctrl+Down: the app's scroll-line key; as Ctrl+wheel it would zoom
        [InlineData(0x11, PageDown)]
        [InlineData(0x5B, Left)] // Win+Left: snap the window
        [InlineData(0x5C, Up)]
        public void ScrollKeyWithChordModifier_PassesThrough_AndStops(int modifier, int virtualKey)
        {
            var fake = new FakeSession();
            fake.Held.Add(modifier);

            var swallowed = fake.Press(virtualKey);

            Assert.False(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.False(fake.Scroller.Active);
        }

        [Fact]
        public void ScrollKeyWithAltStillHeld_Scrolls()
        {
            // Alt is often still held from the Alt + last letter that hovered
            var fake = new FakeSession();
            fake.Held.Add(0x12);

            Assert.True(fake.Press(Down));
            Assert.Equal(Tuple.Create(-HoverScroller.WheelDelta, 0), Assert.Single(fake.Wheels));
        }

        [Fact]
        public void ForegroundChanged_ModifierDoesNotStop()
        {
            var fake = new FakeSession();
            fake.Foreground = new IntPtr(2);

            // Alt+Tab: the Alt press itself is not what ends the scrolling; the next other key is
            Assert.False(fake.Press(0xA4));

            Assert.True(fake.Scroller.Active);
            Assert.Equal(0, fake.HookDisposals);
        }

        [Fact]
        public void AfterStopped_ArrowPassesThrough_WithoutScrolling()
        {
            var fake = new FakeSession();
            fake.Press(Escape);

            var swallowed = fake.Press(Down);

            Assert.False(swallowed);
            Assert.Empty(fake.Wheels);
            Assert.Equal(1, fake.HookDisposals);
        }

        [Fact]
        public void WheelFails_PassesThrough_AndStops()
        {
            var fake = new FakeSession();
            fake.WheelError = new InvalidOperationException("blocked");

            var swallowed = fake.Press(Down);

            Assert.False(swallowed);
            Assert.False(fake.Scroller.Active);
            Assert.Equal(1, fake.HookDisposals);
            Assert.False(fake.Press(Down));
            Assert.Empty(fake.Wheels);
        }

        [Fact]
        public void Dispose_DisposesTheHookOnce()
        {
            var fake = new FakeSession();

            fake.Scroller.Dispose();
            fake.Scroller.Dispose();

            Assert.False(fake.Scroller.Active);
            Assert.Equal(1, fake.HookDisposals);
        }

        [Fact]
        public void Constructor_InstallsTheHookOnce_AndIsActive()
        {
            var fake = new FakeSession();

            Assert.Equal(1, fake.HooksInstalled);
            Assert.True(fake.Scroller.Active);
        }

        /// <summary>A scroller over a fake hook, wheel and foreground window</summary>
        private sealed class FakeSession
        {
            private Func<int, bool> _handler;

            public FakeSession()
            {
                Scroller = new HoverScroller(
                    handler =>
                    {
                        HooksInstalled++;
                        _handler = handler;
                        return new Disposal(() => HookDisposals++);
                    },
                    (vertical, horizontal) =>
                    {
                        if (WheelError != null)
                        {
                            throw WheelError;
                        }
                        Wheels.Add(Tuple.Create(vertical, horizontal));
                    },
                    () => Foreground,
                    () => Cursor,
                    key => Held.Contains(key));
            }

            public HoverScroller Scroller { get; }

            public POINT Cursor { get; set; } = new POINT(100, 200);

            /// <summary>Virtual keys held now</summary>
            public HashSet<int> Held { get; } = new HashSet<int>();

            public List<Tuple<int, int>> Wheels { get; } = new List<Tuple<int, int>>();

            public IntPtr Foreground { get; set; } = new IntPtr(1);

            public Exception WheelError { get; set; }

            public int HooksInstalled { get; private set; }

            public int HookDisposals { get; private set; }

            /// <returns>True if the key was swallowed</returns>
            public bool Press(int virtualKey) => _handler(virtualKey);
        }

        private sealed class Disposal : IDisposable
        {
            private readonly Action _dispose;

            public Disposal(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose() => _dispose();
        }
    }
}
