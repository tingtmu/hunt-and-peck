using System;
using System.Windows;
using HuntAndPeck.Extensions;
using Xunit;

namespace HuntAndPeck.Tests.Extensions
{
    public class DpiHelperTest
    {
        [Theory]
        [InlineData(96u, 1.0)]
        [InlineData(120u, 1.25)]
        [InlineData(144u, 1.5)]
        [InlineData(168u, 1.75)]
        [InlineData(192u, 2.0)]
        public void ScaleFromDpi_StandardScales(uint dpi, double expectedScale)
        {
            Assert.Equal(expectedScale, DpiHelper.ScaleFromDpi(dpi), 10);
        }

        [Fact]
        public void ScaleFromDpi_UnknownDpi_FallsBackToOne()
        {
            Assert.Equal(1.0, DpiHelper.ScaleFromDpi(0));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.5)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void PhysicalToDipFactor_InvalidScale_Throws(double scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DpiHelper.PhysicalToDipFactor(scale));
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(1.25)]
        [InlineData(1.5)]
        [InlineData(1.75)]
        [InlineData(2.0)]
        public void PhysicalToDipFactor_RoundTripsToPhysicalPixels(double scale)
        {
            // Content laid out in physical units, scaled by the factor, then rendered by WPF at the device scale
            const double physical = 1234;
            Assert.Equal(physical, physical * DpiHelper.PhysicalToDipFactor(scale) * scale, 9);
        }

        [Theory]
        [InlineData(1.0, 100, 200, 300, 400)]
        [InlineData(1.25, 80, 160, 240, 320)]
        [InlineData(1.5, 66.666666667, 133.333333333, 200, 266.666666667)]
        [InlineData(2.0, 50, 100, 150, 200)]
        public void PhysicalToDip_ScalesAllComponents(double scale, double x, double y, double width, double height)
        {
            var result = DpiHelper.PhysicalToDip(new Rect(100, 200, 300, 400), scale);

            Assert.Equal(x, result.X, 6);
            Assert.Equal(y, result.Y, 6);
            Assert.Equal(width, result.Width, 6);
            Assert.Equal(height, result.Height, 6);
        }

        [Fact]
        public void PhysicalToDip_NegativeCoordinates_SecondaryMonitorLeftOfPrimary()
        {
            var result = DpiHelper.PhysicalToDip(new Rect(-2880, -300, 1440, 900), 1.5);

            Assert.Equal(new Rect(-1920, -200, 960, 600), result);
        }

        [Fact]
        public void PhysicalToDip_EmptyRect_StaysEmpty()
        {
            Assert.True(DpiHelper.PhysicalToDip(Rect.Empty, 1.5).IsEmpty);
        }
    }
}
