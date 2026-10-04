using System;
using System.Windows;
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

        private OptionsViewModel ViewModel => DataContext as OptionsViewModel;

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
