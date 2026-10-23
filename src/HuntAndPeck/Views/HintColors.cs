using System.Windows;
using System.Windows.Media;
using HuntAndPeck.Configuration;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Colours of the overlay's hint labels for a <see cref="HintStyle"/>
    /// </summary>
    internal sealed class HintColors
    {
        private static readonly HintColors Classic = new HintColors(Colors.Yellow, Colors.Black, Colors.Transparent);

        private static readonly HintColors Light = new HintColors(
            Color.FromRgb(0xFB, 0xFB, 0xFD), Color.FromRgb(0x1B, 0x1B, 0x1F), Color.FromRgb(0x00, 0x5F, 0xB8));

        private static readonly HintColors Dark = new HintColors(
            Color.FromRgb(0x2B, 0x2B, 0x2F), Color.FromRgb(0xF3, 0xF3, 0xF5), Color.FromRgb(0x60, 0xCD, 0xFF));

        private static readonly HintColors HighContrast = new HintColors(Colors.Black, Color.FromRgb(0xFF, 0xFF, 0x00), Colors.White);

        private HintColors(Color background, Color foreground, Color border)
        {
            Background = Freeze(new SolidColorBrush(background));
            Foreground = Freeze(new SolidColorBrush(foreground));
            Border = Freeze(new SolidColorBrush(border));
        }

        public Brush Background { get; }
        public Brush Foreground { get; }
        public Brush Border { get; }

        /// <param name="style">The hint style</param>
        /// <param name="lightTheme">True if the Windows app theme is light; only used by <see cref="HintStyle.Theme"/></param>
        public static HintColors For(HintStyle style, bool lightTheme)
        {
            switch (style)
            {
                case HintStyle.Theme:
                    return lightTheme ? Light : Dark;
                case HintStyle.HighContrast:
                    return HighContrast;
                default:
                    return Classic;
            }
        }

        /// <summary>Puts the brushes into the resources the overlay's hint labels refer to</summary>
        public void ApplyTo(ResourceDictionary resources)
        {
            resources["HintBackground"] = Background;
            resources["HintForeground"] = Foreground;
            resources["HintBorder"] = Border;
        }

        private static Brush Freeze(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
