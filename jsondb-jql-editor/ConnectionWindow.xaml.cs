using System;
using System.Windows;
using System.Windows.Controls;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for ConnectionWindow.xaml
    /// </summary>
    public partial class ConnectionWindow : Window
    {
        public ConnectionWindow(Window o)
        {
            // Set the owner window
            Owner = o;

            // Initialize UI
            InitializeComponent();
            ServerNameBox.Focus();

            // Set button behavior
            ConnectButton.IsDefault = true;
            CancelButton.IsCancel = true;
        }

        private void ConnectToServer(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Connect(ServerNameBox.Text, UsernameBox.Text, PasswordBox.Password, DatabaseName.Text);
                Close();
            }
            catch (Exception ex)
            {
                new MessageWindow(
                    this,
                    ex.Message,
                    "An error occurred",
                    MessageWindowButton.OK,
                    MessageWindowImage.Error).Open();
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
