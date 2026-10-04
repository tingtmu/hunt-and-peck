using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class HintDedupTest
    {
        private static readonly IntPtr WindowA = new IntPtr(1);
        private static readonly IntPtr WindowB = new IntPtr(2);

        [Fact]
        public void FallbackInsideAnotherHint_IsDropped()
        {
            var button = new TestHint("button", WindowA, new Rect(149, 1008, 66, 72), false);
            var icon = new TestHint("icon", WindowA, new Rect(163, 1026, 36, 36), true);

            Assert.Equal(new[] { "button" }, Names(HintDedup.DropContained(new Hint[] { button, icon }, IsFallback)));
        }

        [Fact]
        public void NonFallbackInsideAnotherHint_IsKept()
        {
            var outer = new TestHint("outer", WindowA, new Rect(0, 0, 100, 100), false);
            var inner = new TestHint("inner", WindowA, new Rect(10, 10, 20, 20), false);

            Assert.Equal(new[] { "outer", "inner" }, Names(HintDedup.DropContained(new Hint[] { outer, inner }, IsFallback)));
        }

        [Fact]
        public void FallbackInsideHintOfAnotherWindow_IsKept()
        {
            var other = new TestHint("other", WindowB, new Rect(0, 0, 100, 100), false);
            var fallback = new TestHint("fallback", WindowA, new Rect(10, 10, 20, 20), true);

            Assert.Equal(new[] { "other", "fallback" }, Names(HintDedup.DropContained(new Hint[] { other, fallback }, IsFallback)));
        }

        [Fact]
        public void SideBySideFallbacks_AreKept()
        {
            var first = new TestHint("first", WindowA, new Rect(149, 1008, 66, 72), true);
            var second = new TestHint("second", WindowA, new Rect(215, 1008, 66, 72), true);

            Assert.Equal(new[] { "first", "second" }, Names(HintDedup.DropContained(new Hint[] { first, second }, IsFallback)));
        }

        [Fact]
        public void IdenticalFallbacks_KeepTheFirst()
        {
            var first = new TestHint("first", WindowA, new Rect(0, 0, 50, 50), true);
            var second = new TestHint("second", WindowA, new Rect(0, 0, 50, 50), true);

            Assert.Equal(new[] { "first" }, Names(HintDedup.DropContained(new Hint[] { first, second }, IsFallback)));
        }

        [Fact]
        public void FallbackWithSameBoundsAsNormalHint_IsDropped()
        {
            var fallback = new TestHint("fallback", WindowA, new Rect(0, 0, 50, 50), true);
            var invoke = new TestHint("invoke", WindowA, new Rect(0, 0, 50, 50), false);

            Assert.Equal(new[] { "invoke" }, Names(HintDedup.DropContained(new Hint[] { fallback, invoke }, IsFallback)));
        }

        private static bool IsFallback(Hint hint) => ((TestHint)hint).IsFallback;

        private static IEnumerable<string> Names(IEnumerable<Hint> hints) => hints.Select(x => ((TestHint)x).Name);

        private sealed class TestHint : Hint
        {
            public TestHint(string name, IntPtr window, Rect bounds, bool isFallback)
                : base(window, bounds)
            {
                Name = name;
                IsFallback = isFallback;
            }

            public string Name { get; }

            public bool IsFallback { get; }

            public override void Invoke()
            {
            }
        }
    }
}
