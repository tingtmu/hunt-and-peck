using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HuntAndPeck.ViewModels;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    /// <remarks>Placement and DPI scaling are handled by <see cref="PhysicalOverlayWindow"/></remarks>
    public partial class OverlayView
    {
        private static readonly Duration ShakeDuration = new Duration(TimeSpan.FromMilliseconds(300));

        public OverlayView()
        {
            InitializeComponent();

            // Read per overlay, so a theme change applies from the next hotkey press
            OverlayTheme.ForSystem().ApplyTo(Resources);

            var created = Stopwatch.StartNew();
            ContentRendered += (sender, e) => Trace.TraceInformation("Overlay rendered {0} ms after it was created", created.ElapsedMilliseconds);
        }

        private void OverlayView_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                return;
            }

            // Runs before the key's text reaches the match box: Shift+letter forces a mouse click. The real Shift
            // state, not the letter's case, so Caps Lock doesn't force clicks.
            var vm = DataContext as OverlayViewModel;
            if (vm != null)
            {
                vm.ForceClick = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            }
        }

        /// <summary>
        /// Rejects typed text that would leave no hint (a typo, a digit), so the typed letters always match
        /// something and a mistake needs no Backspace
        /// </summary>
        private void MatchStringControl_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var vm = DataContext as OverlayViewModel;
            var box = MatchStringControl;
            var typed = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, e.Text);
            if (vm != null && !vm.HasMatch(typed))
            {
                e.Handled = true;
                Shake();
            }
        }

        /// <summary>Shakes the match box, shown even before anything was typed</summary>
        private void Shake()
        {
            var shake = new DoubleAnimationUsingKeyFrames { Duration = ShakeDuration };
            foreach (var offset in new[] { -8.0, 8, -6, 6, -3, 0 })
            {
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(offset));
            }
            // Linear key frames without key times are spread evenly over the duration
            MatchBoxShake.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, shake);

            var show = new DoubleAnimation(1, 1, ShakeDuration) { FillBehavior = FillBehavior.Stop };
            MatchStringControl.BeginAnimation(OpacityProperty, show);
        }
    }
}
