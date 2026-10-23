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

            // Runs before the key's text reaches the match box: the modifiers held with the letter choose a mouse
            // action (OverlayViewModel.ForcedActionFor). The real Shift state, not the letter's case, so Caps Lock
            // doesn't force clicks.
            var modifiers = Keyboard.Modifiers;
            var vm = DataContext as OverlayViewModel;
            if (vm != null)
            {
                vm.ForcedAction = OverlayViewModel.ForcedActionFor(
                    (modifiers & ModifierKeys.Shift) != 0,
                    (modifiers & ModifierKeys.Control) != 0);
            }

            // Ctrl+letter types no text (the box would run Ctrl+A, Ctrl+C, ...), so type the letter here
            if ((modifiers & ModifierKeys.Control) != 0 && (modifiers & ModifierKeys.Alt) == 0 && e.Key >= Key.A && e.Key <= Key.Z)
            {
                e.Handled = true;
                var letter = ((char)('A' + (e.Key - Key.A))).ToString();
                if (Accepts(letter))
                {
                    MatchStringControl.SelectedText = letter;
                    MatchStringControl.CaretIndex = MatchStringControl.Text.Length;
                }
            }
        }

        /// <summary>
        /// Rejects typed text that would leave no hint (a typo, a digit), so the typed letters always match
        /// something and a mistake needs no Backspace
        /// </summary>
        private void MatchStringControl_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!Accepts(e.Text))
            {
                e.Handled = true;
            }
        }

        /// <returns>True if some hint matches once <paramref name="text"/> is typed; else shakes the box</returns>
        private bool Accepts(string text)
        {
            var vm = DataContext as OverlayViewModel;
            var box = MatchStringControl;
            var typed = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, text);
            if (vm == null || vm.HasMatch(typed))
            {
                return true;
            }
            Shake();
            return false;
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
