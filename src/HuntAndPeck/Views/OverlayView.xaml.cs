using System.Windows.Input;

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
        }

        private void OverlayView_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
