using System.Windows;
using HuntAndPeck.ViewModels;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Interaction logic for ShellView.xaml
    /// </summary>
    public partial class ShellView : Window
    {
        public ShellView()
        {
            InitializeComponent();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Start with Windows can be changed outside the app (Task Manager, Settings), so re-read it
            (DataContext as ShellViewModel)?.RefreshStartWithWindows();
        }
    }
}
