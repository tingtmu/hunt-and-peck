using System.Windows.Input;
using HuntAndPeck.ViewModels;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    /// <remarks>Placement and DPI scaling are handled by <see cref="PhysicalOverlayWindow"/></remarks>
    public partial class OverlayView
    {
        public OverlayView()
        {
            InitializeComponent();

            // Read per overlay, so a theme change applies from the next hotkey press
            OverlayTheme.ForSystem().ApplyTo(Resources);
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
    }
}
