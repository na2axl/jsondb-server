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
using System.Windows.Shapes;

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

            // Set button behavior
            ConnectButton.IsDefault = true;
            CancelButton.IsCancel = true;
        }

        private void ConnectToServer(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Connect(ServerNameBox.Text, UsernameBox.Text, PasswordBox.Password);
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
