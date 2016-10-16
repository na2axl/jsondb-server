using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Hardcodet.Wpf.TaskbarNotification;

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
            TaskIcon = new TaskbarIcon();
            ResizeMode = ResizeMode.CanMinimize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            App.StartServer();
            InitializeComponent();
            UpdateState();
        }

        private void UpdateState()
        {
            if (App.ServerIsStopped())
            {
                serverStateIcon.Fill = Brushes.Red;
                serverStateNotifierText.Text = "Server Stopped";
                serverStateManager.Content = "Start Server";
                serverRestarter.IsEnabled = false;
            }
            else
            {
                serverStateIcon.Fill = Brushes.Green;
                serverStateNotifierText.Text = "Server Started";
                serverStateManager.Content = "Stop Server";
                serverRestarter.IsEnabled = true;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            App.KillServer();
            base.OnClosing(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
            }

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
                App.KillServer();
            }
            UpdateState();
        }

        private void ServerRestart(object sender, RoutedEventArgs e)
        {
            App.KillServer();
            App.StartServer();
        }
    }
}
