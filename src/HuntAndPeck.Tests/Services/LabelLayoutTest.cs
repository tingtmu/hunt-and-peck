using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class LabelLayoutTest
    {
        private static readonly Size Label = new Size(20, 10);
        private static readonly Rect Area = new Rect(0, 0, 1000, 1000);

        [Fact]
        public void NoCollision_EveryLabelStaysAtItsCorner()
        {
            var elements = new[] { new Rect(0, 0, 100, 50), new Rect(200, 0, 100, 50), new Rect(0, 100, 100, 50) };

            var placed = Place(elements, Label);

            Assert.All(placed, x => Assert.Equal(new Rect(0, 0, 20, 10), x));
        }

        [Fact]
        public void NoCollision_LabelIsShrunkToAnElementSmallerThanIt()
        {
            // 20x10 label in a 10x10 element: scaled by 0.5 to 10x5
            var placed = Place(new[] { new Rect(0, 0, 10, 10) }, Label);

            Assert.Equal(new Rect(0, 0, 10, 5), Assert.Single(placed));
        }

        [Fact]
        public void FittedSize_IsWhatFitLabelGives()
        {
            var elements = new[] { new Rect(0, 0, 10, 10), new Rect(100, 0, 8, 40), new Rect(300, 0, 500, 500) };
            var label = new Size(30, 12);

            var placed = Place(elements, label);

            for (var i = 0; i < elements.Length; i++)
            {
                Assert.Equal(ActionPoint.FitLabel(elements[i].Size, label), placed[i].Size);
            }
        }

        [Fact]
        public void SharedTopLeft_SmallerElementKeepsTheCorner_LargerSlidesRightInsideItself()
        {
            var small = new Rect(0, 0, 30, 30);
            var large = new Rect(0, 0, 200, 100);

            // Large is listed first, but the smaller element is placed first
            var placed = Place(new[] { large, small }, Label);

            Assert.Equal(new Rect(20, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void ChainOfThree_LabelsSitSideBySideInsideTheLargestElements()
        {
            var a = new Rect(0, 0, 30, 30);
            var b = new Rect(0, 0, 100, 50);
            var c = new Rect(0, 0, 200, 100);

            var placed = Place(new[] { c, b, a }, Label);

            Assert.Equal(new Rect(40, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(20, 0, 20, 10), placed[1]);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[2]);
        }

        [Fact]
        public void OffsetsAreRelativeToTheElementsTopLeft()
        {
            var a = new Rect(300, 200, 30, 30);
            var b = new Rect(300, 200, 100, 50);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(20, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void NoRoomToSlideRight_SlidesDownInsideTheElement()
        {
            // Too narrow for two labels side by side, tall enough for two stacked
            var a = new Rect(100, 100, 30, 25);
            var b = new Rect(100, 100, 30, 25);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, 10, 20, 10), placed[1]);
        }

        [Fact]
        public void NoRoomInsideTheElement_GoesLeftOfIt()
        {
            // Neither a second label to the right nor below fits inside the 30x15 element
            var a = new Rect(100, 100, 30, 15);
            var b = new Rect(100, 100, 30, 15);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(-20, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void NoRoomLeftOfTheElement_GoesAboveIt()
        {
            // At the area's left edge, so just left of the element is outside the area
            var a = new Rect(0, 100, 30, 15);
            var b = new Rect(0, 100, 30, 15);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, -10, 20, 10), placed[1]);
        }

        [Fact]
        public void NothingFits_LabelStaysAtTheCorner()
        {
            // In the area's top left corner: neither left of nor above the element is inside the area
            var a = new Rect(0, 0, 30, 15);
            var b = new Rect(0, 0, 30, 15);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void EmptyArea_NoLabelIsMovedOutsideItsElement()
        {
            var a = new Rect(100, 100, 30, 15);
            var b = new Rect(100, 100, 30, 15);

            var placed = LabelLayout.Place(new[] { a, b }, new[] { Label, Label }, Rect.Empty);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void TouchingEdges_AreNotAnOverlap()
        {
            // The second label's corner is exactly where the first one ends
            var a = new Rect(0, 0, 100, 50);
            var b = new Rect(20, 0, 100, 50);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void TouchingEdgesVertically_AreNotAnOverlap()
        {
            var a = new Rect(0, 0, 100, 50);
            var b = new Rect(0, 10, 100, 50);

            var placed = Place(new[] { a, b }, Label);

            Assert.Equal(new Rect(0, 0, 20, 10), placed[0]);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void MovedLabels_LieInsideTheArea()
        {
            // Many elements piled on corners near the area's edges, forcing labels off their corners
            var area = new Rect(0, 0, 200, 120);
            var elements = new List<Rect>();
            foreach (var corner in new[] { new Point(0, 0), new Point(0, 100), new Point(170, 0), new Point(170, 100), new Point(80, 50) })
            {
                for (var i = 0; i < 6; i++)
                {
                    elements.Add(new Rect(corner.X, corner.Y, 30 + i * 4, 18 + i * 2));
                }
            }

            var placed = LabelLayout.Place(elements, elements.Select(x => Label).ToList(), area);

            var moved = 0;
            for (var i = 0; i < elements.Count; i++)
            {
                if (placed[i].X == 0 && placed[i].Y == 0)
                {
                    continue;
                }
                moved++;
                var label = new Rect(elements[i].X + placed[i].X, elements[i].Y + placed[i].Y, placed[i].Width, placed[i].Height);
                Assert.True(area.Contains(label), "Label " + i + " " + label + " is outside " + area);
            }
            Assert.True(moved > 0);
        }

        [Fact]
        public void MovedLabels_DoNotOverlapEachOther_WhenThereIsRoom()
        {
            var elements = Enumerable.Range(0, 5).Select(i => new Rect(50, 50, 40 + i * 20, 30 + i * 10)).ToList();

            var placed = LabelLayout.Place(elements, elements.Select(x => Label).ToList(), Area);

            var absolute = elements.Select((e, i) => new Rect(e.X + placed[i].X, e.Y + placed[i].Y, placed[i].Width, placed[i].Height)).ToList();
            for (var i = 0; i < absolute.Count; i++)
            {
                for (var j = i + 1; j < absolute.Count; j++)
                {
                    Assert.False(Overlap(absolute[i], absolute[j]), absolute[i] + " overlaps " + absolute[j]);
                }
            }
        }

        [Fact]
        public void EmptyElement_GetsAnEmptyRect_AndBlocksNothing()
        {
            var elements = new[] { Rect.Empty, new Rect(0, 0, 100, 50) };

            var placed = Place(elements, Label);

            Assert.True(placed[0].IsEmpty);
            Assert.Equal(new Rect(0, 0, 20, 10), placed[1]);
        }

        [Fact]
        public void Results_FollowTheOrderOfTheInput()
        {
            // Placing order is by element area; results must still be indexed like the input
            var big = new Rect(500, 500, 300, 300);
            var medium = new Rect(100, 100, 100, 100);
            var small = new Rect(10, 10, 12, 8);
            var labels = new[] { new Size(30, 12), new Size(20, 10), new Size(40, 16) };

            var placed = LabelLayout.Place(new[] { big, medium, small }, labels, Area);

            for (var i = 0; i < 3; i++)
            {
                var element = new[] { big, medium, small }[i];
                Assert.Equal(ActionPoint.FitLabel(element.Size, labels[i]), placed[i].Size);
            }
            Assert.Equal(new Size(30, 12), placed[0].Size);
            Assert.Equal(new Size(20, 10), placed[1].Size);
        }

        [Fact]
        public void NoElements_GivesNoRects()
        {
            Assert.Empty(LabelLayout.Place(new Rect[0], new Size[0], Area));
        }

        private static IReadOnlyList<Rect> Place(IReadOnlyList<Rect> elements, Size label)
        {
            return LabelLayout.Place(elements, elements.Select(x => label).ToList(), Area);
        }

        private static bool Overlap(Rect a, Rect b)
        {
            return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
        }
    }
}
