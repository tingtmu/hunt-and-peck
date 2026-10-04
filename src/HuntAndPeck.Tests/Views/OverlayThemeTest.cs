using HuntAndPeck.Views;
using Xunit;

namespace HuntAndPeck.Tests.Views
{
    public class OverlayThemeTest
    {
        [Fact]
        public void AppsUseLightThemeZero_IsDark()
        {
            Assert.Same(OverlayTheme.Dark, OverlayTheme.For(0));
        }

        [Fact]
        public void AppsUseLightThemeOne_IsLight()
        {
            Assert.Same(OverlayTheme.Light, OverlayTheme.For(1));
        }

        [Fact]
        public void MissingOrUnexpectedValue_IsLight()
        {
            Assert.Same(OverlayTheme.Light, OverlayTheme.For(null));
            Assert.Same(OverlayTheme.Light, OverlayTheme.For("0"));
        }
    }
}
