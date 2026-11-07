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
        public void LabelCentre_LargeElement_IsTheLabelsCentreAtTheTopLeftCorner()
        {
            // A wide list row: the hover lands on the label, not 400 px away in the row's middle
            var point = ActionPoint.LabelCentre(100, 200, 900, 260, new Size(30, 20));

            Assert.Equal(new Point(115, 210), point);
        }

        [Fact]
        public void LabelCentre_ElementSmallerThanTheLabel_ShrinksTheLabelUniformlyLikeTheViewbox()
        {
            // 40x10 label in a 20x40 element: scaled by 0.5 to 20x5
            var point = ActionPoint.LabelCentre(0, 0, 20, 40, new Size(40, 10));

            Assert.Equal(new Point(10, 2), point);
        }

        [Fact]
        public void LabelCentre_LabelFillsTheElement_IsTheElementsCentre()
        {
            Assert.Equal(new Point(15, 10), ActionPoint.LabelCentre(0, 0, 30, 20, new Size(30, 20)));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 10)]
        public void LabelCentre_UnknownLabelSize_IsTheElementsCentre(double width, double height)
        {
            Assert.Equal(new Point(150, 120), ActionPoint.LabelCentre(100, 100, 200, 140, new Size(width, height)));
        }

        [Fact]
        public void LabelCentre_EmptySize_IsTheElementsCentre()
        {
            Assert.Equal(new Point(150, 120), ActionPoint.LabelCentre(100, 100, 200, 140, Size.Empty));
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
