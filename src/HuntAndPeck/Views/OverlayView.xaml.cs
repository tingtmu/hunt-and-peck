using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using HuntAndPeck.NativeMethods;
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

            // Before the first render: the view model is set before the window is shown
            DataContextChanged += (sender, e) =>
            {
                var vm = e.NewValue as OverlayViewModel;
                if (vm != null)
                {
                    HintColors.For(vm.HintStyle, OverlayTheme.SystemIsLight()).ApplyTo(Resources);
                }
            };
        }

        /// <summary>
        /// Menu overlay: never activated and click-through, so the menu under it stays open; its keys come from a
        /// keyboard hook (<see cref="HandleHookKey"/>). Set before the window is shown.
        /// </summary>
        public bool Passive { get; set; }

        protected override bool TakesForeground => !Passive;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (Passive)
            {
                var hWnd = new WindowInteropHelper(this).Handle;
                WindowEnumeration.SetExStyle(hWnd, WindowEnumeration.GetExStyle(hWnd)
                    | WindowEnumeration.WS_EX_NOACTIVATE | WindowEnumeration.WS_EX_TRANSPARENT | WindowEnumeration.WS_EX_TOOLWINDOW);
            }
        }

        /// <summary>
        /// A key pressed while this is a <see cref="Passive"/> overlay. Letters (with Shift/Ctrl as usual) and
        /// Backspace work the hints, Esc closes them; any other key closes them and goes on to the menu, so the
        /// menu's own keys (arrows, Enter, underlined letters after Esc) keep working.
        /// </summary>
        /// <returns>True if the key is used here and must not reach the menu</returns>
        public bool HandleHookKey(int virtualKey)
        {
            if (_closed)
            {
                // The hook is removed just after the window closed
                return false;
            }

            var key = KeyInterop.KeyFromVirtualKey(virtualKey);
            switch (key)
            {
                case Key.Escape:
                    Close();
                    return true;
                case Key.Back:
                    var text = MatchStringControl.Text;
                    if (text.Length > 0)
                    {
                        MatchStringControl.Text = text.Substring(0, text.Length - 1);
                        MatchStringControl.CaretIndex = MatchStringControl.Text.Length;
                    }
                    return true;
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    // Held for the next letter
                    return false;
            }

            var alt = KeyboardHook.IsKeyDown(VkMenu);
            if (key < Key.A || key > Key.Z || alt)
            {
                Close();
                return false;
            }

            var vm = DataContext as OverlayViewModel;
            if (vm != null)
            {
                vm.ForcedAction = OverlayViewModel.ForcedActionFor(KeyboardHook.IsKeyDown(VkShift), KeyboardHook.IsKeyDown(VkControl));
            }
            var letter = ((char)('A' + (key - Key.A))).ToString();
            if (Accepts(letter))
            {
                MatchStringControl.Text += letter;
                MatchStringControl.CaretIndex = MatchStringControl.Text.Length;
            }
            return true;
        }

        private bool _closed;

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            base.OnClosed(e);
        }

        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;

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
