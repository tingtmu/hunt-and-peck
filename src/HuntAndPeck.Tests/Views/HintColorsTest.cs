using System;
using HuntAndPeck.Configuration;
using HuntAndPeck.Views;
using System.Windows.Media;
using Xunit;

namespace HuntAndPeck.Tests.Views
{
    public class HintColorsTest
    {
        [Theory]
        [InlineData("Classic", true, 0xFFFF00, 0x000000, null)]
        [InlineData("Classic", false, 0xFFFF00, 0x000000, null)]
        [InlineData("Theme", true, 0xFBFBFD, 0x1B1B1F, 0x005FB8)]
        [InlineData("Theme", false, 0x2B2B2F, 0xF3F3F5, 0x60CDFF)]
        [InlineData("HighContrast", true, 0x000000, 0xFFFF00, 0xFFFFFF)]
        [InlineData("HighContrast", false, 0x000000, 0xFFFF00, 0xFFFFFF)]
        public void For_ReturnsFrozenBrushes(string styleName, bool light, int background, int foreground, int? border)
        {
            var colors = HintColors.For((HintStyle)Enum.Parse(typeof(HintStyle), styleName), light);

            AssertColor(background, colors.Background);
            AssertColor(foreground, colors.Foreground);
            if (border.HasValue)
            {
                AssertColor(border.Value, colors.Border);
            }
            else
            {
                Assert.Equal(Colors.Transparent, ((SolidColorBrush)colors.Border).Color);
            }
            Assert.True(colors.Background.IsFrozen && colors.Foreground.IsFrozen && colors.Border.IsFrozen);
        }

        private static void AssertColor(int rgb, Brush brush)
        {
            var expected = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            Assert.Equal(expected, ((SolidColorBrush)brush).Color);
        }
    }
}

