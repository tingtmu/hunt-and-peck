using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Colours of the overlay's typed-text box, following the Windows app theme (Settings > Personalization > Colors)
    /// </summary>
    internal sealed class OverlayTheme
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        /// <summary>Near-white Fluent surface, ink text, Windows blue caret</summary>
        public static readonly OverlayTheme Light = new OverlayTheme(
            background: Color.FromArgb(0xF2, 0xFB, 0xFB, 0xFD),
            border: Color.FromArgb(0x24, 0x00, 0x00, 0x00),
            text: Color.FromRgb(0x1B, 0x1B, 0x1F),
            caret: Color.FromRgb(0x00, 0x5F, 0xB8));

        /// <summary>Charcoal Fluent surface, soft white text, light blue caret</summary>
        public static readonly OverlayTheme Dark = new OverlayTheme(
            background: Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2F),
            border: Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF),
            text: Color.FromRgb(0xF3, 0xF3, 0xF5),
            caret: Color.FromRgb(0x60, 0xCD, 0xFF));

        private OverlayTheme(Color background, Color border, Color text, Color caret)
        {
            Background = Freeze(new SolidColorBrush(background));
            Border = Freeze(new SolidColorBrush(border));
            Text = Freeze(new SolidColorBrush(text));
            Caret = Freeze(new SolidColorBrush(caret));
        }

        public Brush Background { get; }
        public Brush Border { get; }
        public Brush Text { get; }
        public Brush Caret { get; }

        /// <summary>The theme matching the current Windows app theme; light if it can't be read (the Windows default)</summary>
        public static OverlayTheme ForSystem()
        {
            return For(ReadAppsUseLightTheme());
        }

        /// <param name="appsUseLightTheme">The AppsUseLightTheme registry value, else null if absent</param>
        public static OverlayTheme For(object appsUseLightTheme)
        {
            return appsUseLightTheme is int value && value == 0 ? Dark : Light;
        }

        /// <summary>Puts the theme's brushes into the resources the overlay's text box refers to</summary>
        public void ApplyTo(ResourceDictionary resources)
        {
            resources["MatchBoxBackground"] = Background;
            resources["MatchBoxBorder"] = Border;
            resources["MatchBoxText"] = Text;
            resources["MatchBoxCaret"] = Caret;
        }

        private static object ReadAppsUseLightTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    return key?.GetValue(AppsUseLightThemeValue);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
            {
                Trace.TraceWarning("Overlay: reading the Windows app theme failed, using the light theme: {0}", ex.Message);
                return null;
            }
        }

        private static Brush Freeze(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
