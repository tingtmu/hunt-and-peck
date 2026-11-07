using System.Windows;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class ActionPointTest
    {
        [Fact]
        public void Centre_IsTheMiddleOfTheRectangle()
        {
            Assert.Equal(new Point(150, 120), ActionPoint.Centre(100, 100, 200, 140));
        }

        [Fact]
        public void LabelCentre_LabelAtTheCorner_IsTheLabelsCentreAtTheTopLeftCorner()
        {
            // A wide list row: the hover lands on the label, not 400 px away in the row's middle
            var point = ActionPoint.LabelCentre(100, 200, 900, 260, new Rect(0, 0, 30, 20));

            Assert.Equal(new Point(115, 210), point);
        }

        [Fact]
        public void LabelCentre_LabelMovedInsideTheElement_IsTheMovedLabelsCentre()
        {
            // Slid 40 px right of the corner to make way for another label
            var point = ActionPoint.LabelCentre(100, 200, 900, 260, new Rect(40, 0, 30, 20));

            Assert.Equal(new Point(155, 210), point);
        }

        [Fact]
        public void LabelCentre_LabelLeftOfTheElement_IsClampedIntoTheElement()
        {
            var point = ActionPoint.LabelCentre(100, 200, 900, 260, new Rect(-30, 0, 30, 20));

            Assert.Equal(new Point(100, 210), point);
        }

        [Fact]
        public void LabelCentre_LabelAboveTheElement_IsClampedIntoTheElement()
        {
            var point = ActionPoint.LabelCentre(100, 200, 900, 260, new Rect(0, -20, 30, 20));

            Assert.Equal(new Point(115, 200), point);
        }

        [Fact]
        public void LabelCentre_LabelBeyondTheFarEdges_IsClampedToTheLastPixel()
        {
            var point = ActionPoint.LabelCentre(100, 200, 140, 220, new Rect(100, 100, 30, 20));

            Assert.Equal(new Point(139, 219), point);
        }

        [Fact]
        public void LabelCentre_LabelFillsTheElement_IsTheElementsCentre()
        {
            Assert.Equal(new Point(15, 10), ActionPoint.LabelCentre(0, 0, 30, 20, new Rect(0, 0, 30, 20)));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        public void LabelCentre_NonPositiveLabelSize_IsTheElementsCentre(double width, double height)
        {
            Assert.Equal(new Point(150, 120), ActionPoint.LabelCentre(100, 100, 200, 140, new Rect(5, 5, width, height)));
        }

        [Fact]
        public void LabelCentre_EmptyRect_IsTheElementsCentre()
        {
            Assert.Equal(new Point(150, 120), ActionPoint.LabelCentre(100, 100, 200, 140, Rect.Empty));
        }

        [Fact]
        public void FitLabel_ElementLargerThanTheLabel_ReturnsTheLabelUnchanged()
        {
            Assert.Equal(new Size(30, 20), ActionPoint.FitLabel(new Size(800, 60), new Size(30, 20)));
        }

        [Fact]
        public void FitLabel_ElementSmallerThanTheLabel_ShrinksTheLabelUniformlyLikeTheViewbox()
        {
            // 40x10 label in a 20x40 element: scaled by 0.5 to 20x5
            Assert.Equal(new Size(20, 5), ActionPoint.FitLabel(new Size(20, 40), new Size(40, 10)));
        }

        [Fact]
        public void FitLabel_TheTighterDimensionDecidesTheScale()
        {
            // Width allows 0.5, height allows 0.25
            Assert.Equal(new Size(10, 5), ActionPoint.FitLabel(new Size(20, 5), new Size(40, 20)));
        }

        [Fact]
        public void FitLabel_ZeroSizedElement_ShrinksTheLabelToNothing()
        {
            Assert.Equal(new Size(0, 0), ActionPoint.FitLabel(new Size(0, 50), new Size(40, 20)));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        public void FitLabel_UnknownLabelSize_IsReturnedUnchanged(double width, double height)
        {
            Assert.Equal(new Size(width, height), ActionPoint.FitLabel(new Size(100, 100), new Size(width, height)));
        }

        [Fact]
        public void FitLabel_EmptyLabelSize_IsReturnedUnchanged()
        {
            Assert.Equal(Size.Empty, ActionPoint.FitLabel(new Size(100, 100), Size.Empty));
        }

        [Fact]
        public void EstimateLabelSize_GrowsWithTheLetterCountAndFontSize()
        {
            var one = ActionPoint.EstimateLabelSize("A", 14);
            var two = ActionPoint.EstimateLabelSize("AB", 14);
            var big = ActionPoint.EstimateLabelSize("AB", 28);

            Assert.True(two.Width > one.Width);
            Assert.Equal(one.Height, two.Height);
            Assert.True(big.Width > two.Width && big.Height > two.Height);
            // Two bold 14 px capitals with the 1-px margin and border: about 25 x 21 px
            Assert.InRange(two.Width, 20, 30);
            Assert.InRange(two.Height, 17, 24);
        }

        [Fact]
        public void EstimateLabelSize_EmptyLabel_CountsAsOneLetter()
        {
            Assert.Equal(ActionPoint.EstimateLabelSize("A", 14), ActionPoint.EstimateLabelSize("", 14));
        }
    }
}
