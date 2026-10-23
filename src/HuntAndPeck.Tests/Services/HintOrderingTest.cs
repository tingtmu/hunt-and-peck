using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class HintOrderingTest
    {
        [Fact]
        public void Rows_TopToBottom_EachLeftToRight()
        {
            var hints = new Hint[]
            {
                new TestHint("bottom-right", 200, 100),
                new TestHint("top-right", 200, 0),
                new TestHint("bottom-left", 0, 100),
                new TestHint("top-left", 0, 0),
            };

            Assert.Equal(new[] { "top-left", "top-right", "bottom-left", "bottom-right" }, Names(HintOrdering.ByReadingOrder(hints)));
        }

        [Fact]
        public void SlightlyMisalignedControls_ShareARow()
        {
            // Height 20, so tops within 10 px of the row's first hint are on the same row
            var hints = new Hint[]
            {
                new TestHint("button", 300, 4),
                new TestHint("textbox", 100, 0),
                new TestHint("checkbox", 0, 8),
            };

            Assert.Equal(new[] { "checkbox", "textbox", "button" }, Names(HintOrdering.ByReadingOrder(hints)));
        }

        [Fact]
        public void HintBelowTolerance_StartsANewRow()
        {
            var hints = new Hint[]
            {
                new TestHint("lower", 0, 11),
                new TestHint("upper", 100, 0),
            };

            Assert.Equal(new[] { "upper", "lower" }, Names(HintOrdering.ByReadingOrder(hints)));
        }

        [Fact]
        public void EqualPositions_KeepInputOrder()
        {
            var hints = new Hint[]
            {
                new TestHint("first", 0, 0),
                new TestHint("second", 0, 0),
            };

            Assert.Equal(new[] { "first", "second" }, Names(HintOrdering.ByReadingOrder(hints)));
        }

        [Fact]
        public void DoesNotChangeTheInput()
        {
            var hints = new List<Hint> { new TestHint("b", 100, 0), new TestHint("a", 0, 0) };

            HintOrdering.ByReadingOrder(hints);

            Assert.Equal(new[] { "b", "a" }, Names(hints));
        }

        [Fact]
        public void EmptyAndSingle()
        {
            Assert.Empty(HintOrdering.ByReadingOrder(new List<Hint>()));
            Assert.Equal(new[] { "only" }, Names(HintOrdering.ByReadingOrder(new Hint[] { new TestHint("only", 5, 5) })));
        }

        private static IEnumerable<string> Names(IEnumerable<Hint> hints) => hints.Select(x => ((TestHint)x).Name);

        private sealed class TestHint : Hint
        {
            public TestHint(string name, double left, double top)
                : base(IntPtr.Zero, new Rect(left, top, 50, 20))
            {
                Name = name;
            }

            public string Name { get; }

            public override void Invoke()
            {
            }
        }
    }
}
