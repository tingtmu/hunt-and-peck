using System.Windows;
using HuntAndPeck.ViewModels;
using System.Linq;
using HuntAndPeck.Services;
using HuntAndPeck.Views;
using HuntAndPeck.Models;

namespace HuntAndPeck
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly SingleLaunchMutex _singleLaunchMutex = new SingleLaunchMutex();
        private readonly UiAutomationHintProviderService _hintProviderService = new UiAutomationHintProviderService();
        private readonly HintLabelService _hintLabelService = new HintLabelService();
        private KeyListenerService _keyListenerService;

        private void ShowOverlay(OverlayViewModel vm)
        {
            var view = new OverlayView
            {
                DataContext = vm
            };
            vm.CloseOverlay = () => view.Close();
            view.ShowDialog();
        }

        private void ShowDebugOverlay(DebugOverlayViewModel vm)
        {
            var view = new DebugOverlayView
            {
                DataContext = vm
            };
            view.ShowDialog();
        }

        private void ShowOptions(OptionsViewModel vm)
        {
            var view = new OptionsView
            {
                DataContext = vm
            };
            view.ShowDialog();
        }

        /// <summary>
        /// Shows the overlay for a headless (/hint, /tray) invocation, or shuts down if there is nothing to show
        /// </summary>
        /// <returns>True if the overlay was shown</returns>
        private bool ShowHeadlessOverlay(HintSession session)
        {
            if (session == null)
            {
                Current.Shutdown();
                return false;
            }

            var overlayWindow = new OverlayView()
            {
                DataContext = new OverlayViewModel(session, _hintLabelService)
            };
            overlayWindow.Show();
            return true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Contains("/hint"))
            {
                // support headless mode
                if (!ShowHeadlessOverlay(_hintProviderService.EnumHints()))
                {
                    return;
                }
            }
            else if (e.Args.Contains("/tray"))
            {
                // support headless tray mode
                if (!ShowHeadlessOverlay(_hintProviderService.EnumHints(Taskbar.FindPrimaryTaskbar())))
                {
                    return;
                }
            }
            else
            {
                // Prevent multiple startup in non-headless mode
                if (_singleLaunchMutex.AlreadyRunning)
                {
                    Current.Shutdown();
                    return;
                }

                // Create this as late as possible as it has a window
                _keyListenerService = new KeyListenerService();

                var shellViewModel = new ShellViewModel(
                    ShowOverlay,
                    ShowDebugOverlay,
                    ShowOptions,
                    _hintLabelService,
                    _hintProviderService,
                    _hintProviderService,
                    _keyListenerService);

                var shellView = new ShellView
                {
                    DataContext = shellViewModel
                };
                shellView.Show();
            }
            base.OnStartup(e);
        }
    }
}
