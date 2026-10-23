using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HuntAndPeck.ViewModels;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Interaction logic for OptionsView.xaml
    /// </summary>
    public partial class OptionsView : Window
    {
        public OptionsView()
        {
            InitializeComponent();
        }

        /// <summary>Pixels scrolled per mouse wheel notch; the WPF default is 48 (3 lines)</summary>
        private const double WheelPixelsPerNotch = 28;

        private OptionsViewModel ViewModel => DataContext as OptionsViewModel;

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (FontSizeBox.IsDropDownOpen || HintStyleBox.IsDropDownOpen)
            {
                // The wheel belongs to the open list
                return;
            }

            var scroller = (ScrollViewer)sender;
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset - e.Delta / 120.0 * WheelPixelsPerNotch);
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Never leave the global hotkeys suspended
            ViewModel?.EndHotKeyCapture();
            base.OnClosed(e);
        }

        private void HotKeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ViewModel?.BeginHotKeyCapture();
        }

        private void HotKeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ViewModel?.EndHotKeyCapture();
        }
    }
}
