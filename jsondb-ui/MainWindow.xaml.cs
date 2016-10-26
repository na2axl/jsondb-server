using System;
using System.Windows;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private TaskbarIcon TaskIcon { get; set; }

        public MainWindow()
        {
            // Initialize the taskbar icon
            TaskIcon = new TaskbarIcon();
            TaskIcon.Icon = AppResources.ServerStoppedIcon;
            TaskIcon.ToolTipText = AppResources.AppName;
            TaskIcon.TrayMouseDoubleClick += TrayMouseDoubleClick;

            // Start the server
            App.StartServer();

            // Initialize the UI
            InitializeComponent();

            // Check the server state every 1 / 4 seconds
            // var statusChecker = new StatusChecker(this);
            // var stateTimer = new Timer(statusChecker.CheckStatus, null, 0, 250);
            UpdateServerState();
        }

        private void TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                Focus();
            }
            else
            {
                WindowState = WindowState.Minimized;
            }
        }

        public void UpdateServerState()
        {
            if (App.ServerIsStopped())
            {
                TaskIcon.Icon = AppResources.ServerStoppedIcon;
                serverStateIcon.Fill = Brushes.Red;
                serverStateNotifierText.Text = "Server Stopped";
                serverStateManager.Content = "Start Server";
                serverRestarter.IsEnabled = false;
                MenuFileRestartServer.IsEnabled = false;
                MenuFileStopServer.IsEnabled = false;
                MenuFileStartServer.IsEnabled = true;
            }
            else
            {
                TaskIcon.Icon = AppResources.ServerStartedIcon;
                serverStateIcon.Fill = Brushes.Green;
                serverStateNotifierText.Text = "Server Started";
                serverStateManager.Content = "Stop Server";
                serverRestarter.IsEnabled = true;
                MenuFileRestartServer.IsEnabled = true;
                MenuFileStopServer.IsEnabled = true;
                MenuFileStartServer.IsEnabled = false;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            App.StopServer();
            base.OnClosing(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            ShowInTaskbar = WindowState != WindowState.Minimized;

            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        }

        private void ServerStateToggle(object sender, RoutedEventArgs e)
        {
            if (App.ServerIsStopped())
            {
                App.StartServer();
            }
            else
            {
                App.StopServer();
            }
            UpdateServerState();
        }

        private void ServerRestart(object sender, RoutedEventArgs e)
        {
            App.StopServer();
            App.StartServer();
            UpdateServerState();
        }

        private void ServerStart(object sender, RoutedEventArgs e)
        {
            App.StartServer();
            UpdateServerState();
        }

        private void ServerStop(object sender, RoutedEventArgs e)
        {
            App.StopServer();
            UpdateServerState();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenAboutWindow(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow(this);
            aboutWindow.ShowDialog();
        }

        private void OpenSettingsWindow(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this);
            settingsWindow.ShowDialog();
        }

        private void OpenNewServerWindow(object sender, RoutedEventArgs e)
        {
            var newServerWindow = new NewServerWindow(this);
            newServerWindow.ShowDialog();
        }
    }

    public class StatusChecker
    {

        private MainWindow window { get; set; }

        public StatusChecker(MainWindow w)
        {
            window = w;
        }

        // This method is called by the timer delegate.
        public void CheckStatus(Object stateInfo)
        {
            window.UpdateServerState();
        }
    }
}
