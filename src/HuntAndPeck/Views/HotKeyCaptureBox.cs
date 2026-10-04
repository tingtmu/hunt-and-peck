using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HuntAndPeck.Configuration;
using HuntAndPeck.NativeMethods;
using Keys = System.Windows.Forms.Keys;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Read-only text box that records a pressed key combination and passes it to <see cref="CaptureCommand"/>
    /// as a <see cref="HotKeyCombination"/>. Tab, Enter and Esc without Ctrl, Alt or Win keep their usual
    /// meaning, so the dialog stays keyboard navigable.
    /// </summary>
    public sealed class HotKeyCaptureBox : TextBox
    {
        public static readonly DependencyProperty CaptureCommandProperty = DependencyProperty.Register(
            nameof(CaptureCommand), typeof(ICommand), typeof(HotKeyCaptureBox));

        public HotKeyCaptureBox()
        {
            IsReadOnly = true;
            IsReadOnlyCaretVisible = false;
            IsUndoEnabled = false;
            ContextMenu = null;
            InputMethod.SetIsInputMethodEnabled(this, false);
        }

        /// <summary>Receives each pressed combination; validation is up to the command</summary>
        public ICommand CaptureCommand
        {
            get { return (ICommand)GetValue(CaptureCommandProperty); }
            set { SetValue(CaptureCommandProperty, value); }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            var modifiers = CurrentModifiers();
            if (IsNavigation(key, modifiers))
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            e.Handled = true;
            if (IsModifierKey(key))
            {
                // Wait for the actual key
                return;
            }

            var combination = new HotKeyCombination(modifiers, (Keys)KeyInterop.VirtualKeyFromKey(key));
            if (CaptureCommand != null && CaptureCommand.CanExecute(combination))
            {
                CaptureCommand.Execute(combination);
            }
        }

        private static KeyModifier CurrentModifiers()
        {
            var wpf = Keyboard.Modifiers;
            KeyModifier modifiers = 0;
            if (wpf.HasFlag(ModifierKeys.Alt))
            {
                modifiers |= KeyModifier.Alt;
            }
            if (wpf.HasFlag(ModifierKeys.Control))
            {
                modifiers |= KeyModifier.Control;
            }
            if (wpf.HasFlag(ModifierKeys.Shift))
            {
                modifiers |= KeyModifier.Shift;
            }
            if (wpf.HasFlag(ModifierKeys.Windows))
            {
                modifiers |= KeyModifier.Windows;
            }
            return modifiers;
        }

        private static bool IsNavigation(Key key, KeyModifier modifiers)
        {
            var plain = (modifiers & (KeyModifier.Alt | KeyModifier.Control | KeyModifier.Windows)) == 0;
            return plain && (key == Key.Tab || key == Key.Enter || key == Key.Escape);
        }

        private static bool IsModifierKey(Key key)
        {
            switch (key)
            {
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LWin:
                case Key.RWin:
                case Key.None:
                    return true;
                default:
                    return false;
            }
        }
    }
}
